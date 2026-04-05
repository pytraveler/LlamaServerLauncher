namespace LlamaServerLauncher;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        
        _mainWindow = new MainWindow();
        _mainWindow.Closing += MainWindow_Closing;
        _mainWindow.Show();
        
        CreateNotifyIcon();
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
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
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }

    private void CreateNotifyIcon()
    {
        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("Show", null, OnRestore);
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Close program", null, OnExit);

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Visible = true,
            Text = "llama-server launcher",
            ContextMenuStrip = contextMenu
        };
        
        _notifyIcon.DoubleClick += OnRestore;
    }

    private System.Drawing.Icon LoadIcon()
    {
        try
        {
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "llama-launcher.ico");
            if (System.IO.File.Exists(iconPath))
            {
                return new System.Drawing.Icon(iconPath);
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
        if (_mainWindow?.DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.StopServerIfRunningAsync();
        }
        
        CleanupNotifyIcon();
        _mainWindow?.ForceClose();
        Shutdown();
    }
}