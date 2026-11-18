namespace Overmind.Mcp;

public sealed record RescueVerdictResult(string Verdict, IReadOnlyList<string> Reasons);

public static class RescueVerdict
{
    public const string RescueWorthy = "rescue-worthy";
    public const string Clean = "clean";
    public const string Stale = "stale";
    public const string Unknown = "unknown";

    public static RescueVerdictResult Of(RescueCodeLocation location, RescueGitFacts facts)
    {
        if (facts.NothingMeasured)
        {
            var reason = facts.Error ?? "no git fact could be measured for this candidate";

            return new RescueVerdictResult(location.Measurable ? Unknown : Stale, [reason]);
        }

        var notes = new List<string>();
        if (location.Stale)
        {
            notes.Add($"worktree path {location.WorktreePath} is gone, only the branch is left");
        }

        var signals = new List<string>();
        if (facts.DirtyFiles > 0)
        {
            signals.Add($"{facts.DirtyFiles} uncommitted {(facts.DirtyFiles == 1 ? "file" : "files")} in the worktree");
        }

        if (facts.UnpushedCommits > 0)
        {
            signals.Add(
                $"{facts.UnpushedCommits} unpushed {(facts.UnpushedCommits == 1 ? "commit" : "commits")} over {facts.UnpushedBase}");
        }

        if (facts.Unmerged == true)
        {
            signals.Add($"not merged into {facts.Mainline}");
        }

        return signals.Count > 0
            ? new RescueVerdictResult(RescueWorthy, [.. notes, .. signals])
            : new RescueVerdictResult(Clean, CleanReasons(location, facts));
    }

    private static IReadOnlyList<string> CleanReasons(RescueCodeLocation location, RescueGitFacts facts)
    {
        var reasons = new List<string>();
        if (location.Stale)
        {
            reasons.Add($"worktree path {location.WorktreePath} is gone, but the branch is merged and pushed");
        }

        if (facts.DirtyFiles == 0)
        {
            reasons.Add("nothing uncommitted in the worktree");
        }

        if (facts.UnpushedCommits == 0)
        {
            reasons.Add($"nothing ahead of {facts.UnpushedBase}");
        }

        if (facts.Unmerged == false)
        {
            reasons.Add($"already merged into {facts.Mainline}");
        }

        foreach (var unknown in new[]
                 {
                     facts.DirtyFiles is null ? "worktree state unmeasurable" : null,
                     facts.UnpushedCommits is null ? "unpushed count unknown" : null,
                     facts.Unmerged is null ? "merge state unknown" : null,
                 })
        {
            if (unknown is not null)
            {
                reasons.Add(unknown);
            }
        }

        if (facts.Error is { Length: > 0 } error)
        {
            reasons.Add(error);
        }

        return reasons;
    }
}
