namespace Overmind.Host.Api.Contracts;

public sealed record IngestUsageRequest(
    string Alias,
    Guid SessionId,
    DateTime Ts,
    string Model,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens = 0,
    long CacheWriteTokens = 0);

public sealed record IngestUsageResponse(Guid Id);

public sealed record UsageSummaryResponse(
    Guid ProjectId,
    DateTime? Since,
    DateTime? Until,
    IReadOnlyList<UsageFeatureSummaryRow> Features,
    IReadOnlyList<UsageAgentSummaryRow> Agents);

public sealed record UsageFeatureSummaryRow(
    Guid FeatureId,
    string Slug,
    long Records,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    decimal Cost);

public sealed record UsageAgentSummaryRow(
    string Alias,
    long Records,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    decimal Cost);
