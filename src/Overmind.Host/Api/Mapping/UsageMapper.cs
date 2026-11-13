using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Usage;

namespace Overmind.Host.Api.Mapping;

public static class UsageMapper
{
    public static UsageSummaryResponse ToResponse(UsageSummary summary) => new(
        summary.ProjectId,
        summary.Since,
        summary.Until,
        summary.Features.Select(ToRow).ToList(),
        summary.Agents.Select(ToRow).ToList());

    private static UsageFeatureSummaryRow ToRow(UsageFeatureTotal total) => new(
        total.FeatureId,
        total.Slug,
        total.Records,
        total.InputTokens,
        total.OutputTokens,
        total.CacheReadTokens,
        total.CacheWriteTokens,
        total.Cost);

    private static UsageAgentSummaryRow ToRow(UsageAgentTotal total) => new(
        total.Alias,
        total.Records,
        total.InputTokens,
        total.OutputTokens,
        total.CacheReadTokens,
        total.CacheWriteTokens,
        total.Cost);
}
