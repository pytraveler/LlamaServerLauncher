using System.ComponentModel;
using System.Windows;
using LlamaServerLauncher.Models;
using LlamaServerLauncher.Resources;
using LlamaServerLauncher.Services;
using LlamaServerLauncher.ViewModels;

namespace LlamaServerLauncher;

public partial class MainWindow : Window
{
    private readonly ConfigurationService _configService;
    private MainViewModel? _viewModel;
    private int _closeFlag;

    public MainWindow()
    {
        InitializeComponent();
        
        var logService = new LogService();
        _configService = new ConfigurationService(logService);
        
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // This is handled by MainWindow_Loaded
    }

    private void MainWindow_StateChanged(object? sender, System.EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = await _configService.LoadAppSettingsAsync();

        if (settings.WindowWidth > 0) Width = settings.WindowWidth;
        if (settings.WindowHeight > 0) Height = settings.WindowHeight;

        if (settings.WindowLeft.HasValue && settings.WindowTop.HasValue)
        {
            var left = settings.WindowLeft.Value;
            var top = settings.WindowTop.Value;
            
            var screenBounds = SystemParameters.WorkArea;
            if (left >= 0 && left + Width <= screenBounds.Width)
                Left = left;
            if (top >= 0 && top + Height <= screenBounds.Height)
                Top = top;
        }

        if (DataContext is MainViewModel viewModel)
        {
            _viewModel = viewModel;
            viewModel.ApplyAppSettings(settings);
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.LogText) && _viewModel?.AutoScroll == true)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.ScrollToEnd();
            });
        }
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Get view model reference once
        MainViewModel? viewModel = DataContext as MainViewModel;
        bool isServerRunning = viewModel?.IsServerRunning ?? false;
        
        // Prevent multiple close attempts - use Interlocked for thread safety
        if (System.Threading.Interlocked.Exchange(ref _closeFlag, 1) == 1)
        {
            e.Cancel = true;
            return;
        }

        try
        {
            // Check if server is running and ask for confirmation
            if (isServerRunning && viewModel != null)
            {
                // Cancel the close first, show message box, then decide whether to really close
                e.Cancel = true;
                
                string msg = LocalizedStrings.ConfirmCloseMessage;
                string title = LocalizedStrings.ConfirmCloseTitle;
                
                var dialogResult = System.Windows.MessageBox.Show(
                    this,
                    msg,
                    title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                // Reset flag so we can be called again
                _closeFlag = 0;

                if (dialogResult == MessageBoxResult.Yes)
                {
                    // User confirmed - stop server first, then close
                    if (viewModel != null && viewModel.IsServerRunning)
                    {
                        await viewModel.StopServerIfRunningAsync();
                    }
                    
                    // Now proceed with closing
                    Closing -= MainWindow_Closing;
                    Close();
                }
                // If No, do nothing - window stays open
                return;
            }
        }
        finally
        {
            // Reset flag only if we're not cancelling
            if (!e.Cancel)
            {
                // Continue with closing
            }
        }

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (DataContext is MainViewModel vm)
        {
            var settings = vm.GetAppSettings();
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            settings.WindowLeft = Left;
            settings.WindowTop = Top;
            
            await _configService.SaveAppSettingsAsync(settings);
        }
    }

    public void ForceClose()
    {
        Close();
    }
}