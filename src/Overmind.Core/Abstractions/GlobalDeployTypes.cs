namespace Overmind.Core;

public enum GlobalDeployEntryKind
{
    File,
    MarkerBlock,
}

public enum GlobalDeployOutcome
{
    Written,
    Skipped,
    Refused,
}

public sealed record GlobalDeployEntry(
    GlobalDeployEntryKind Kind,
    AgentPlatform Platform,
    string Path,
    GlobalDeployOutcome Outcome,
    string? Reason = null);

public sealed record GlobalDeployReport(
    Guid KitId,
    string Agent,
    DateTime StartedAt,
    DateTime FinishedAt,
    GlobalDeployEntry[] Entries,
    string[] Warnings)
{
    public bool Success { get; } = Entries.All(e => e.Outcome != GlobalDeployOutcome.Refused);
}

public enum GlobalDriftKind
{
    Missing,
    Modified,
    MarkerMissing,
    MarkerModified,
}

public sealed record GlobalDriftItem(
    Guid KitId,
    AgentPlatform Platform,
    string Path,
    GlobalDriftKind Kind);
