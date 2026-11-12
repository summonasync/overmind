namespace Overmind.Infrastructure.Usage;

public sealed record UsageFeatureTotal(
    Guid FeatureId,
    string Slug,
    long Records,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    decimal Cost);
