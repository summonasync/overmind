namespace Overmind.Host.Api.Contracts;

public sealed record SettingsResponse(
    string EmbeddingProvider,
    string OllamaBaseUrl,
    string OllamaModel,
    string OpenAiBaseUrl,
    bool OpenAiApiKeyConfigured,
    string PlanningMaterializationDefault,
    string PlanningRequireHumanValidationDefault);

public sealed record UpdateSettingsRequest(
    string? EmbeddingProvider,
    string? OllamaBaseUrl,
    string? OllamaModel,
    string? OpenAiBaseUrl,
    string? OpenAiApiKey,
    string? PlanningMaterializationDefault,
    string? PlanningRequireHumanValidationDefault);
