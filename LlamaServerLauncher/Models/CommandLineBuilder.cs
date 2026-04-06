using System.Text;

namespace LlamaServerLauncher.Models;

public static class CommandLineBuilder
{
    public static string Build(ServerConfiguration config)
    {
        var args = new List<string>();

        if (!string.IsNullOrEmpty(config.ModelPath))
            args.Add($"-m \"{EscapePath(config.ModelPath)}\"");
        else if (!string.IsNullOrEmpty(config.ModelsDir))
            args.Add($"--models-dir \"{EscapePath(config.ModelsDir)}\"");

        AddIfSet(args, "--host", config.Host);
        AddIfSet(args, "--port", config.Port.ToString());
        AddIfSet(args, "-c", config.ContextSize?.ToString());
        AddIfSet(args, "-t", config.Threads?.ToString());
        AddIfSet(args, "-ngl", config.GpuLayers?.ToString());
        AddIfSet(args, "--temp", config.Temperature?.ToString());
        AddIfSet(args, "-n", config.MaxTokens?.ToString());
        AddIfSet(args, "-b", config.BatchSize?.ToString());
        AddIfSet(args, "--top-k", config.TopK?.ToString());
        AddIfSet(args, "--top-p", config.TopP?.ToString());
        AddIfSet(args, "--repeat-penalty", config.RepeatPenalty?.ToString());

        AddBoolFlag(args, "-fa", config.FlashAttention);

        // WebUI is enabled by default, so only add --no-webui when explicitly disabled
        if (config.EnableWebUI == false)
            args.Add("--no-webui");
        AddBoolFlag(args, "--embedding", config.EmbeddingMode);
        AddBoolFlag(args, "--slots", config.EnableSlots);
        AddBoolFlag(args, "--metrics", config.EnableMetrics);

        if (!string.IsNullOrEmpty(config.ApiKey))
            args.Add($"--api-key \"{config.ApiKey}\"");
        if (!string.IsNullOrEmpty(config.LogFilePath))
            args.Add($"--log-file \"{config.LogFilePath}\"");
        if (config.VerboseLogging)
            args.Add("-v");

        if (!string.IsNullOrEmpty(config.CustomArguments))
            args.Add(config.CustomArguments);

        return string.Join(" ", args);
    }

    private static void AddIfSet(List<string> args, string flag, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            args.Add($"{flag} {value}");
        }
    }

    private static void AddBoolFlag(List<string> args, string flag, bool? value)
    {
        if (value.HasValue)
        {
            args.Add($"{flag} {(value.Value ? "on" : "off")}");
        }
    }

    private static string EscapePath(string path)
    {
        // Escape backslashes to prevent them from escaping quotes in the final command line
        return path.Replace("\\", "\\\\");
    }

    public static string BuildFullCommand(ServerConfiguration config)
    {
        var args = Build(config);
        return $"\"{config.ExecutablePath}\" {args}";
    }
}