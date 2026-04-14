using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using LlamaServerLauncher.Models;
using LlamaServerLauncher.Resources;
using LlamaServerLauncher.Services;
using LlamaServerLauncher.ViewModels;

using static LlamaServerLauncher.Models.CommandLineBuilder;

namespace LlamaServerLauncher;

public partial class MainWindow : Window
{
    private ConfigurationService? _configService;
    private LogService? _logService;
    private MainViewModel? _viewModel;
    private int _closeFlag;
    
    // Drag-and-drop out support
    private System.Windows.Point _dragStartPoint;
    private bool _isDraggingCommand;
    private string? _tempBatFilePath;

    public MainWindow()
    {
        InitializeComponent();
        
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

    private bool _isDraggingValidFile;

    private void MainWindow_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (_isDraggingValidFile)
        {
            DragDropOverlay.Visibility = Visibility.Visible;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.UpArrow;
        }
    }

   private void MainWindow_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        _isDraggingValidFile = false;
        DragDropOverlay.Visibility = Visibility.Collapsed;
        Mouse.OverrideCursor = null;
    }

    // Preview handlers - called before bubbling events, ensures drag-drop works over any control
    private void Grid_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                var ext = Path.GetExtension(files[0]).ToLowerInvariant();
                if (ext == ".bat" || ext == ".cmd" || ext == ".json")
                {
                    e.Effects = System.Windows.DragDropEffects.Copy;
                    e.Handled = true; // Prevents child controls from handling this event
                    
                    if (!_isDraggingValidFile)
                    {
                        _isDraggingValidFile = true;
                        DragDropOverlay.Visibility = Visibility.Visible;
                        Mouse.OverrideCursor = System.Windows.Input.Cursors.UpArrow;
                    }
                    return;
                }
            }
        }
        
        _isDraggingValidFile = false;
        e.Effects = System.Windows.DragDropEffects.None;
        e.Handled = true;
        DragDropOverlay.Visibility = Visibility.Collapsed;
        Mouse.OverrideCursor = null;
    }

    private void Grid_PreviewDrop(object sender, System.Windows.DragEventArgs e)
    {
        MainWindow_Drop(sender, e);
    }

    private void MainWindow_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                var ext = Path.GetExtension(files[0]).ToLowerInvariant();
                if (ext == ".bat" || ext == ".cmd" || ext == ".json")
                {
                    e.Effects = System.Windows.DragDropEffects.Copy;
                    e.Handled = true;
                    
                    if (!_isDraggingValidFile)
                    {
                        _isDraggingValidFile = true;
                        DragDropOverlay.Visibility = Visibility.Visible;
                        Mouse.OverrideCursor = System.Windows.Input.Cursors.UpArrow;
                    }
                    return;
                }
            }
        }
        
        _isDraggingValidFile = false;
        e.Effects = System.Windows.DragDropEffects.None;
        e.Handled = true;
        DragDropOverlay.Visibility = Visibility.Collapsed;
        Mouse.OverrideCursor = null;
    }

    private async void MainWindow_Drop(object sender, System.Windows.DragEventArgs e)
    {
        _isDraggingValidFile = false;
        DragDropOverlay.Visibility = Visibility.Collapsed;
        Mouse.OverrideCursor = null;

        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            return;

        var files = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
        if (files == null || files.Length == 0)
            return;

        var filePath = files[0];
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        
        try
        {
            if (ext == ".json")
            {
                await LoadJsonProfileAsync(filePath);
            }
            else if (ext == ".bat" || ext == ".cmd")
            {
                await LoadBatFileAsync(filePath);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                string.Format(LocalizedStrings.GetString("ErrorLoadingFile"), ex.Message),
                LocalizedStrings.ErrorTitle,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private static (string exePath, string args) ExtractLlamaCommand(string batContent)
    {
        const string exeName = "llama-server.exe";
        
        // Разбиваем на строки, но сохраняем информацию о пустых строках
        var rawLines = batContent.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        var nonEmptyLines = new List<string>();
        
        for (int i = 0; i < rawLines.Length; i++)
        {
            var line = rawLines[i];
            if (!string.IsNullOrWhiteSpace(line))
            {
                nonEmptyLines.Add(line.Trim());
            }
            else
            {
                // Добавляем маркер пустой строки чтобы разделить логические блоки
                nonEmptyLines.Add("__EMPTY_LINE_MARKER__");
            }
        }

        for (int i = 0; i < nonEmptyLines.Count; i++)
        {
            var line = nonEmptyLines[i];
            
            // Пропускаем маркеры пустых строк и продолжаем поиск
            if (line == "__EMPTY_LINE_MARKER__")
                continue;
                
            var result = TryExtractFromLine(line, exeName);
            
            // Если нашли llama-server с валидными аргументами - возвращаем результат
            if (!string.IsNullOrEmpty(result.args) && HasValidArguments(result.args))
            {
                return result;
            }
        }

        return (string.Empty, string.Empty);
    }
    
    private static bool HasValidArguments(string argsStr)
    {
        // Проверяем что строка содержит реальные аргументы командной строки llama-server
        if (string.IsNullOrWhiteSpace(argsStr))
            return false;
            
        var trimmed = argsStr.Trim();
        
        // Если начинается с - или --, это явный аргумент llama-server
        if (trimmed.StartsWith("--") || trimmed.StartsWith("-"))
            return true;
            
        // Если это модель .gguf файла (путь к модели)
        if (trimmed.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains(".gguf\""))
            return true;
            
        // Если это директория моделей
        if (trimmed.Contains("--models-dir") || 
            trimmed.Contains("models\\") || 
            trimmed.Contains("models/"))
            return true;
            
        // Короткие аргументы типа /F или одиночные буквы/числа - НЕ валидны для запуска сервера
        if (trimmed.Length <= 3 && !trimmed.Contains("\\") && !trimmed.Contains("/"))
            return false;
            
        // Если это явно путь Windows или Unix
        if (trimmed.StartsWith("." + Path.DirectorySeparatorChar) ||
            trimmed.StartsWith(Path.DirectorySeparatorChar.ToString()) ||
            (trimmed.Length > 1 && trimmed[1] == ':'))
            return true;
            
        // Проверяем что есть минимум 2 слова в аргументах
        var words = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Для llama-server нужно как минимум: параметр(-m, --model, --port и т.д.) И его значение
        // Например: "-m model.gguf" или "--host 0.0.0.0"
        bool hasFlag = words.Any(w => w.StartsWith("-"));
        
        return hasFlag || words.Length >= 2;
    }

    /// <summary>
    /// Пытается извлечь путь к exe и аргументы из одной строки
    /// </summary>
    private static (string exePath, string args) TryExtractFromLine(string line, string exeName)
    {
        var lowerLine = line.ToLowerInvariant();
        var idx = lowerLine.IndexOf(exeName, StringComparison.OrdinalIgnoreCase);
        
        if (idx < 0)
            return (string.Empty, string.Empty);

        // Определяем начало строки для поиска пути (ищем назад от llama-server.exe)
        int searchStart = Math.Max(0, idx - 150); // Ищем путь в пределах ~150 символов
        
        // Извлекаем потенциальный путь перед llama-server.exe
        // Используем список символов чтобы потом перевернуть в правильном порядке
        var pathChars = new List<char>();
        for (int i = idx - 1; i >= searchStart && i >= 0; i--)
        {
            char c = line[i];
            
            // Если встретили пробел/таб или перенос строки - это конец пути
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                break;
                
            pathChars.Add(c);
        }
        
        // Переворачиваем символы в правильный порядок
        pathChars.Reverse();
        string pathBefore = new string(pathChars.ToArray());

        // Очищаем путь от кавычек и лишних символов
        pathBefore = pathBefore.Trim('"', '\'', ' ', '\t');
        
        // Определяем полный путь к exe
        string fullExePath = "";
        if (!string.IsNullOrEmpty(pathBefore))
        {
            // Если путь содержит разделители пути (\ или /) - используем его
            if (pathBefore.Contains('\\') || pathBefore.Contains('/'))
            {
                fullExePath = pathBefore + exeName;
            }
            else if (pathBefore == "." || pathBefore.EndsWith("."))
            {
                // Относительный путь типа .\ или ./
                fullExePath = ".\\" + exeName;
            }
        }
        
        // Извлекаем аргументы после llama-server.exe
        int argsStartIdx = idx + exeName.Length;
        
        if (argsStartIdx >= line.Length)
            return (fullExePath, string.Empty);

        // БЕРЁМ ВСЮ ОСТАВШУЮСЯ ЧАСТЬ СТРОКИ и очищаем от кавычек и пробелов
        var argsStr = line.Substring(argsStartIdx);
        
        // Если перед exe был путь в кавычках, закрывающая кавычка может быть в начале аргументов
        bool hadQuotedPathBefore = !string.IsNullOrEmpty(pathBefore) && 
                                   (pathBefore.StartsWith('"') || pathBefore.Contains("\""));
        
        if (!hadQuotedPathBefore)
        {
            // Проверяем есть ли кавычка прямо перед llama-server.exe
            for (int i = idx - 1; i >= 0; i--)
            {
                char c = line[i];
                if (c == ' ' || c == '\t')
                    continue;
                if (c == '"' || c == '\'')
                {
                    hadQuotedPathBefore = true;
                    break;
                }
                break;
            }
        }
        
        // Триммим все лишние символы включая кавычки
        argsStr = argsStr.Trim(' ', '\t', '"', '\'');
        
        // Удаляем команды типа 'pause' в конце строки
        var parts = argsStr.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        while (parts.Length > 0 && 
               (parts[^1].Equals("pause", StringComparison.OrdinalIgnoreCase) ||
                parts[^1].Equals("&", StringComparison.OrdinalIgnoreCase)))
        {
            var newParts = parts.Take(parts.Length - 1).ToArray();
            parts = newParts;
        }
        
        argsStr = string.Join(" ", parts);

        return (fullExePath, argsStr);
}

    private async Task LoadJsonProfileAsync(string filePath)
    {
        if (_logService == null || _configService == null) return;
        
        _logService.AppLog($"=== Loading JSON profile: {filePath} ===");
        
        try
        {
            var config = await _configService.LoadProfileFromFileAsync(filePath);
            if (config != null)
            {
                _logService.AppLog($"Loaded config - Model: {config.ModelPath}, Port: {config.Port}");
                _viewModel?.LoadConfigFromCommandLine(config);
            }
            else
            {
                _logService.AppLog("Failed to load JSON profile");
                System.Windows.MessageBox.Show(
                    LocalizedStrings.GetString("ErrorLoadingFile"),
                    LocalizedStrings.ErrorTitle,
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        catch (JsonException ex)
        {
            _logService.AppLog($"JSON parsing error: {ex.Message}");
            System.Windows.MessageBox.Show(
                string.Format(LocalizedStrings.GetString("ErrorLoadingFile"), ex.Message),
                LocalizedStrings.ErrorTitle,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task LoadBatFileAsync(string filePath)
    {
        if (_logService == null) return;
        
        var content = await File.ReadAllTextAsync(filePath);
        _logService.AppLog($"=== Parsing BAT file: {filePath} ===");
        var previewLen = Math.Min(500, content.Length);
        var contentPreview = content.Replace("\r", "\\r").Replace("\n", "\\n").Substring(0, previewLen);
        if (content.Length > 500)
            contentPreview += "...";
        _logService.AppLog($"File content preview: [{contentPreview}]");
        
        var (exePath, args) = ExtractLlamaCommand(content);
        
        _logService.AppLog($">>> Extracted exePath: '{exePath}', args length: {args?.Length ?? 0}");
        if (!string.IsNullOrEmpty(args))
            _logService.AppLog($">>> Args preview: '{(args.Length > 200 ? args.Substring(0, 200) + "..." : args)}'");

        if (!string.IsNullOrEmpty(args))
        {
            var config = ServerConfigurationExtensions.ParseFromCommandLine(args);
            if (config != null)
            {
                if (!string.IsNullOrEmpty(exePath))
                    config.ExecutablePath = exePath;

                _logService.AppLog($"Parsed config - Model: {config.ModelPath}, Port: {config.Port}");
                _viewModel?.LoadConfigFromCommandLine(config);
            }
            else
            {
                _logService.AppLog("Failed to parse command line arguments");
            }
        }
        else
        {
            _logService.AppLog("No llama-server.exe command found in the file");
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Get services from ViewModel to ensure unified logging
        if (DataContext is MainViewModel viewModel)
        {
            _viewModel = viewModel;
            _logService = viewModel.LogService;
            _configService = new ConfigurationService(_logService);
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            
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

            viewModel.ApplyAppSettings(settings);
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
            
            if (_configService != null)
                await _configService.SaveAppSettingsAsync(settings);
        }
    }

    public void ForceClose()
    {
        Close();
    }

    private void ProfileComboBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel?.LoadProfileCommand.CanExecute(null) == true)
        {
            _viewModel.LoadProfileCommand.Execute(null);
            e.Handled = true;
        }
    }

    // Drag-and-drop out: export command as .bat file
    private void CurrentCommandTextBlock_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDraggingCommand = false;
        
        // Check if there's a valid command to export
        if (_viewModel != null && !string.IsNullOrWhiteSpace(_viewModel.CurrentCommand))
        {
            _isDraggingCommand = true;
        }
    }

    private async void CurrentCommandTextBlock_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !_isDraggingCommand)
            return;

        var currentPos = e.GetPosition(null);
        var diff = currentPos - _dragStartPoint;

        // Start drag after moving at least 5 pixels
        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _isDraggingCommand = false; // Prevent multiple initiations

            try
            {
                await StartCommandDragDropAsync();
            }
            catch (Exception ex)
            {
                _logService?.AppLog($"Error during command drag-drop: {ex.Message}");
            }
            finally
            {
                CleanupTempBatFile();
            }
        }
    }

    private async Task StartCommandDragDropAsync()
    {
        if (_viewModel == null || _logService == null)
            return;

        var config = _viewModel.GetCurrentConfig();
        var command = BuildFullCommand(config);
        
        if (string.IsNullOrWhiteSpace(command))
        {
            _logService.AppLog("No command to export");
            return;
        }

        // Generate temp .bat file content (same format as ExportToBatAsync)
        var batContent = $"@echo off\n{command}\npause";
        
        // Create unique temp file name
        var tempDir = Path.GetTempPath();
        var tempFileName = $"llama-server-command-{Guid.NewGuid():N}.bat";
        _tempBatFilePath = Path.Combine(tempDir, tempFileName);

        // Write the BAT file
        await File.WriteAllTextAsync(_tempBatFilePath, batContent);
        _logService.AppLog($"Created temp BAT for drag-drop: {_tempBatFilePath}");

        // Initiate drag-drop with the file
        var dataObject = new System.Windows.DataObject(System.Windows.DataFormats.FileDrop, new[] { _tempBatFilePath });
        
        // Get the TextBlock from namescope (handles XAML naming)
        var textBlock = this.FindName("CurrentCommandTextBlock") as System.Windows.Controls.TextBlock;
        if (textBlock != null)
        {
            System.Windows.DragDrop.DoDragDrop(textBlock, dataObject, System.Windows.DragDropEffects.Copy);
        }
    }

    private void CleanupTempBatFile()
    {
        if (!string.IsNullOrEmpty(_tempBatFilePath) && File.Exists(_tempBatFilePath))
        {
            try
            {
                File.Delete(_tempBatFilePath);
                _logService?.AppLog($"Cleaned up temp BAT file: {_tempBatFilePath}");
            }
            catch (Exception ex)
            {
                _logService?.AppLog($"Failed to delete temp BAT file: {ex.Message}");
            }
            finally
            {
                _tempBatFilePath = null;
            }
        }
    }
}