using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class RescueScanTools
{
    private const string BranchListArguments =
        "for-each-ref --format=%(refname:short) refs/heads";

    private readonly OvermindApiClient _api;
    private readonly IGitCli _git;
    private readonly ProjectResolver _projects;

    public RescueScanTools(OvermindApiClient api, IGitCli git, ProjectResolver projects)
    {
        _api = api;
        _git = git;
        _projects = projects;
    }

    [McpServerTool(Name = "planning_rescue_scan", ReadOnly = true, Idempotent = true)]
    [Description("Rescue scan over dead claims: pull the rescue-candidates door, then measure each candidate " +
        "adapter-side with git — uncommitted files, commits ahead of upstream (origin/main fallback), merged into " +
        "mainline or not, last commit. claimState here is raw liveness, stale|ended only. Facts alone: nothing " +
        "is pushed, merged or claimed by this tool.")]
    public async Task<JsonElement> RescueScan(
        [Description("Optional feature slug: scan that candidate only.")] string? slug = null,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the rescue scan", ct);
        var source = await new RescueScanReader(_api).ReadAsync(resolved, slug, ct);
        var root = await ScanRootAsync(resolved, ct);
        var listing = source.Candidates.Count switch
        {
            0 => RescueListing.NothingToDo,
            _ when !root.GitAvailable => RescueListing.WithoutGit(
                $"git facts skipped: no git repository at either the project disk path or {Environment.CurrentDirectory}; " +
                "board facts are still answered, verdicts are not"),
            _ => await ListingAsync(root.Path, ct),
        };
        var allowed = source.Autonomy == RescueScanSource.OwnerSet;

        var rows = new JsonArray();
        var scanner = new RescueGitScanner(_git);
        foreach (var row in source.Candidates)
        {
            rows.Add(await RowJsonAsync(row, listing, root.Path, scanner, allowed, ct));
        }

        var payload = new JsonObject
        {
            ["projectId"] = resolved.ToString(),
            ["generatedAt"] = source.GeneratedAt,
            ["scanRoot"] = new JsonObject
            {
                ["path"] = root.Path,
                ["source"] = root.Source,
                ["adapterWorkspace"] = Environment.CurrentDirectory,
            },
            ["policy"] = new JsonObject
            {
                ["text"] = source.PolicyText,
                ["autonomy"] = source.Autonomy,
                ["guidance"] = source.Guidance,
                ["error"] = source.PolicyError,
            },
            ["candidates"] = rows,
            ["counts"] = Counts(rows),
        };

        if (rows.Count == 0)
        {
            payload["hint"] = slug is null
                ? "no stale or ended claim on an in-progress or review feature — nothing to rescue"
                : $"no rescue candidate carries slug '{slug}'";
        }

        if (listing.Unavailable is { } reason)
        {
            payload["discoveryError"] = reason;
        }

        return JsonSerializer.SerializeToElement(payload);
    }

    private async Task<JsonObject> RowJsonAsync(
        RescueCandidateRow row,
        RescueListing listing,
        string root,
        RescueGitScanner scanner,
        bool allowed,
        CancellationToken ct)
    {
        var source = row.Source;

        if (listing.Unavailable is { } reason)
        {
            source["code"] = null;
            source["git"] = null;
            source["verdict"] = RescueVerdict.Unknown;
            source["reasons"] = new JsonArray(reason);
            source["rescueAllowed"] = false;
            return source;
        }

        var location = RescueLocationPlanner.Locate(
            row.Slug, row.PinnedWorktreePath, row.PinnedBranch, listing.Worktrees, listing.Branches);
        var facts = await scanner.ScanAsync(location, root, ct);
        var verdict = RescueVerdict.Of(location, facts);

        source["code"] = new JsonObject
        {
            ["source"] = location.Source,
            ["worktreePath"] = location.WorktreePath,
            ["worktreeExists"] = location.WorktreeExists,
            ["branch"] = location.Branch,
            ["branchExists"] = location.BranchExists,
            ["stale"] = location.Stale,
            ["measuredIn"] = location.Measurable ? location.RunIn(root) : null,
        };
        source["git"] = new JsonObject
        {
            ["dirtyFiles"] = facts.DirtyFiles,
            ["untrackedFiles"] = facts.UntrackedFiles,
            ["unpushedCommits"] = facts.UnpushedCommits,
            ["unpushedBase"] = facts.UnpushedBase,
            ["unmerged"] = facts.Unmerged,
            ["mergedInto"] = facts.Mainline,
            ["lastCommit"] = facts.LastCommitHash is null
                ? null
                : new JsonObject { ["hash"] = facts.LastCommitHash, ["date"] = facts.LastCommitDate },
            ["error"] = facts.Error,
        };
        source["verdict"] = verdict.Verdict;
        source["reasons"] = new JsonArray([.. verdict.Reasons.Select(reason => (JsonNode)reason)]);
        source["rescueAllowed"] = allowed;

        return source;
    }

    private async Task<RescueListing> ListingAsync(string root, CancellationToken ct)
    {
        if (!GitRepositoryProbe.IsRepository(root))
        {
            return RescueListing.WithoutGit(
                $"git facts skipped: {root} is not inside a git repository; board facts are still answered, verdicts are not");
        }

        try
        {
            var worktrees = await _git.RunAsync(root, "worktree list --porcelain", ct);
            var branches = await _git.RunAsync(root, BranchListArguments, ct);
            if (worktrees.ExitCode != 0 || branches.ExitCode != 0)
            {
                var failure = worktrees.ExitCode != 0 ? worktrees : branches;

                return RescueListing.WithoutGit(
                    $"git facts skipped: listing failed in {root} (exit {failure.ExitCode}): {Clip(Detail(failure))}");
            }

            return new RescueListing(
                GitWorktreeListParser.Parse(worktrees.StandardOutput),
                [.. branches.StandardOutput
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
                null);
        }
        catch (Exception ex) when (ex is McpException or InvalidOperationException)
        {
            return RescueListing.WithoutGit($"git facts skipped: {ex.Message}");
        }
    }

    private async Task<(string Path, string Source, bool GitAvailable)> ScanRootAsync(
        Guid projectId,
        CancellationToken ct)
    {
        var workspace = Environment.CurrentDirectory;
        string? diskPath = null;
        try
        {
            var project = await _api.GetAsync($"/api/projects/{projectId}", ct);
            diskPath = project.TryGetProperty("diskPath", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (Exception ex) when (ex is ApiStatusException or ApiNotFoundException or ApiUnreachableException)
        {
            Console.Error.WriteLine($"[overmind-mcp] WARN: rescue scan could not read the project disk path: {ex.Message}");
        }

        if (diskPath is { Length: > 0 } path && Directory.Exists(path) && GitRepositoryProbe.IsRepository(path))
        {
            return (Path.TrimEndingDirectorySeparator(path), "project-disk-path", true);
        }

        return (workspace, "adapter-workspace", GitRepositoryProbe.IsRepository(workspace));
    }

    private static JsonObject Counts(JsonArray rows)
    {
        var verdicts = rows
            .OfType<JsonObject>()
            .Where(row => row["verdict"] is JsonValue)
            .Select(row => row["verdict"]!.GetValue<string>())
            .GroupBy(verdict => verdict, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return new JsonObject
        {
            ["candidates"] = rows.Count,
            ["rescueWorthy"] = verdicts.GetValueOrDefault(RescueVerdict.RescueWorthy),
            ["clean"] = verdicts.GetValueOrDefault(RescueVerdict.Clean),
            ["stale"] = verdicts.GetValueOrDefault(RescueVerdict.Stale),
            ["unknown"] = verdicts.GetValueOrDefault(RescueVerdict.Unknown),
        };
    }

    private static string Detail(GitCommandResult result)
    {
        var error = result.StandardError.Trim();

        return error.Length > 0 ? error : result.StandardOutput.Trim();
    }

    private static string Clip(string value) => value.Length <= 200 ? value : value[..200];

    private sealed record RescueListing(
        IReadOnlyList<GitWorktree> Worktrees,
        IReadOnlyCollection<string> Branches,
        string? Unavailable)
    {
        public static readonly RescueListing NothingToDo = new([], [], null);

        public static RescueListing WithoutGit(string reason) => new([], [], reason);
    }
}
