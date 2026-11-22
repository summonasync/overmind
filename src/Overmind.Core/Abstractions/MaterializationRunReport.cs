namespace Overmind.Core;

public sealed record MaterializationRunReport(
    DateTime StartedAt,
    DateTime FinishedAt,
    bool Atomic,
    string[] Written,
    PlanningMaterializationSkip[] Skipped,
    PlanningMaterializationSkip[] Refused,
    PlanningMaterializationSkip[]? Quarantined = null);
