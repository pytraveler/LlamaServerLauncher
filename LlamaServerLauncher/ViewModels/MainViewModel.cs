using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using LlamaServerLauncher.Models;
using LlamaServerLauncher.Resources;
using LlamaServerLauncher.Services;
using Microsoft.Win32;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

using static LlamaServerLauncher.Models.CommandLineBuilder;

namespace LlamaServerLauncher.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly LlamaServerService _serverService;
    private readonly ConfigurationService _configService;
    private readonly LogService _logService;
    private ServerConfiguration? _loadedProfileConfig;
    private string _loadedProfileName = string.Empty;

    public LocalizedStrings Localized { get; } = LocalizedStrings.Instance;

    private string _selectedLanguage = "en";
    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage != value)
            {
                _selectedLanguage = value;
                OnPropertyChanged();
                ChangeLanguage(value);
            }
        }
    }

    public List<LanguageOption> AvailableLanguages { get; } = new()
    {
        new LanguageOption { Code = "en", Name = "English" },
        new LanguageOption { Code = "ru", Name = "Русский" }
    };

    public List<string> CacheTypes { get; } = new() { "", "f32", "f16", "bf16", "q8_0", "q4_0", "q4_1", "iq4_nl", "q5_0", "q5_1" };

    private void ChangeLanguage(string languageCode)
    {
        var culture = new CultureInfo(languageCode);
        LocalizedStrings.SetCulture(culture);
        OnPropertyChanged(nameof(Localized));
    }

    private string _executablePath = string.Empty;
    private string _modelPath = string.Empty;
    private string _modelsDir = string.Empty;
    private string _host = "127.0.0.1";
    private int _port = 8080;
    private string _contextSize = string.Empty;
    private string _threads = string.Empty;
    private string _gpuLayers = string.Empty;
    private string _temperature = string.Empty;
    private string _maxTokens = string.Empty;
    private string _batchSize = string.Empty;
    private string _uBatchSize = string.Empty;
    private string _minP = string.Empty;
    private string _mmprojPath = string.Empty;
    private string _cacheTypeK = string.Empty;
    private string _cacheTypeV = string.Empty;
    private string _topK = string.Empty;
    private string _topP = string.Empty;
    private string _repeatPenalty = string.Empty;
    private bool? _flashAttention;
    private bool? _enableWebUI;
    private bool? _embeddingMode;
    private bool? _enableSlots;
    private bool? _enableMetrics;
    private string _apiKey = string.Empty;
    private string _logFilePath = string.Empty;
    private bool _verboseLogging;
    private string _customArguments = string.Empty;
    private bool _autoRestart;
    private bool _autoScroll = true;
    private string _selectedProfile = string.Empty;
    private string _profileNameInput = string.Empty;
    private bool _isServerRunning;
    private string _serverStatus = "Stopped";
    private string _currentLog = string.Empty;
    private string _logOutput = string.Empty;
    private string _logText = string.Empty;
    private string _currentCommand = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        _logService = new LogService();
        _logService.LogReceived += OnLogReceived;
        _serverService = new LlamaServerService(_logService);
        _configService = new ConfigurationService(_logService);

        _serverService.OutputReceived += OnServerOutput;
        _serverService.ServerStateChanged += OnServerStateChanged;

        Profiles = new ObservableCollection<string>();

        ChangeLanguage(_selectedLanguage);

        BrowseExecutableCommand = new RelayCommand(BrowseExecutable);
        BrowseModelCommand = new RelayCommand(BrowseModel);
        BrowseModelsDirCommand = new RelayCommand(BrowseModelsDir);
        BrowseLogFileCommand = new RelayCommand(BrowseLogFile);
        BrowseMmprojCommand = new RelayCommand(BrowseMmproj);
        StartServerCommand = new AsyncRelayCommand(StartServerAsync, () => CanStartServer);
        StopServerCommand = new AsyncRelayCommand(StopServerAsync, () => IsServerRunning);
        UnloadModelCommand = new AsyncRelayCommand(UnloadModelAsync, () => IsServerRunning && !_serverService.IsSingleModelMode);
        SaveProfileCommand = new AsyncRelayCommand(SaveProfileAsync);
        LoadProfileCommand = new AsyncRelayCommand(LoadProfileAsync);
        DeleteProfileCommand = new AsyncRelayCommand(DeleteProfileAsync);
        ExportProfileCommand = new AsyncRelayCommand(ExportProfileAsync);
        ImportProfileCommand = new AsyncRelayCommand(ImportProfileAsync);
        ClearLogCommand = new RelayCommand(ClearLog);

        LoadProfiles();
        _logService.AppLog("Application started");
        UpdateCurrentCommand();
    }

    public void ApplyAppSettings(AppSettings settings)
    {
        var language = string.IsNullOrEmpty(settings.Language) ? "en" : settings.Language;
        
        // Force language change even if value is the same
        _selectedLanguage = language;
        ChangeLanguage(language);
        OnPropertyChanged(nameof(SelectedLanguage));
        
        ProfileNameInput = settings.ProfileNameInput;
        ExecutablePath = settings.ExecutablePath;
        ModelPath = settings.ModelPath;
        ModelsDir = settings.ModelsDir;
        Host = settings.Host;
        Port = settings.Port;
        ContextSize = settings.ContextSize;
        Threads = settings.Threads;
        GpuLayers = settings.GpuLayers;
        Temperature = settings.Temperature;
        MaxTokens = settings.MaxTokens;
        BatchSize = settings.BatchSize;
        UBatchSize = settings.UBatchSize;
        MinP = settings.MinP;
        MmprojPath = settings.MmprojPath;
        CacheTypeK = settings.CacheTypeK;
        CacheTypeV = settings.CacheTypeV;
        TopK = settings.TopK;
        TopP = settings.TopP;
        RepeatPenalty = settings.RepeatPenalty;
        FlashAttention = settings.FlashAttention;
        EnableWebUI = settings.EnableWebUI;
        EmbeddingMode = settings.EmbeddingMode;
        EnableSlots = settings.EnableSlots;
        EnableMetrics = settings.EnableMetrics;
        ApiKey = settings.ApiKey;
        LogFilePath = settings.LogFilePath;
        VerboseLogging = settings.VerboseLogging;
        CustomArguments = settings.CustomArguments;
        AutoRestart = settings.AutoRestart;
        AutoScroll = settings.AutoScrollLog;
    }

    public AppSettings GetAppSettings()
    {
        return new AppSettings
        {
            Language = SelectedLanguage,
            ProfileNameInput = ProfileNameInput,
            ExecutablePath = ExecutablePath,
            ModelPath = ModelPath,
            ModelsDir = ModelsDir,
            Host = Host,
            Port = Port,
            ContextSize = ContextSize,
            Threads = Threads,
            GpuLayers = GpuLayers,
            Temperature = Temperature,
            MaxTokens = MaxTokens,
            BatchSize = BatchSize,
            UBatchSize = UBatchSize,
            MinP = MinP,
            MmprojPath = MmprojPath,
            CacheTypeK = CacheTypeK,
            CacheTypeV = CacheTypeV,
            TopK = TopK,
            TopP = TopP,
            RepeatPenalty = RepeatPenalty,
            FlashAttention = FlashAttention,
            EnableWebUI = EnableWebUI,
            EmbeddingMode = EmbeddingMode,
            EnableSlots = EnableSlots,
            EnableMetrics = EnableMetrics,
            ApiKey = ApiKey,
            LogFilePath = LogFilePath,
            VerboseLogging = VerboseLogging,
            CustomArguments = CustomArguments,
            AutoRestart = AutoRestart,
            AutoScrollLog = AutoScroll
        };
    }

    public string ExecutablePath
    {
        get => _executablePath;
        set { _executablePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartServer)); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string ModelPath
    {
        get => _modelPath;
        set { _modelPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartServer)); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string ModelsDir
    {
        get => _modelsDir;
        set { _modelsDir = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartServer)); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string Host
    {
        get => _host;
        set { _host = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public int Port
    {
        get => _port;
        set { _port = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string ContextSize
    {
        get => _contextSize;
        set { _contextSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string Threads
    {
        get => _threads;
        set { _threads = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string GpuLayers
    {
        get => _gpuLayers;
        set { _gpuLayers = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string Temperature
    {
        get => _temperature;
        set { _temperature = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string MaxTokens
    {
        get => _maxTokens;
        set { _maxTokens = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string BatchSize
    {
        get => _batchSize;
        set { _batchSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string UBatchSize
    {
        get => _uBatchSize;
        set { _uBatchSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string MinP
    {
        get => _minP;
        set { _minP = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string MmprojPath
    {
        get => _mmprojPath;
        set { _mmprojPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string CacheTypeK
    {
        get => _cacheTypeK;
        set { _cacheTypeK = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string CacheTypeV
    {
        get => _cacheTypeV;
        set { _cacheTypeV = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string TopK
    {
        get => _topK;
        set { _topK = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string TopP
    {
        get => _topP;
        set { _topP = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string RepeatPenalty
    {
        get => _repeatPenalty;
        set { _repeatPenalty = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public bool? FlashAttention
    {
        get => _flashAttention;
        set { _flashAttention = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public bool? EnableWebUI
    {
        get => _enableWebUI;
        set { _enableWebUI = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public bool? EmbeddingMode
    {
        get => _embeddingMode;
        set { _embeddingMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public bool? EnableSlots
    {
        get => _enableSlots;
        set { _enableSlots = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public bool? EnableMetrics
    {
        get => _enableMetrics;
        set { _enableMetrics = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string ApiKey
    {
        get => _apiKey;
        set { _apiKey = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string LogFilePath
    {
        get => _logFilePath;
        set { _logFilePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public bool VerboseLogging
    {
        get => _verboseLogging;
        set { _verboseLogging = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public bool AutoRestart
    {
        get => _autoRestart;
        set { _autoRestart = value; OnPropertyChanged(); }
    }

    public bool AutoScroll
    {
        get => _autoScroll;
        set { _autoScroll = value; OnPropertyChanged(); }
    }

    public string CustomArguments
    {
        get => _customArguments;
        set { _customArguments = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); UpdateCurrentCommand(); }
    }

    public string SelectedProfile
    {
        get => _selectedProfile;
        set { _selectedProfile = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnsavedChanges)); }
    }

    public bool HasUnsavedChanges
    {
        get
        {
            if (_loadedProfileConfig == null || string.IsNullOrEmpty(_loadedProfileName))
                return false;
            
            var currentConfig = GetCurrentConfig();
            return !ConfigsEqual(_loadedProfileConfig, currentConfig);
        }
    }

    private static bool ConfigsEqual(ServerConfiguration a, ServerConfiguration b)
    {
        return a.ExecutablePath == b.ExecutablePath &&
               a.ModelPath == b.ModelPath &&
               a.ModelsDir == b.ModelsDir &&
               a.Host == b.Host &&
               a.Port == b.Port &&
               a.ContextSize == b.ContextSize &&
               a.Threads == b.Threads &&
               a.GpuLayers == b.GpuLayers &&
               a.Temperature == b.Temperature &&
               a.MaxTokens == b.MaxTokens &&
               a.BatchSize == b.BatchSize &&
               a.UBatchSize == b.UBatchSize &&
               a.MinP == b.MinP &&
               a.MmprojPath == b.MmprojPath &&
               a.CacheTypeK == b.CacheTypeK &&
               a.CacheTypeV == b.CacheTypeV &&
               a.TopK == b.TopK &&
               a.TopP == b.TopP &&
               a.RepeatPenalty == b.RepeatPenalty &&
               a.FlashAttention == b.FlashAttention &&
               a.EnableWebUI == b.EnableWebUI &&
               a.EmbeddingMode == b.EmbeddingMode &&
               a.EnableSlots == b.EnableSlots &&
               a.EnableMetrics == b.EnableMetrics &&
               a.ApiKey == b.ApiKey &&
               a.LogFilePath == b.LogFilePath &&
               a.VerboseLogging == b.VerboseLogging &&
               a.CustomArguments == b.CustomArguments;
    }

    public string ProfileNameInput
    {
        get => _profileNameInput;
        set { _profileNameInput = value; OnPropertyChanged(); }
    }

    public bool IsServerRunning
    {
        get => _isServerRunning;
        set { _isServerRunning = value; OnPropertyChanged(); }
    }

    public string ServerStatus
    {
        get => _serverStatus;
        set { _serverStatus = value; OnPropertyChanged(); }
    }

    public string CurrentLog
    {
        get => _currentLog;
        set { _currentLog = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> Profiles { get; }
    public ObservableCollection<string> LogLines { get; } = new();

    public string LogOutput
    {
        get => _logOutput;
        set { _logOutput = value; OnPropertyChanged(); }
    }

    public string LogText
    {
        get => _logText;
        private set { _logText = value; OnPropertyChanged(); }
    }

    public string CurrentCommand
    {
        get => _currentCommand;
        private set { _currentCommand = value; OnPropertyChanged(); }
    }

    public ICommand BrowseExecutableCommand { get; }
    public ICommand BrowseModelCommand { get; }
    public ICommand BrowseModelsDirCommand { get; }
    public ICommand BrowseLogFileCommand { get; }
    public ICommand BrowseMmprojCommand { get; }
    public ICommand StartServerCommand { get; }
    public ICommand StopServerCommand { get; }
    public ICommand UnloadModelCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand LoadProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand ExportProfileCommand { get; }
    public ICommand ImportProfileCommand { get; }
    public ICommand ClearLogCommand { get; }

    public bool CanStartServer => !string.IsNullOrEmpty(ExecutablePath) && 
                                   (!string.IsNullOrEmpty(ModelPath) || !string.IsNullOrEmpty(ModelsDir));

    private void BrowseExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select llama-server.exe"
        };
        if (dialog.ShowDialog() == true)
        {
            ExecutablePath = dialog.FileName;
        }
    }

    private void UpdateCurrentCommand()
    {
        var config = GetCurrentConfig();
        CurrentCommand = BuildFullCommand(config);
    }

    private void BrowseModel()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Model files (*.gguf)|*.gguf|All files (*.*)|*.*",
            Title = "Select Model File"
        };
        if (dialog.ShowDialog() == true)
        {
            ModelPath = dialog.FileName;
        }
    }

    private void BrowseModelsDir()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Models Directory"
        };
        if (dialog.ShowDialog() == true)
        {
            ModelsDir = dialog.FolderName;
        }
    }

    private void BrowseLogFile()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Log files (*.log)|*.log|All files (*.*)|*.*",
            Title = "Select Log File"
        };
        if (dialog.ShowDialog() == true)
        {
            LogFilePath = dialog.FileName;
        }
    }

    private void BrowseMmproj()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "GGUF files (*.gguf)|*.gguf|All files (*.*)|*.*",
            Title = "Select MMProj File"
        };
        if (dialog.ShowDialog() == true)
        {
            MmprojPath = dialog.FileName;
        }
    }

    private ServerConfiguration GetCurrentConfig()
    {
        return new ServerConfiguration
        {
            ExecutablePath = ExecutablePath,
            ModelPath = ModelPath,
            ModelsDir = ModelsDir,
            Host = Host,
            Port = Port,
            ContextSize = ParseNullableInt(ContextSize),
            Threads = ParseNullableInt(Threads),
            GpuLayers = ParseNullableInt(GpuLayers),
            Temperature = ParseNullableDouble(Temperature),
            MaxTokens = ParseNullableInt(MaxTokens),
           BatchSize = ParseNullableInt(BatchSize),
            UBatchSize = ParseNullableInt(UBatchSize),
            MinP = ParseNullableDouble(MinP),
            MmprojPath = MmprojPath,
            CacheTypeK = CacheTypeK,
            CacheTypeV = CacheTypeV,
            TopK = ParseNullableInt(TopK),
            TopP = ParseNullableDouble(TopP),
            RepeatPenalty = ParseNullableDouble(RepeatPenalty),
            FlashAttention = FlashAttention,
            EnableWebUI = EnableWebUI,
            EmbeddingMode = EmbeddingMode,
            EnableSlots = EnableSlots,
            EnableMetrics = EnableMetrics,
            ApiKey = ApiKey,
            LogFilePath = LogFilePath,
            VerboseLogging = VerboseLogging,
            CustomArguments = CustomArguments
        };
    }

    private void LoadConfigToUI(ServerConfiguration config)
    {
        ExecutablePath = config.ExecutablePath;
        ModelPath = config.ModelPath;
        ModelsDir = config.ModelsDir;
        Host = config.Host;
        Port = config.Port;
        ContextSize = config.ContextSize?.ToString() ?? string.Empty;
        Threads = config.Threads?.ToString() ?? string.Empty;
        GpuLayers = config.GpuLayers?.ToString() ?? string.Empty;
        Temperature = config.Temperature?.ToString() ?? string.Empty;
        MaxTokens = config.MaxTokens?.ToString() ?? string.Empty;
       BatchSize = config.BatchSize?.ToString() ?? string.Empty;
        UBatchSize = config.UBatchSize?.ToString() ?? string.Empty;
        MinP = config.MinP?.ToString() ?? string.Empty;
        MmprojPath = config.MmprojPath;
        CacheTypeK = config.CacheTypeK;
        CacheTypeV = config.CacheTypeV;
        TopK = config.TopK?.ToString() ?? string.Empty;
        TopP = config.TopP?.ToString() ?? string.Empty;
        RepeatPenalty = config.RepeatPenalty?.ToString() ?? string.Empty;
        FlashAttention = config.FlashAttention;
        EnableWebUI = config.EnableWebUI;
        EmbeddingMode = config.EmbeddingMode;
        EnableSlots = config.EnableSlots;
        EnableMetrics = config.EnableMetrics;
        ApiKey = config.ApiKey;
        LogFilePath = config.LogFilePath;
        VerboseLogging = config.VerboseLogging;
        CustomArguments = config.CustomArguments;
    }

    private static int? ParseNullableInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value, out var result) ? result : null;
    }

    private static double? ParseNullableDouble(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return double.TryParse(value, out var result) ? result : null;
    }

    private async Task StartServerAsync()
    {
        try
        {
            var config = GetCurrentConfig();
            await _serverService.StartAsync(config);
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(LocalizedStrings.FailedToStartServer, ex.Message), 
                LocalizedStrings.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task StopServerAsync()
    {
        await _serverService.StopAsync();
    }

    public async Task StopServerIfRunningAsync()
    {
        if (_serverService.IsRunning)
        {
            await _serverService.StopAsync();
        }
    }

    private async Task UnloadModelAsync()
    {
        await _serverService.UnloadModelAsync();
    }

    private void LoadProfiles()
    {
        Profiles.Clear();
        var profiles = _configService.GetAllProfiles();
        foreach (var profile in profiles)
        {
            Profiles.Add(profile.Name);
        }
    }

    private async Task SaveProfileAsync()
    {
        // Use ProfileNameInput first (what user typed in editable ComboBox), then fall back to SelectedProfile
        var name = !string.IsNullOrWhiteSpace(ProfileNameInput) ? ProfileNameInput : SelectedProfile;
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(LocalizedStrings.PleaseEnterProfileName, LocalizedStrings.WarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var config = GetCurrentConfig();
        await _configService.SaveProfileAsync(name, config);
        
        // Clear the input field after saving and refresh profiles list
        ProfileNameInput = string.Empty;
        LoadProfiles();
        
        // Select the newly saved profile and track it
        SelectedProfile = name;
        _loadedProfileName = name;
        _loadedProfileConfig = config;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private async Task LoadProfileAsync()
    {
        var name = SelectedProfile;
        if (string.IsNullOrWhiteSpace(name)) return;

        if (HasUnsavedChanges)
        {
            var result = MessageBox.Show(
                LocalizedStrings.UnsavedChangesMessage,
                LocalizedStrings.UnsavedChangesTitle,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return;
            
            if (result == MessageBoxResult.Yes)
            {
                // Save to the ORIGINAL loaded profile, not the new selection
                if (string.IsNullOrWhiteSpace(_loadedProfileName))
                {
                    // No original profile - ask for a name or use ProfileNameInput
                    var saveName = !string.IsNullOrWhiteSpace(ProfileNameInput) ? ProfileNameInput : name;
                    if (string.IsNullOrWhiteSpace(saveName))
                    {
                        MessageBox.Show(LocalizedStrings.PleaseEnterProfileName, LocalizedStrings.WarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    var config = GetCurrentConfig();
                    await _configService.SaveProfileAsync(saveName, config);
                    ProfileNameInput = string.Empty;
                    LoadProfiles();
                    SelectedProfile = saveName;
                    _loadedProfileName = saveName;
                    _loadedProfileConfig = config;
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    return;
                }

                // Save to existing profile
                var currentConfig = GetCurrentConfig();
                await _configService.SaveProfileAsync(_loadedProfileName, currentConfig);
                
                // Refresh and keep tracking same profile
                LoadProfiles();
                _loadedProfileConfig = currentConfig;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                return; // Done - don't load another profile
            }
            // If "No", continue to load the selected profile
        }

        var loadedConfig = await _configService.LoadProfileAsync(name);
        if (loadedConfig != null)
        {
            LoadConfigToUI(loadedConfig);
            _loadedProfileConfig = loadedConfig;
            _loadedProfileName = name;  // Track which profile is loaded
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private async Task DeleteProfileAsync()
    {
        var name = SelectedProfile;
        if (string.IsNullOrWhiteSpace(name)) return;

        var result = MessageBox.Show(string.Format(LocalizedStrings.GetString("ConfirmDelete"), name), 
                LocalizedStrings.ConfirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            await _configService.DeleteProfileAsync(name);
            LoadProfiles();
        }
    }

    private async Task ExportProfileAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            Title = "Export Profile"
        };
        if (dialog.ShowDialog() == true)
        {
            var config = GetCurrentConfig();
            await _configService.ExportProfileAsync(dialog.FileName, config);
        }
    }

    private async Task ImportProfileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            Title = "Import Profile"
        };
        if (dialog.ShowDialog() == true)
        {
            var config = await _configService.ImportProfileAsync(dialog.FileName);
            if (config != null)
            {
                LoadConfigToUI(config);
            }
        }
    }

    private void ClearLog()
    {
        LogLines.Clear();
        LogText = string.Empty;
    }

    private void OnLogReceived(object? sender, string logLine)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogLines.Add(logLine);
            if (LogLines.Count > 1000)
            {
                LogLines.RemoveAt(0);
            }
            LogText = string.Join("\n", LogLines);
        });
    }

    private void OnServerOutput(object? sender, string output)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogLines.Add(output);
            if (LogLines.Count > 1000)
            {
                LogLines.RemoveAt(0);
            }
            LogText = string.Join("\n", LogLines);
        });
    }

    private bool _isAutoRestarting;

    private async void OnServerStateChanged(object? sender, bool isRunning)
    {
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            IsServerRunning = isRunning;
            ServerStatus = isRunning 
                ? string.Format(Resources.LocalizedStrings.GetString("StatusRunning"), _serverService.ProcessId) 
                : Localized.StatusStopped;

            if (!isRunning && AutoRestart && !_isAutoRestarting && !_serverService.WasStoppedIntentionally)
            {
                _logService.AppLog("Server exited unexpectedly. Auto-restarting...");
                _isAutoRestarting = true;
                await Task.Delay(1000);
                try
                {
                    var config = GetCurrentConfig();
                    await _serverService.StartAsync(config);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(LocalizedStrings.FailedToAutoRestart, ex.Message), 
                        LocalizedStrings.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    _isAutoRestarting = false;
                }
            }
        });
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class LanguageOption
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}