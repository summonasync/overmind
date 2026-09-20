namespace Overmind.Host.Api.Contracts;

public sealed record SettingsResponse(
    string EmbeddingProvider,
    string OllamaBaseUrl,
    string OllamaModel,
    string OpenAiBaseUrl,
    string QdrantEndpoint,
    bool OpenAiApiKeyConfigured,
    string PlanningMaterializationDefault,
    string PlanningRequireHumanValidationDefault);

public sealed record UpdateSettingsResponse(
    string EmbeddingProvider,
    string OllamaBaseUrl,
    string OllamaModel,
    string OpenAiBaseUrl,
    string QdrantEndpoint,
    bool OpenAiApiKeyConfigured,
    string PlanningMaterializationDefault,
    string PlanningRequireHumanValidationDefault,
    bool ReindexRecommended);

public sealed record UpdateSettingsRequest(
    string? EmbeddingProvider,
    string? OllamaBaseUrl,
    string? OllamaModel,
    string? OpenAiBaseUrl,
    string? OpenAiApiKey,
    string? QdrantEndpoint,
    string? PlanningMaterializationDefault,
    string? PlanningRequireHumanValidationDefault);

public sealed record TestSettingsRequest(
    string? Provider,
    string? OllamaBaseUrl,
    string? OllamaModel,
    string? OpenAiBaseUrl,
    string? QdrantEndpoint);
