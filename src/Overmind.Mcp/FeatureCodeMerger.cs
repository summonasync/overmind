namespace Overmind.Mcp;

public static class FeatureCodeMerger
{
    public static IReadOnlyList<FeatureCodeCandidate> Merge(
        string slug,
        IReadOnlyList<FeatureCodeCandidate> candidates,
        string? pinnedBranch,
        string? pinnedWorktreePath)
    {
        pinnedBranch = NormalizeBranchRef(pinnedBranch);
        if (pinnedBranch is null && pinnedWorktreePath is null)
        {
            return FeatureCodeMatcher.Match(slug, candidates);
        }

        var pinned = new List<FeatureCodeCandidate>();
        foreach (var candidate in candidates)
        {
            if (MatchesPin(candidate, pinnedBranch, pinnedWorktreePath))
            {
                pinned.Add(candidate with { Pinned = true });
            }
        }

        var merged = new List<FeatureCodeCandidate>(pinned);
        if (pinnedBranch is not null && pinned.All(match => match.Ref != pinnedBranch))
        {
            merged.Add(StaleBranchPin(pinnedBranch));
        }

        if (pinnedWorktreePath is not null && pinned.All(match => !SamePath(match.Path, pinnedWorktreePath)))
        {
            merged.Add(StaleWorktreePin(pinnedWorktreePath));
        }

        var pinnedKeys = pinned.Select(match => (match.Kind, match.Path, match.Ref)).ToHashSet();
        foreach (var match in FeatureCodeMatcher.Match(slug, candidates))
        {
            if (!pinnedKeys.Contains((match.Kind, match.Path, match.Ref)))
            {
                merged.Add(match);
            }
        }

        return merged;
    }

    private static string? NormalizeBranchRef(string? branch) =>
        branch is not null && branch.StartsWith("refs/heads/", StringComparison.Ordinal)
            ? branch["refs/heads/".Length..]
            : branch;

    private static bool MatchesPin(FeatureCodeCandidate candidate, string? pinnedBranch, string? pinnedWorktreePath) =>
        (pinnedBranch is not null && candidate.Ref == pinnedBranch)
        || (pinnedWorktreePath is not null
            && candidate.Kind == FeatureCodeCandidate.WorktreeKind
            && SamePath(candidate.Path, pinnedWorktreePath));

    private static bool SamePath(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(
            left.Replace('\\', '/').TrimEnd('/'),
            right.Replace('\\', '/').TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static FeatureCodeCandidate StaleBranchPin(string branch) =>
        new(FeatureCodeCandidate.BranchKind, branch, [branch], null, null, null, Stale: true, Pinned: true);

    private static FeatureCodeCandidate StaleWorktreePin(string worktreePath)
    {
        var leaf = LeafOf(worktreePath);
        return new(FeatureCodeCandidate.WorktreeKind, leaf, [leaf], worktreePath, null, null, Stale: true, Pinned: true);
    }

    private static string LeafOf(string path) =>
        Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
}
