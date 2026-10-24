namespace Overmind.Infrastructure.VectorSearch;

public sealed record CollectionRebuildReport(
    EmbeddingFingerprint? Previous,
    EmbeddingFingerprint Seeded,
    ulong DroppedPoints,
    bool Existed);
