using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class WaveSnapshotTools
{
    private readonly OvermindApiClient _api;
    private readonly IGitCli _git;
    private readonly WaveListingCache _listing;

    public WaveSnapshotTools(OvermindApiClient api, IGitCli git, WaveListingCache listing)
    {
        _api = api;
        _git = git;
        _listing = listing;
    }

    [McpServerTool(Name = "overmind_wave_snapshot", ReadOnly = true, Idempotent = true)]
    [Description("One-call read projection for wave coordination — the whole marshal picture without the 10-call fan-out. " +
        "The host half returns the task queue (planning_task_queue rows verbatim), per-claim identity (alias, vendor, " +
        "claimState, claimedAt, claimedByHuman, last board activity), per in-flight feature code pins (claimed or " +
        "inProgress/review features), open feature blockers rolled up per feature, and the latest coordination entries " +
        "by tag (defaults: wave-sync, lane-marshalling — overridable). The adapter half enriches in-flight features with " +
        "repo code discovery (the planning_feature_code projection: worktrees/branches carrying the slug or NNN prefix, " +
        "pins matched first and flagged, stale pins reported): the git listing is cached briefly, so repeated snapshots " +
        "in one marshal pass do not spawn git processes per request; a discovery failure degrades to discoveryError and " +
        "the rest of the payload is unaffected. compact=true projects every section to coordination-critical fields " +
        "only (queue rows drop briefExcerpt, taskCounts, spec/plan statuses and materializer stamps; claims drop " +
        "lastActivityAt; entries drop snippets — discovery, code pins and blockers pass through unchanged); " +
        "default false keeps the full payload. projectId is required; project resolution never creates a project row.")]
    public async Task<JsonElement> WaveSnapshot(
        [Description("Id of the project to snapshot.")] Guid? projectId = null,
        [Description("Optional comma-separated tag list for the coordination entries section; " +
            "defaults to wave-sync,lane-marshalling.")] string? tags = null,
        [Description("Optional cap (1-100) on the coordination entries section; default 20.")] int? entryLimit = null,
        [Description("Project each section to coordination-critical fields only to save tokens; " +
            "default false keeps the full payload.")] bool compact = false,
        CancellationToken ct = default)
    {
        if (projectId is null)
        {
            throw new McpException(PlanningToolText.ProjectIdRequired);
        }

        var url = $"/api/agent/projects/{projectId}/planning/wave-snapshot";
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(tags))
        {
            query.Add($"tags={Uri.EscapeDataString(tags)}");
        }

        if (entryLimit is not null)
        {
            query.Add($"entryLimit={entryLimit}");
        }

        if (query.Count > 0)
        {
            url += $"?{string.Join('&', query)}";
        }

        var snapshot = await ReadAsync(url, ct);
        return await WithDiscoveryAsync(compact ? WaveSnapshotCompactor.Compact(snapshot) : snapshot, ct);
    }

    private async Task<JsonElement> ReadAsync(string url, CancellationToken ct)
    {
        try
        {
            return await _api.GetAsync(url, ct);
        }
        catch (ApiStatusException ex)
        {
            throw new McpException(MutationGuard.StopDetailOf(ex) ?? ex.Message);
        }
        catch (Exception ex) when (ex is ApiNotFoundException or ApiUnreachableException)
        {
            throw new McpException(ex.Message);
        }
    }

    private async Task<JsonElement> WithDiscoveryAsync(JsonElement snapshot, CancellationToken ct)
    {
        if (snapshot.ValueKind != JsonValueKind.Object ||
            !snapshot.TryGetProperty("codeLocations", out var locations) ||
            locations.ValueKind != JsonValueKind.Array ||
            locations.GetArrayLength() == 0)
        {
            return snapshot;
        }

        var workspace = Environment.CurrentDirectory;
        var payload = JsonNode.Parse(snapshot.GetRawText())!.AsObject();
        if (!GitRepositoryProbe.IsRepository(workspace))
        {
            payload["workspace"] = workspace;
            payload["discoveryError"] =
                $"code discovery skipped: {workspace} is not inside a git repository; the rest of the snapshot is unaffected";
            return payload.Deserialize<JsonElement>();
        }

        try
        {
            var listing = await _listing.GetAsync(workspace, _git, ct);
            var discovery = new JsonArray();
            foreach (var location in locations.EnumerateArray())
            {
                discovery.Add(await DiscoverAsync(location, listing, workspace, ct));
            }

            payload["workspace"] = workspace;
            payload["discovery"] = discovery;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            payload["discoveryError"] =
                $"code discovery failed, the rest of the snapshot is unaffected: {ex.Message}";
        }

        return payload.Deserialize<JsonElement>();
    }

    private async Task<JsonObject> DiscoverAsync(
        JsonElement location, WaveListing listing, string workspace, CancellationToken ct)
    {
        var slug = StringOf(location, "slug") ?? string.Empty;
        var matches = FeatureCodeMerger.Merge(
            slug,
            listing.Candidates,
            StringOf(location, "pinnedBranch"),
            StringOf(location, "pinnedWorktreePath"));

        var anyPinned = matches.Any(match => match.Pinned);
        var array = new JsonArray();
        foreach (var match in matches)
        {
            var commit = await CommitOfAsync(match, listing.Heads, workspace, ct);
            var entry = new JsonObject
            {
                ["kind"] = match.Kind,
                ["name"] = match.Name,
                ["path"] = match.Path,
                ["stale"] = match.Stale
            };
            if (anyPinned)
            {
                entry["pinned"] = match.Pinned;
            }

            entry["lastCommit"] = commit is null
                ? null
                : new JsonObject { ["hash"] = commit.Value.Hash, ["date"] = commit.Value.Date };
            array.Add(entry);
        }

        return new JsonObject
        {
            ["slug"] = slug,
            ["matches"] = array,
            ["workspace"] = workspace
        };
    }

    private async Task<(string Hash, string Date)?> CommitOfAsync(
        FeatureCodeCandidate match,
        IReadOnlyDictionary<string, GitBranchHead> heads,
        string workspace,
        CancellationToken ct)
    {
        if (match.Ref is not null && heads.TryGetValue(match.Ref, out var head))
        {
            return (head.ShortHash, head.CommitDate);
        }

        if (match.Kind == FeatureCodeCandidate.WorktreeKind &&
            match.HeadSha is { } sha && sha.Length is >= 4 and <= 64 && sha.All(char.IsAsciiHexDigit))
        {
            var result = await _git.RunAsync(workspace, $"log -1 --format=%h%x09%cI {sha}", ct);
            var parts = result.StandardOutput.TrimEnd().Split('\t');
            if (result.ExitCode == 0 && parts.Length == 2)
            {
                return (parts[0], parts[1]);
            }
        }

        return null;
    }

    private static string? StringOf(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
