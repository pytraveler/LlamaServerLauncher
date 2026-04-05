using System.Diagnostics;
using System.Net.Http;
using System.Text;
using LlamaServerLauncher.Models;

namespace LlamaServerLauncher.Services;

public class LlamaServerService : ILlamaServerService, IDisposable
{
    private Process? _process;
    private readonly LogService _logService;
    private ServerConfiguration? _currentConfig;
    private bool _disposed;

    public bool IsRunning => _process != null && !_process.HasExited;
    public int? ProcessId => _process?.Id;
    public string BaseUrl => _currentConfig != null 
        ? $"http://{_currentConfig.Host}:{_currentConfig.Port}" 
        : "http://127.0.0.1:8080";

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<bool>? ServerStateChanged;

    public LlamaServerService(LogService logService)
    {
        _logService = logService;
    }

    public async Task StartAsync(ServerConfiguration config)
    {
        if (IsRunning)
        {
            _logService.Warning("Server is already running");
            return;
        }

        if (string.IsNullOrEmpty(config.ExecutablePath))
        {
            throw new InvalidOperationException("Executable path is not set");
        }

        if (string.IsNullOrEmpty(config.ModelPath) && string.IsNullOrEmpty(config.ModelsDir))
        {
            throw new InvalidOperationException("Model path or models directory must be specified");
        }

        _currentConfig = config;

        var startInfo = new ProcessStartInfo
        {
            FileName = config.ExecutablePath,
            Arguments = CommandLineBuilder.Build(config),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        _logService.Info($"Starting server: {config.ExecutablePath} {startInfo.Arguments}");

        try
        {
            _process = new Process { StartInfo = startInfo };
            _process.OutputDataReceived += OnOutputDataReceived;
            _process.ErrorDataReceived += OnErrorDataReceived;
            _process.EnableRaisingEvents = true;
            _process.Exited += OnProcessExited;

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _logService.Info($"Server started with PID: {_process.Id}");
            ServerStateChanged?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to start server: {ex.Message}");
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            _logService.Warning("Server is not running");
            return;
        }

        try
        {
            _logService.Info("Stopping server...");

            if (_process != null && !_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }

            _logService.Info("Server stopped");
            ServerStateChanged?.Invoke(this, false);
        }
        catch (Exception ex)
        {
            _logService.Error($"Error stopping server: {ex.Message}");
        }
        finally
        {
            _process?.Dispose();
            _process = null;
        }
    }

    public async Task UnloadModelAsync()
    {
        if (!IsRunning)
        {
            _logService.Warning("Cannot unload model: server is not running");
            return;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.PostAsync($"{BaseUrl}/v1/model/unload", null);
            if (response.IsSuccessStatusCode)
            {
                _logService.Info("Model unloaded successfully");
            }
            else
            {
                _logService.Warning($"Failed to unload model: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"Error unloading model: {ex.Message}");
        }
    }

    public async Task<string?> GetCurrentModelAsync()
    {
        if (!IsRunning)
        {
            return null;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.GetAsync($"{BaseUrl}/v1/models");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return json;
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"Error getting current model: {ex.Message}");
        }

        return null;
    }

    public async Task<string?> GetSlotsStatusAsync()
    {
        if (!IsRunning)
        {
            return null;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.GetAsync($"{BaseUrl}/slots");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"Error getting slots status: {ex.Message}");
        }

        return null;
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            OutputReceived?.Invoke(this, e.Data);
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            OutputReceived?.Invoke(this, $"[ERR] {e.Data}");
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        _logService.Info("Server process exited");
        ServerStateChanged?.Invoke(this, false);
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (IsRunning)
        {
            StopAsync().Wait();
        }

        _process?.Dispose();
        _disposed = true;
    }
}