namespace Overmind.Core;

public sealed record VectorSearchHealthReport(
    bool Up,
    QdrantHealthLeg Qdrant,
    EmbeddingHealthLeg Embedding);

public sealed record QdrantHealthLeg(bool Up, string Endpoint, string? Error);

public sealed record EmbeddingHealthLeg(
    bool Up,
    string Provider,
    string Model,
    int? Dimensions,
    string? Error);
