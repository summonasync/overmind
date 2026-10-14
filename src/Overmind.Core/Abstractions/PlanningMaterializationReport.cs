namespace Overmind.Core;

public sealed record PlanningMaterializedFileInfo(string RelativePath, string Sha256);

public sealed record PlanningMaterializationSkip(string RelativePath, string Reason);

public sealed record PlanningMaterializationReport(
    Guid ProjectId,
    PlanningMaterialization Strategy,
    IReadOnlyList<PlanningMaterializedFileInfo> Files,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<PlanningMaterializationSkip> Skipped,
    IReadOnlyList<PlanningMaterializationSkip>? Refused = null,
    bool Atomic = true,
    DateTime StartedAt = default,
    DateTime FinishedAt = default,
    PlanningGitHygieneOutcome? GitHygiene = null);
