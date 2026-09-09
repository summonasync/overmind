namespace Overmind.Core;

public sealed record PlanningMaterializedFileInfo(string RelativePath, string Sha256);

public sealed record PlanningMaterializationReport(
    Guid ProjectId,
    PlanningMaterialization Strategy,
    IReadOnlyList<PlanningMaterializedFileInfo> Files,
    IReadOnlyList<string> Warnings);
