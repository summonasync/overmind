namespace Overmind.Infrastructure.Usage;

public sealed record UsageAgentTotal(
    string Alias,
    long Records,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    decimal Cost);
