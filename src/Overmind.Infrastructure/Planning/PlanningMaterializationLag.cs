namespace Overmind.Infrastructure.Planning;

public sealed record PlanningMaterializationLag(
    int PendingMutations,
    DateTime? LastAttemptAt,
    DateTime? LastSuccessAt,
    bool Stale);
