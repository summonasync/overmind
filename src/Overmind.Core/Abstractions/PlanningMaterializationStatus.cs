namespace Overmind.Core;

public sealed record PlanningMaterializationStatus(
    Guid ProjectId,
    DateTime? LastSuccess,
    DateTime? LastAttempt,
    string? LastAttemptReason,
    MaterializationRunReport? LastRun,
    int TrackedFiles,
    IReadOnlyList<string> StalePaths,
    PlanningMaterialization Strategy,
    string TargetDir);
