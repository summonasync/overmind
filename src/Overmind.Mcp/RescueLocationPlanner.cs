namespace Overmind.Mcp;

public sealed record RescueCodeLocation(
    string? WorktreePath,
    bool WorktreeExists,
    string? Branch,
    bool BranchExists,
    bool Stale,
    string Source)
{
    public const string SourcePin = "pin";
    public const string SourceDiscovery = "discovery";
    public const string SourceNone = "none";

    public bool Measurable => WorktreeExists || BranchExists;

    public string RunIn(string workspace) => WorktreeExists ? WorktreePath! : workspace;
}

public static class RescueLocationPlanner
{
    public static RescueCodeLocation Locate(
        string slug,
        string? pinnedWorktreePath,
        string? pinnedBranch,
        IReadOnlyList<GitWorktree> worktrees,
        IReadOnlyCollection<string> branches)
    {
        var path = Trim(pinnedWorktreePath);
        var branch = Trim(pinnedBranch);

        if (path is not null || branch is not null)
        {
            var worktree = path is not null
                ? worktrees.FirstOrDefault(entry => SamePath(entry.Path, path))
                : worktrees.FirstOrDefault(entry => string.Equals(entry.Branch, branch, StringComparison.Ordinal));

            return Describe(path ?? worktree?.Path, branch ?? worktree?.Branch, branches, RescueCodeLocation.SourcePin);
        }

        var matched = FeatureCodeMatcher.Match(slug, Candidates(worktrees, branches));
        var hit = matched.FirstOrDefault(entry => entry.Kind == FeatureCodeCandidate.WorktreeKind)
            ?? matched.FirstOrDefault(entry => entry.Kind == FeatureCodeCandidate.BranchKind);

        return hit is null
            ? new RescueCodeLocation(null, false, null, false, false, RescueCodeLocation.SourceNone)
            : Describe(hit.Path, hit.Ref, branches, RescueCodeLocation.SourceDiscovery);
    }

    private static RescueCodeLocation Describe(
        string? path,
        string? branch,
        IReadOnlyCollection<string> branches,
        string source)
    {
        var exists = path is not null && Directory.Exists(path);

        return new RescueCodeLocation(
            path,
            exists,
            branch,
            branch is not null && branches.Contains(branch, StringComparer.Ordinal),
            path is not null && !exists,
            source);
    }

    private static IReadOnlyList<FeatureCodeCandidate> Candidates(
        IReadOnlyList<GitWorktree> worktrees,
        IReadOnlyCollection<string> branches)
    {
        var candidates = new List<FeatureCodeCandidate>(worktrees.Count + branches.Count);

        foreach (var worktree in worktrees)
        {
            if (worktree.IsBare)
            {
                continue;
            }

            var leaf = Leaf(worktree.Path);
            string[] names = worktree.Branch is null || worktree.Branch == leaf ? [leaf] : [worktree.Branch, leaf];

            candidates.Add(new FeatureCodeCandidate(
                FeatureCodeCandidate.WorktreeKind,
                worktree.Branch ?? leaf,
                names,
                worktree.Path,
                worktree.Branch,
                worktree.Head,
                !Directory.Exists(worktree.Path)));
        }

        foreach (var name in branches)
        {
            candidates.Add(new FeatureCodeCandidate(
                FeatureCodeCandidate.BranchKind,
                name,
                [name],
                null,
                name,
                null,
                false));
        }

        return candidates;
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            StringComparison.OrdinalIgnoreCase);

    private static string Leaf(string path) =>
        Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
