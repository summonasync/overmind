using System.Text.RegularExpressions;

namespace Overmind.Mcp;

public sealed partial class RescueGitScanner(IGitCli git)
{
    private const string MainlineFallback = "origin/main";

    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_./-]*$")]
    private static partial Regex SafeRefRegex { get; }

    public async Task<RescueGitFacts> ScanAsync(
        RescueCodeLocation location,
        string workspace,
        CancellationToken ct)
    {
        if (!location.Measurable)
        {
            return Unmeasurable("no branch or worktree of this candidate exists in the scanned repository");
        }

        if (location.Branch is { } branch && !SafeRefRegex.IsMatch(branch))
        {
            return Unmeasurable($"refused to query '{branch}': not a safe git ref name");
        }

        var failures = new List<string>();
        var runIn = location.RunIn(workspace);
        var target = location.Branch ?? "HEAD";

        var workingTree = location.WorktreeExists
            ? await WorkingTreeAsync(runIn, failures, ct)
            : (Dirty: (int?)null, Untracked: (int?)null);
        var unpushed = await UnpushedAsync(runIn, target, failures, ct);
        var merged = await MergedAsync(runIn, target, failures, ct);
        var lastCommit = await LastCommitAsync(runIn, target, failures, ct);

        return new RescueGitFacts(
            workingTree.Dirty,
            workingTree.Untracked,
            unpushed.Count,
            unpushed.Base,
            merged.Unmerged,
            merged.Mainline,
            lastCommit?.Hash,
            lastCommit?.Date,
            failures.Count == 0 ? null : string.Join("; ", failures));
    }

    private async Task<(int? Dirty, int? Untracked)> WorkingTreeAsync(
        string runIn,
        List<string> failures,
        CancellationToken ct)
    {
        var status = await git.RunAsync(runIn, "status --porcelain", ct);
        if (status.ExitCode != 0)
        {
            failures.Add(Describe("status --porcelain", status));
            return (null, null);
        }

        var lines = Lines(status.StandardOutput);

        return (lines.Count, lines.Count(line => line.StartsWith("??", StringComparison.Ordinal)));
    }

    private async Task<(int? Count, string? Base)> UnpushedAsync(
        string runIn,
        string target,
        List<string> failures,
        CancellationToken ct)
    {
        var baseRef = await UpstreamAsync(runIn, target, ct) ?? MainlineFallback;
        if (!await RefExistsAsync(runIn, baseRef, ct))
        {
            failures.Add($"{baseRef} is not present locally, so unpushed work cannot be counted");
            return (null, null);
        }

        var count = await git.RunAsync(runIn, $"rev-list --count {baseRef}..{target}", ct);
        if (count.ExitCode != 0)
        {
            failures.Add(Describe($"rev-list --count {baseRef}..{target}", count));
            return (null, baseRef);
        }

        return (int.TryParse(count.StandardOutput.Trim(), out var value) ? value : null, baseRef);
    }

    private async Task<string?> UpstreamAsync(string runIn, string target, CancellationToken ct)
    {
        var upstream = await git.RunAsync(
            runIn,
            $"rev-parse --abbrev-ref --symbolic-full-name {target}@{{upstream}}",
            ct);
        var name = upstream.StandardOutput.Trim();

        return upstream.ExitCode == 0 && name.Length > 0 && name != "HEAD" && SafeRefRegex.IsMatch(name)
            ? name
            : null;
    }

    private async Task<(bool? Unmerged, string? Mainline)> MergedAsync(
        string runIn,
        string target,
        List<string> failures,
        CancellationToken ct)
    {
        var mainline = await MainlineAsync(runIn, ct);
        if (mainline is null)
        {
            failures.Add("no origin/main, main or master ref locally, so the merged flag is unknown");
            return (null, null);
        }

        var ancestor = await git.RunAsync(runIn, $"merge-base --is-ancestor {target} {mainline}", ct);
        if (ancestor.ExitCode is 0 or 1)
        {
            return (ancestor.ExitCode == 1, mainline);
        }

        failures.Add(Describe($"merge-base --is-ancestor {target} {mainline}", ancestor));
        return (null, mainline);
    }

    private async Task<string?> MainlineAsync(string runIn, CancellationToken ct)
    {
        foreach (var candidate in new[] { MainlineFallback, "main", "master" })
        {
            if (await RefExistsAsync(runIn, candidate, ct))
            {
                return candidate;
            }
        }

        return null;
    }

    private async Task<bool> RefExistsAsync(string runIn, string name, CancellationToken ct) =>
        SafeRefRegex.IsMatch(name)
        && (await git.RunAsync(runIn, $"rev-parse --verify --quiet {name}", ct)).ExitCode == 0;

    private async Task<(string Hash, string Date)?> LastCommitAsync(
        string runIn,
        string target,
        List<string> failures,
        CancellationToken ct)
    {
        var log = await git.RunAsync(runIn, $"log -1 --format=%h%x09%cI {target}", ct);
        if (log.ExitCode != 0)
        {
            failures.Add(Describe($"log -1 {target}", log));
            return null;
        }

        var parts = log.StandardOutput.TrimEnd().Split('\t');

        return parts.Length == 2 ? (parts[0], parts[1]) : null;
    }

    private static RescueGitFacts Unmeasurable(string reason) =>
        new(null, null, null, null, null, null, null, null, reason);

    private static string Describe(string arguments, GitCommandResult result)
    {
        var detail = result.StandardError.Trim();
        if (detail.Length == 0)
        {
            detail = result.StandardOutput.Trim();
        }

        return $"git {arguments} failed (exit {result.ExitCode}): {Clip(detail)}";
    }

    private static string Clip(string value) => value.Length <= 160 ? value : value[..160];

    private static List<string> Lines(string output) =>
        [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
