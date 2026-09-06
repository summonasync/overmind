namespace Overmind.Core;

public enum DriftKind
{
    Missing,
    Modified
}

public sealed record DriftItem(
    Guid ProjectId,
    Guid KitId,
    AgentPlatform Platform,
    string RelativePath,
    DriftKind Kind);
