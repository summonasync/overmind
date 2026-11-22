using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Overmind.Mcp;

namespace Overmind.Mcp.Planning;

using static Overmind.Mcp.Planning.PlanningToolResponses;

[McpServerToolType]
public sealed class PlanningMaterializationTools
{
    private const int SkipHeadLimit = 5;

    private readonly OvermindApiClient _api;
    private readonly MutationGuard _mutations;
    private readonly ProjectResolver _projects;

    public PlanningMaterializationTools(OvermindApiClient api, MutationGuard mutations, ProjectResolver projects)
    {
        _api = api;
        _mutations = mutations;
        _projects = projects;
    }

    [McpServerTool(Name = "planning_import_feature", Destructive = false, Idempotent = false)]
    [Description("Imports a materialized feature folder from disk — bodies never pass through the model. Needs the folder path holding feature.md.")]
    public async Task<JsonElement> ImportFeature(
        [Description("Absolute folder path inside the project root.")] string path,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new McpException("path is required: the feature folder to import, e.g. /repo/BACKLOG/058-agent-side-import.");
        }

        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        return await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/import",
            new { path = path.Trim() },
            ct), readback: null, ct);
    }

    [McpServerTool(Name = "planning_materialize", Destructive = false, Idempotent = false)]
    [Description("Runs a materialization pass inline and returns the report; board mutations already enqueue one. force adopts guarded files instead of refusing. quarantine skips guarded files untouched (hand edits survive), reports them as quarantined, and mirrors everything else.")]
    public async Task<JsonElement> Materialize(
        [Description("Optional project id.")] Guid? projectId = null,
        [Description("Adopt guarded files instead of refusing.")] bool force = false,
        [Description("Skip guarded files untouched and report them as quarantined instead of aborting the pass. force wins when both are set.")] bool quarantine = false,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var report = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/projects/{resolved}/planning/materialize",
            new { force, quarantine },
            ct), readback: null, ct);
        return CompactMaterialization(report, force);
    }

    [McpServerTool(Name = "planning_materialize_status", ReadOnly = true, Idempotent = true)]
    [Description("Mirror diagnostics without touching disk: last attempt vs success, pending mutations, stale paths. Never triggers a pass.")]
    public async Task<JsonElement> MaterializeStatus(
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);

        return await Guarded(() => _api.GetAsync(
            $"/api/agent/projects/{resolved}/planning/materialization-status",
            ct));
    }

    [McpServerTool(Name = "planning_export_backlog", Destructive = false, Idempotent = false)]
    [Description("Renders the materialized mirror into an explicit empty directory as greppable files. A snapshot, not a sync.")]
    public async Task<JsonElement> ExportBacklog(
        [Description("Absolute path of an empty target directory.")] string targetPath,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new McpException(
                "targetPath is required: an absolute path of the empty target directory for the snapshot.");
        }

        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var report = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/export",
            new { targetPath = targetPath.Trim() },
            ct), readback: null, ct);

        return new JsonObject
        {
            ["projectId"] = report.GetProperty("projectId").GetString(),
            ["target"] = report.GetProperty("target").GetString(),
            ["files"] = report.GetProperty("fileCount").GetInt32()
        }.Deserialize<JsonElement>();
    }

    private static JsonElement CompactMaterialization(JsonElement report, bool force)
    {
        var skipped = report.GetProperty("skipped");
        var compact = new JsonObject
        {
            ["projectId"] = report.GetProperty("projectId").GetString(),
            ["strategy"] = report.GetProperty("strategy").GetString(),
            ["written"] = report.GetProperty("files").GetArrayLength(),
            ["skipped"] = skipped.GetArrayLength(),
            ["force"] = force
        };

        if (report.TryGetProperty("lastSuccess", out var lastSuccess))
        {
            compact["lastSuccess"] = lastSuccess.ValueKind == JsonValueKind.String
                ? lastSuccess.GetString()
                : null;
        }

        if (report.TryGetProperty("lastAttempt", out var lastAttempt))
        {
            compact["lastAttempt"] = lastAttempt.ValueKind == JsonValueKind.String
                ? lastAttempt.GetString()
                : null;
        }

        if (skipped.GetArrayLength() > 0)
        {
            var head = new JsonArray();
            foreach (var skip in skipped.EnumerateArray().Take(SkipHeadLimit))
            {
                head.Add($"{skip.GetProperty("relativePath").GetString()}: {skip.GetProperty("reason").GetString()}");
            }

            compact["skippedHead"] = head;
        }

        if (report.TryGetProperty("quarantined", out var quarantined) &&
            quarantined.ValueKind == JsonValueKind.Array)
        {
            compact["quarantined"] = quarantined.GetArrayLength();
            if (quarantined.GetArrayLength() > 0)
            {
                var head = new JsonArray();
                foreach (var skip in quarantined.EnumerateArray().Take(SkipHeadLimit))
                {
                    head.Add($"{skip.GetProperty("relativePath").GetString()}: {skip.GetProperty("reason").GetString()}");
                }

                compact["quarantinedHead"] = head;
            }
        }

        return compact.Deserialize<JsonElement>();
    }
}
