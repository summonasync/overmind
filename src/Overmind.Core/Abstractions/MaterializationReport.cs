namespace Overmind.Core;

public sealed record MaterializedFileInfo(AgentPlatform Platform, string RelativePath, string Sha256);

public sealed record MaterializationReport(
    Guid ProjectId,
    Guid KitId,
    IReadOnlyList<MaterializedFileInfo> Files,
    IReadOnlyList<string> Warnings);
