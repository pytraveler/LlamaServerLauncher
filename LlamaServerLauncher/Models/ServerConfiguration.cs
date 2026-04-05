using System.Text.Json.Serialization;

namespace LlamaServerLauncher.Models;

public class ServerConfiguration
{
    [JsonPropertyName("executablePath")]
    public string ExecutablePath { get; set; } = string.Empty;

    [JsonPropertyName("modelPath")]
    public string ModelPath { get; set; } = string.Empty;

    [JsonPropertyName("modelsDir")]
    public string ModelsDir { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 8080;

    [JsonPropertyName("contextSize")]
    public int? ContextSize { get; set; }

    [JsonPropertyName("threads")]
    public int? Threads { get; set; }

    [JsonPropertyName("gpuLayers")]
    public int? GpuLayers { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("maxTokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("batchSize")]
    public int? BatchSize { get; set; }

    [JsonPropertyName("topK")]
    public int? TopK { get; set; }

    [JsonPropertyName("topP")]
    public double? TopP { get; set; }

    [JsonPropertyName("repeatPenalty")]
    public double? RepeatPenalty { get; set; }

    [JsonPropertyName("flashAttention")]
    public bool? FlashAttention { get; set; }

    [JsonPropertyName("enableWebUI")]
    public bool? EnableWebUI { get; set; }

    [JsonPropertyName("embeddingMode")]
    public bool? EmbeddingMode { get; set; }

    [JsonPropertyName("enableSlots")]
    public bool? EnableSlots { get; set; }

    [JsonPropertyName("enableMetrics")]
    public bool? EnableMetrics { get; set; }

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("logFilePath")]
    public string LogFilePath { get; set; } = string.Empty;

    [JsonPropertyName("verboseLogging")]
    public bool VerboseLogging { get; set; }

    [JsonPropertyName("customArguments")]
    public string CustomArguments { get; set; } = string.Empty;

    public ServerConfiguration Clone()
    {
        return new ServerConfiguration
        {
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
            CustomArguments = CustomArguments
        };
    }
}