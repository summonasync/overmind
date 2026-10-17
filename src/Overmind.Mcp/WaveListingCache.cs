using ModelContextProtocol;

namespace Overmind.Mcp;

public sealed record WaveListing(
    string Workspace,
    DateTime FetchedAt,
    IReadOnlyList<FeatureCodeCandidate> Candidates,
    IReadOnlyDictionary<string, GitBranchHead> Heads);

public sealed class WaveListingCache
{
    private const string BranchListArguments =
        "for-each-ref --format=%(refname:short)%09%(objectname:short)%09%(committerdate:iso8601-strict) refs/heads";

    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(15);

    private readonly object _lock = new();
    private WaveListing? _listing;

    public async Task<WaveListing> GetAsync(string workspace, IGitCli git, CancellationToken ct)
    {
        lock (_lock)
        {
            if (_listing is { } cached &&
                DateTime.UtcNow - cached.FetchedAt < Lifetime &&
                SameWorkspace(cached.Workspace, workspace))
            {
                return cached;
            }
        }

        var fresh = await FetchAsync(workspace, git, ct);
        lock (_lock)
        {
            _listing = fresh;
        }

        return fresh;
    }

    private static async Task<WaveListing> FetchAsync(string workspace, IGitCli git, CancellationToken ct)
    {
        var listing = await RunAsync(git, workspace, "worktree list --porcelain", ct);
        var branchList = await RunAsync(git, workspace, BranchListArguments, ct);
        var heads = GitBranchListParser.Parse(branchList.StandardOutput)
            .ToDictionary(head => head.Name, StringComparer.Ordinal);

        var candidates = new List<FeatureCodeCandidate>();
        candidates.AddRange(GitWorktreeListParser.Parse(listing.StandardOutput).Select(WorktreeCandidate));
        candidates.AddRange(heads.Values.Select(head =>
            new FeatureCodeCandidate(
                FeatureCodeCandidate.BranchKind, head.Name, [head.Name], null, head.Name, null, false)));

        return new WaveListing(workspace, DateTime.UtcNow, candidates, heads);
    }

    private static FeatureCodeCandidate WorktreeCandidate(GitWorktree worktree)
    {
        var leaf = Path.GetFileName(worktree.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string[] names = worktree.Branch is null || worktree.Branch == leaf
            ? [leaf]
            : [worktree.Branch, leaf];

        return new FeatureCodeCandidate(
            FeatureCodeCandidate.WorktreeKind,
            worktree.Branch ?? leaf,
            names,
            worktree.Path,
            worktree.Branch,
            worktree.IsBare ? null : worktree.Head,
            !Directory.Exists(worktree.Path));
    }

    private static async Task<GitCommandResult> RunAsync(
        IGitCli git, string workspace, string arguments, CancellationToken ct)
    {
        var result = await git.RunAsync(workspace, arguments, ct);
        if (result.ExitCode != 0)
        {
            var detail = result.StandardError.Trim();
            if (detail.Length == 0)
            {
                detail = result.StandardOutput.Trim();
            }

            throw new McpException(
                $"git {arguments} failed in {workspace} (exit {result.ExitCode}): {detail}");
        }

        return result;
    }

    private static bool SameWorkspace(string left, string right) => string.Equals(
        Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
        StringComparison.OrdinalIgnoreCase);
}
