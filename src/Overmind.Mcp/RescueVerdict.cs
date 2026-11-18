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

            return new RescueVerdictResult(
                location.Measurable ? Unknown : Stale,
                [.. LostWorktree(location), reason]);
        }

        var notes = new List<string>(LostWorktree(location));

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
            : VerdictWithoutSignals(location, facts, notes);
    }

    private static RescueVerdictResult VerdictWithoutSignals(
        RescueCodeLocation location,
        RescueGitFacts facts,
        List<string> notes)
    {
        // Nothing asked for a rescue. That is only evidence when at least one signal was measured:
        // three nulls plus a working `log -1` mean git never answered the question, not that the
        // work is safe. Claiming clean here is how a ddcc3f5-shaped loss gets labelled "nothing to save".
        if (facts.NoCleanSignal)
        {
            var gaps = Unmeasured(facts);
            if (facts.Error is { Length: > 0 } error)
            {
                gaps.Add(error);
            }

            return new RescueVerdictResult(Unknown, [.. notes, .. gaps]);
        }

        return new RescueVerdictResult(Clean, CleanReasons(location, facts));
    }

    private static IReadOnlyList<string> CleanReasons(RescueCodeLocation location, RescueGitFacts facts)
    {
        var reasons = new List<string>();
        if (WorktreeLost(location))
        {
            reasons.Add(facts.UnpushedCommits == 0 && facts.Unmerged == false
                ? $"worktree path {location.WorktreePath} is gone, but the branch is merged and pushed"
                : LostWorktreeNote(location));
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

        reasons.AddRange(Unmeasured(facts));

        if (facts.Error is { Length: > 0 } error)
        {
            reasons.Add(error);
        }

        return reasons;
    }

    private static IEnumerable<string> LostWorktree(RescueCodeLocation location) =>
        WorktreeLost(location) ? [LostWorktreeNote(location)] : [];

    private static bool WorktreeLost(RescueCodeLocation location) =>
        location is { Stale: true, BranchExists: true };

    private static string LostWorktreeNote(RescueCodeLocation location) =>
        $"worktree path {location.WorktreePath} is gone, only the branch is left";

    private static List<string> Unmeasured(RescueGitFacts facts)
    {
        var gaps = new List<string>();
        if (facts.DirtyFiles is null)
        {
            gaps.Add("worktree state unmeasurable");
        }

        if (facts.UnpushedCommits is null)
        {
            gaps.Add("unpushed count unknown");
        }

        if (facts.Unmerged is null)
        {
            gaps.Add("merge state unknown");
        }

        return gaps;
    }
}
