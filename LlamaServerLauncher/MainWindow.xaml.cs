using System.ComponentModel;
using System.Windows;
using LlamaServerLauncher.Models;
using LlamaServerLauncher.Services;
using LlamaServerLauncher.ViewModels;

namespace LlamaServerLauncher;

public partial class MainWindow : Window
{
    private readonly ConfigurationService _configService;

    public MainWindow()
    {
        InitializeComponent();
        
        var logService = new LogService();
        _configService = new ConfigurationService(logService);
        
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
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
            viewModel.ApplyAppSettings(settings);
        }
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            var settings = viewModel.GetAppSettings();
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