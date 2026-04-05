using System.IO;
using System.Text.Json;
using LlamaServerLauncher.Models;

namespace LlamaServerLauncher.Services;

public class ConfigurationService
{
    private readonly string _profilesPath;
    private readonly string _appSettingsPath;
    private readonly LogService _logService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public ConfigurationService(LogService logService)
    {
        _logService = logService;
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LlamaServerLauncher"
        );
        _profilesPath = Path.Combine(appDataPath, "profiles");
        _appSettingsPath = Path.Combine(appDataPath, "app.json");
        Directory.CreateDirectory(_profilesPath);
    }

    public async Task SaveAppSettingsAsync(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(_appSettingsPath, json);
            _logService.Info("Application settings saved successfully");
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to save application settings: {ex.Message}");
        }
    }

    public async Task<AppSettings> LoadAppSettingsAsync()
    {
        try
        {
            if (!File.Exists(_appSettingsPath))
            {
                return new AppSettings();
            }

            var json = await File.ReadAllTextAsync(_appSettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            _logService.Info("Application settings loaded successfully");
            return settings ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to load application settings: {ex.Message}");
            return new AppSettings();
        }
    }

    public async Task SaveProfileAsync(string name, ServerConfiguration config)
    {
        try
        {
            var profile = new ProfileInfo
            {
                Name = name,
                FilePath = GetProfilePath(name),
                LastModified = DateTime.Now,
                Configuration = config
            };

            var json = JsonSerializer.Serialize(profile, JsonOptions);
            var filePath = GetProfilePath(name);
            await File.WriteAllTextAsync(filePath, json);
            _logService.Info($"Profile '{name}' saved successfully");
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to save profile '{name}': {ex.Message}");
            throw;
        }
    }

    public async Task<ServerConfiguration?> LoadProfileAsync(string name)
    {
        try
        {
            var filePath = GetProfilePath(name);
            if (!File.Exists(filePath))
            {
                _logService.Warning($"Profile '{name}' not found");
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var profile = JsonSerializer.Deserialize<ProfileInfo>(json);
            _logService.Info($"Profile '{name}' loaded successfully");
            return profile?.Configuration;
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to load profile '{name}': {ex.Message}");
            return null;
        }
    }

    public List<ProfileInfo> GetAllProfiles()
    {
        var profiles = new List<ProfileInfo>();

        try
        {
            var files = Directory.GetFiles(_profilesPath, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var profile = JsonSerializer.Deserialize<ProfileInfo>(json);
                    if (profile != null)
                    {
                        profile.FilePath = file;
                        profiles.Add(profile);
                    }
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to get profiles: {ex.Message}");
        }

        return profiles.OrderBy(p => p.Name).ToList();
    }

    public async Task DeleteProfileAsync(string name)
    {
        try
        {
            var filePath = GetProfilePath(name);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logService.Info($"Profile '{name}' deleted");
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to delete profile '{name}': {ex.Message}");
            throw;
        }
    }

    public async Task ExportProfileAsync(string filePath, ServerConfiguration config)
    {
        var profile = new ProfileInfo
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            FilePath = filePath,
            LastModified = DateTime.Now,
            Configuration = config
        };

        var json = JsonSerializer.Serialize(profile, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
        _logService.Info($"Profile exported to '{filePath}'");
    }

    public async Task<ServerConfiguration?> ImportProfileAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var profile = JsonSerializer.Deserialize<ProfileInfo>(json);
            _logService.Info($"Profile imported from '{filePath}'");
            return profile?.Configuration;
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to import profile: {ex.Message}");
            return null;
        }
    }

    private string GetProfilePath(string name)
    {
        var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_profilesPath, $"{safeName}.json");
    }
}