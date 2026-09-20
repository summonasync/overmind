namespace Overmind.Infrastructure.VectorSearch;

public sealed record VectorSearchCandidate(
    string? Provider,
    string? OllamaBaseUrl,
    string? OllamaModel,
    string? OpenAiBaseUrl,
    string? QdrantEndpoint);
