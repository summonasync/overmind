namespace Overmind.Host.Api.Contracts;

public sealed record SettingsResponse(
    string EmbeddingProvider,
    string OllamaBaseUrl,
    string OpenAiBaseUrl,
    bool OpenAiApiKeyConfigured);

public sealed record UpdateSettingsRequest(
    string? EmbeddingProvider,
    string? OllamaBaseUrl,
    string? OpenAiBaseUrl,
    string? OpenAiApiKey);
