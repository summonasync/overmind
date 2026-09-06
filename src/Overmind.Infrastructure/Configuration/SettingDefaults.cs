namespace Overmind.Infrastructure.Configuration;

public static class SettingDefaults
{
    public const string EmbeddingProvider = "onnx";
    public const string EmbeddingOllamaBaseUrl = "http://127.0.0.1:11434";
    public const string EmbeddingOpenAiBaseUrl = "https://api.openai.com";
    public const string QdrantEndpoint = "http://127.0.0.1:6334";

    public static readonly string[] EmbeddingProviders = ["onnx", "ollama", "openai"];
}
