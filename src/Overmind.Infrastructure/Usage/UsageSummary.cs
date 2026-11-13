namespace Overmind.Infrastructure.Usage;

public sealed record UsageSummary(
    Guid ProjectId,
    DateTime? Since,
    DateTime? Until,
    IReadOnlyList<UsageFeatureTotal> Features,
    IReadOnlyList<UsageAgentTotal> Agents);
