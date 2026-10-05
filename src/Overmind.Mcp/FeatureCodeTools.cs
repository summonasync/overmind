using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class FeatureCodeTools
{
    private const string ProjectIdEnvironmentVariable = "OVERMIND_PROJECT_ID";
    private const string WorktreeKind = "worktree";
    private const string BranchKind = "branch";
    private const string BranchListArguments =
        "for-each-ref --format=%(refname:short)%09%(objectname:short)%09%(committerdate:iso8601-strict) refs/heads";
    private const string NoMatchHint =
        "No branch or worktree matches this slug in the workspace. Name branches and worktrees feat/NNN-slug " +
        "(e.g. feat/063-worktree-feature-binding for slug 063-worktree-feature-binding) so discovery stays zero-input.";

    private readonly OvermindApiClient _api;
    private readonly IGitCli _git;

    public FeatureCodeTools(OvermindApiClient api, IGitCli git)
    {
        _api = api;
        _git = git;
    }

    [McpServerTool(Name = "planning_feature_code", ReadOnly = true, Idempotent = true)]
    [Description("One call answers where this feature's code is. Discovery runs adapter-side in the working directory " +
        "(git worktree list --porcelain + a branch listing, read-only): branches and worktrees whose names carry the " +
        "feature's slug or its NNN prefix (the feat/NNN-slug convention) are reported with kind (worktree/branch), name, " +
        "path and last commit short hash + date; a worktree whose path no longer exists is reported stale, not dropped. " +
        "Multiple matches are all listed; no match returns an honest empty result with the naming-convention hint. " +
        "The answer rides along with the host's feature read (status/priority context) into one payload — the host half " +
        "may fail independently, and then the discovery half is still answered with featureError set.")]
    public async Task<JsonElement> FeatureCode(
        [Description("Slug of the feature, as shown on the board.")] string slug,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var trimmed = slug.Trim();
        if (trimmed.Length == 0)
        {
            throw new McpException("slug is required: pass the feature slug as shown on the board.");
        }

        var resolved = await ResolveProjectIdAsync(projectId, ct);
        var workspace = Environment.CurrentDirectory;
        var (matches, heads) = await DiscoverAsync(trimmed, workspace, ct);

        var payload = new JsonObject
        {
            ["slug"] = trimmed,
            ["workspace"] = workspace,
            ["matches"] = await MatchesJsonAsync(matches, heads, workspace, ct),
            ["hint"] = matches.Count == 0 ? NoMatchHint : null
        };
        await AppendFeatureAsync(payload, resolved, trimmed, workspace, ct);
        return JsonSerializer.SerializeToElement(payload);
    }

    private async Task<(IReadOnlyList<FeatureCodeCandidate> Matches, IReadOnlyDictionary<string, GitBranchHead> Heads)> DiscoverAsync(
        string slug, string workspace, CancellationToken ct)
    {
        var listing = await RunAsync(workspace, "worktree list --porcelain", ct);
        var branchList = await RunAsync(workspace, BranchListArguments, ct);
        var heads = GitBranchListParser.Parse(branchList.StandardOutput)
            .ToDictionary(head => head.Name, StringComparer.Ordinal);

        var candidates = new List<FeatureCodeCandidate>();
        foreach (var worktree in GitWorktreeListParser.Parse(listing.StandardOutput))
        {
            candidates.Add(WorktreeCandidate(worktree));
        }

        foreach (var head in heads.Values)
        {
            candidates.Add(new FeatureCodeCandidate(
                BranchKind, head.Name, [head.Name], null, head.Name, null, false));
        }

        return (FeatureCodeMatcher.Match(slug, candidates), heads);
    }

    private static FeatureCodeCandidate WorktreeCandidate(GitWorktree worktree)
    {
        var leaf = Path.GetFileName(worktree.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string[] names = worktree.Branch is null || worktree.Branch == leaf
            ? [leaf]
            : [worktree.Branch, leaf];

        return new FeatureCodeCandidate(
            WorktreeKind,
            worktree.Branch ?? leaf,
            names,
            worktree.Path,
            worktree.Branch,
            worktree.IsBare ? null : worktree.Head,
            !Directory.Exists(worktree.Path));
    }

    private async Task<JsonArray> MatchesJsonAsync(
        IReadOnlyList<FeatureCodeCandidate> matches,
        IReadOnlyDictionary<string, GitBranchHead> heads,
        string workspace,
        CancellationToken ct)
    {
        var array = new JsonArray();
        foreach (var match in matches)
        {
            var commit = await CommitOfAsync(match, heads, workspace, ct);
            array.Add(new JsonObject
            {
                ["kind"] = match.Kind,
                ["name"] = match.Name,
                ["path"] = match.Path,
                ["stale"] = match.Stale,
                ["lastCommit"] = commit is null
                    ? null
                    : new JsonObject { ["hash"] = commit.Value.Hash, ["date"] = commit.Value.Date }
            });
        }

        return array;
    }

    private async Task<(string Hash, string Date)?> CommitOfAsync(
        FeatureCodeCandidate match, IReadOnlyDictionary<string, GitBranchHead> heads, string workspace, CancellationToken ct)
    {
        if (match.Ref is not null && heads.TryGetValue(match.Ref, out var head))
        {
            return (head.ShortHash, head.CommitDate);
        }

        if (match.Kind == WorktreeKind && match.HeadSha is { } sha && sha.Length is >= 4 and <= 64 && sha.All(char.IsAsciiHexDigit))
        {
            var result = await RunAsync(workspace, $"log -1 --format=%h%x09%cI {sha}", ct);
            var parts = result.StandardOutput.TrimEnd().Split('\t');
            if (parts.Length == 2)
            {
                return (parts[0], parts[1]);
            }
        }

        return null;
    }

    private async Task<GitCommandResult> RunAsync(string workspace, string arguments, CancellationToken ct)
    {
        var result = await _git.RunAsync(workspace, arguments, ct);
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

    private async Task AppendFeatureAsync(JsonObject payload, Guid projectId, string slug, string workspace, CancellationToken ct)
    {
        try
        {
            var project = await _api.GetAsync($"/api/projects/{projectId}", ct);
            payload["workspaceMatchesProject"] = WorkspaceMatchesProject(workspace, StringOf(project, "diskPath"));
            var feature = await _api.GetAsync(
                $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}?includeArtifacts=false", ct);
            payload["feature"] = new JsonObject
            {
                ["title"] = StringOf(feature, "title"),
                ["status"] = StringOf(feature, "status"),
                ["priority"] = StringOf(feature, "priority"),
                ["updatedAt"] = StringOf(feature, "updatedAt")
            };
            payload["featureError"] = null;
        }
        catch (Exception ex) when (ex is ApiStatusException or ApiNotFoundException or ApiUnreachableException)
        {
            payload["feature"] = null;
            payload["featureError"] = $"the host feature read failed, so only the discovery half is answered: {ex.Message}";
        }
    }

    private static bool WorkspaceMatchesProject(string workspace, string? diskPath)
    {
        if (diskPath is null)
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(diskPath).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? StringOf(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private async Task<Guid> ResolveProjectIdAsync(Guid? projectId, CancellationToken ct)
    {
        if (projectId is not null)
        {
            return projectId.Value;
        }

        var overrideId = Environment.GetEnvironmentVariable(ProjectIdEnvironmentVariable);
        if (Guid.TryParse(overrideId, out var fromEnvironment))
        {
            return fromEnvironment;
        }

        JsonElement onboard;
        try
        {
            onboard = await _api.PostAsync("/api/onboard", new { cwd = Environment.CurrentDirectory }, ct);
        }
        catch (Exception ex) when (ex is ApiStatusException or ApiNotFoundException or ApiUnreachableException)
        {
            throw new McpException(
                $"Could not resolve the project for the working directory: {ex.Message}. Pass projectId explicitly or call overmind_onboard first.");
        }

        if (onboard.ValueKind == JsonValueKind.Object &&
            onboard.TryGetProperty("projectId", out var value) &&
            value.ValueKind == JsonValueKind.String &&
            Guid.TryParse(value.GetString(), out var resolved))
        {
            return resolved;
        }

        throw new McpException(
            "The host returned no usable projectId for the working directory. Call overmind_onboard, then pass its projectId explicitly.");
    }
}
