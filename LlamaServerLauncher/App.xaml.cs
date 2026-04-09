using LlamaServerLauncher.Resources;

namespace LlamaServerLauncher;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;

    private bool _notifyIconCreated;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        
        LocalizedStrings.CultureChanged += OnCultureChanged;
        
        _mainWindow = new MainWindow();
        _mainWindow.Closing += MainWindow_Closing;
        _mainWindow.Show();
        
        // NotifyIcon will be created by the first ChangeLanguage call
        // If no language change happens, create it here as fallback
        if (!_notifyIconCreated)
        {
            CreateNotifyIcon();
        }
    }
    
    private void OnCultureChanged()
    {
        if (_notifyIconCreated)
        {
            // Icon already exists, just cleanup and recreate for updated text
            CleanupNotifyIcon();
        }
        
        CreateNotifyIcon();
        _notifyIconCreated = true;
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // If cancellation was requested (e.g., user said "No" to confirm close), don't proceed
        if (e.Cancel)
            return;
            
        if (_mainWindow?.DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.StopServerIfRunningAsync();
        }
        
        CleanupNotifyIcon();
        Shutdown();
    }

    private void CleanupNotifyIcon()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.DoubleClick -= OnRestore;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }

    private void CreateNotifyIcon()
    {
        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add(LocalizedStrings.GetString("Show"), null, OnRestore);
        contextMenu.Items.Add("-");
        contextMenu.Items.Add(LocalizedStrings.GetString("CloseProgram"), null, OnExit);

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Visible = true,
            Text = LocalizedStrings.GetString("WindowTitle"),
            ContextMenuStrip = contextMenu
        };
        
        _notifyIcon.DoubleClick += OnRestore;
    }

    private System.Drawing.Icon LoadIcon()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "LlamaServerLauncher.llama-launcher.ico";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                return new System.Drawing.Icon(stream);
            }
        }
        catch
        {
        }
        
        return System.Drawing.SystemIcons.Application;
    }

    private void OnRestore(object? sender, System.EventArgs e)
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
            return;

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }
        
        _mainWindow.WindowState = System.Windows.WindowState.Normal;
        _mainWindow.Activate();
    }

    private async void OnExit(object? sender, System.EventArgs e)
    {
        // Check if server is running and ask for confirmation (same as in MainWindow)
        if (_mainWindow?.DataContext is ViewModels.MainViewModel viewModel && viewModel.IsServerRunning)
        {
            var result = System.Windows.MessageBox.Show(
                LocalizedStrings.GetString("ConfirmCloseMessage"),
                LocalizedStrings.GetString("ConfirmCloseTitle"),
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.No)
            {
                return; // User cancelled - do nothing
            }
        }

        // Proceed with closing
        if (_mainWindow?.DataContext is ViewModels.MainViewModel vm2)
        {
            await vm2.StopServerIfRunningAsync();
        }
        
        CleanupNotifyIcon();
        _mainWindow?.ForceClose();
        Shutdown();
    }
}