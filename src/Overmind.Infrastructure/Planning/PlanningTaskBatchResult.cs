using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record PlanningTaskBatchResult(
    IReadOnlyList<PlanningArtifact> Artifacts,
    int Created,
    int Updated,
    IReadOnlyList<IReadOnlyList<int>> BlockedOrders);
