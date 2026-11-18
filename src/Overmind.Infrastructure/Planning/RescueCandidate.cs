using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record RescueCandidate(
    string Slug,
    string Title,
    PlanningFeatureStatus Status,
    AgentSessionLiveness ClaimState,
    string ClaimedByAlias,
    string ClaimedByVendor,
    DateTime ClaimedAt,
    bool ClaimedByHuman,
    string? PinnedBranch,
    string? PinnedWorktreePath,
    DateTime UpdatedAt);
