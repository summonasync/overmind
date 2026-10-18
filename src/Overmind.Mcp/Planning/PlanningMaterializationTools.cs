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

    public PlanningMaterializationTools(OvermindApiClient api, MutationGuard mutations)
    {
        _api = api;
        _mutations = mutations;
    }

    [McpServerTool(Name = "planning_import_feature", Destructive = false, Idempotent = false)]
    [Description("Imports one materialized feature folder from disk in a single call — bodies never pass through the model. Give the absolute path of the folder that holds feature.md, under the project root (e.g. /repo/BACKLOG/058-agent-side-import); spec.md, plan.md, result.md and tasks/NN-*.md are picked up by convention, statuses are restored verbatim with [import] markers in the logs, and existing artifacts are re-imported as new versions. Relative paths and paths outside the project root are rejected. Import never enforces the create-path number-collision guard — a mirror with colliding NNN numbers restores verbatim. The response reports created vs updated per feature and artifact plus per-file notes.")]
    public Task<JsonElement> ImportFeature(
        [Description("Absolute path of the feature folder under the project root — the directory containing feature.md.")] string path,
        [Description("Id of the project to import the feature into.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        if (projectId is null)
        {
            throw new McpException(PlanningToolText.ProjectIdRequired);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new McpException("path is required: the feature folder to import, e.g. /repo/BACKLOG/058-agent-side-import.");
        }

        return _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{projectId}/planning/import",
            new { path = path.Trim() },
            ct), readback: null, ct);
    }

    [McpServerTool(Name = "planning_materialize", Destructive = false, Idempotent = false)]
    [Description("Triggers a materialization pass: the full plan is computed first, then written atomically — any guarded file (untracked at a planned path, or tracked with local edits not on the board) aborts the whole run with a 409 listing every refusal, and nothing is written that run. Skips in the report are byte-identical no-ops (content already matches the board), not guarded files. A normal board mutation already enqueues a pass that runs shortly after in the background; this tool runs the pass inline and waits for the report — call it after an import, a strategy switch, or when onboard reports a failed attempt or stale mirror state. force adopts guarded files with per-file warnings instead of refusing; transcribe hand edits to the board before forcing.")]
    public async Task<JsonElement> Materialize(
        [Description("Id of the project to materialize.")] Guid? projectId = null,
        [Description("True to overwrite guarded files with per-file warnings instead of refusing them; transcribe hand edits to the board before forcing.")] bool force = false,
        CancellationToken ct = default)
    {
        if (projectId is null)
        {
            throw new McpException(PlanningToolText.ProjectIdRequired);
        }

        var report = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/projects/{projectId}/planning/materialize",
            new { force },
            ct), readback: null, ct);
        return CompactMaterialization(report, force);
    }

    [McpServerTool(Name = "planning_materialize_status", ReadOnly = true, Idempotent = true)]
    [Description("Mirror diagnostics without filesystem access: returns the project's materialization state as one structured payload — lastSuccess vs lastAttempt (lastAttemptReason carries the failure when the attempt did not succeed), the persisted last-run report per file (startedAt, finishedAt, atomic, written paths, skipped and refused with reasons), trackedFiles, stalePaths (tracked files the last run neither wrote nor skipped, plus tracked files whose disk bytes drifted), strategy and targetDir. lastRun is null when the failed attempt persisted no report — read it as no report for this attempt. Read-only: never triggers a pass; use planning_materialize for that.")]
    public Task<JsonElement> MaterializeStatus(
        [Description("Id of the project to inspect.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        if (projectId is null)
        {
            throw new McpException(PlanningToolText.ProjectIdRequired);
        }

        return Guarded(() => _api.GetAsync(
            $"/api/agent/projects/{projectId}/planning/materialization-status",
            ct));
    }

    [McpServerTool(Name = "planning_export_backlog", Destructive = false, Idempotent = false)]
    [Description("Renders the full materialized mirror (planning tree + entries projection) into an explicit target directory as plain greppable files — the \"commit the planning alongside the code\" path. Uses the same document renderers as the materializer pass, so the snapshot is byte-identical to what a materialize run would write; zero tracking rows and zero drift machinery are created. The target must be an empty directory — non-empty refuses with 409 (a snapshot, not a sync) — and it must not sit inside BACKLOG/ or ENTRIES/. targetPath is always explicit; there is no default. Compact echo: written file count + target, no body echo.")]
    public async Task<JsonElement> ExportBacklog(
        [Description("Absolute path of the target directory: it must be empty or not exist yet, and it cannot sit inside BACKLOG/ or ENTRIES/.")] string targetPath,
        [Description("Id of the project whose mirror to export.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        if (projectId is null)
        {
            throw new McpException(PlanningToolText.ProjectIdRequired);
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new McpException(
                "targetPath is required: an absolute path of the empty target directory for the snapshot.");
        }

        var report = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{projectId}/planning/export",
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

        return compact.Deserialize<JsonElement>();
    }
}
