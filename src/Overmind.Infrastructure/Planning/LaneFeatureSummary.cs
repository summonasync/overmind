using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record LaneFeatureSummary(
    string Slug,
    PlanningFeatureStatus Status,
    string? ClaimedByAlias,
    string? ClaimedByVendor,
    AgentSessionLiveness? ClaimState);
