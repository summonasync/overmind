namespace Overmind.Mcp;

public sealed record FeatureCodeCandidate(
    string Kind,
    string Name,
    IReadOnlyList<string> Names,
    string? Path,
    string? Ref,
    string? HeadSha,
    bool Stale,
    bool Pinned = false)
{
    public const string WorktreeKind = "worktree";
    public const string BranchKind = "branch";
}
