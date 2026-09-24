using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class OvermindPlanningTools
{
    private static readonly HashSet<string> ArtifactKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "spec", "plan", "task", "result"
    };

    private static readonly HashSet<string> ReviewLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "light", "hard"
    };

    private static readonly Dictionary<string, string> ArtifactStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["draft"] = "draft",
        ["validated"] = "validated",
        ["todo"] = "todo",
        ["inProgress"] = "inProgress",
        ["review"] = "review",
        ["done"] = "done",
        ["blocked"] = "blocked"
    };

    private static readonly Dictionary<string, string> AgentFeatureStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["done"] = "done",
        ["inProgress"] = "inProgress"
    };

    private static readonly HashSet<string> FeaturePriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "p0", "p1", "p2", "p3"
    };

    private const string StatusNoteRequired =
        "logNote is required for status moves: every status change is recorded as a dated line in the artifact <log>.";

    private const string FeatureStatusNoteRequired =
        "logNote is required for feature status moves: the note is recorded as a dated line in the feature log and carries the provenance (who asked for the move).";

    private const string PatchShapeError =
        "A patch carries either a section with its replacement content, or an acceptance-criteria tick (acIndex or exact acText) — not both and not neither.";

    private const string MigratedNoteRequired =
        "logNote is required with asMigrated: the migration marker must record where the pre-approval came from.";

    private const int SkipHeadLimit = 5;

    private readonly OvermindApiClient _api;

    public OvermindPlanningTools(OvermindApiClient api)
    {
        _api = api;
    }

    [McpServerTool(Name = "planning_list_features", ReadOnly = true, Idempotent = true)]
    [Description("Planning ceremony, step 1: the feature board of a project — slugs, statuses, spec/plan validation and task progress. Read it before picking work.")]
    public Task<JsonElement> ListFeatures(
        [Description("Id of the project whose feature board to read.")] Guid projectId,
        CancellationToken ct) =>
        Guarded(() => _api.GetAsync($"/api/agent/projects/{projectId}/planning/features", ct));

    [McpServerTool(Name = "planning_read_feature", ReadOnly = true, Idempotent = true)]
    [Description("Reads one feature in full: its body plus every spec, plan, task and result artifact with statuses and log lines — the most expensive read: the whole feature with all artifacts; prefer planning_read_artifact for one document.")]
    public Task<JsonElement> ReadFeature(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature, as shown on the board.")] string slug,
        CancellationToken ct) =>
        Guarded(() => _api.GetAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}", ct));

    [McpServerTool(Name = "planning_read_artifact", ReadOnly = true, Idempotent = true)]
    [Description("Reads one artifact by id — without sections, one artifact, full body; with sections, the cheapest read: only the named sections. missingSections names requested-but-absent sections; a read never fails on a missing one.")]
    public Task<JsonElement> ReadArtifact(
        [Description("Id of the artifact to read, as handed out by planning_current_task.")] Guid artifactId,
        [Description("Optional comma/space-separated section names, e.g. log,obstacles — narrows the body to those sections.")] string? sections = null,
        CancellationToken ct = default)
    {
        var url = $"/api/agent/planning/artifacts/{artifactId}";
        if (!string.IsNullOrWhiteSpace(sections))
        {
            url += $"?sections={Uri.EscapeDataString(sections)}";
        }

        return Guarded(() => _api.GetAsync(url, ct));
    }

    [McpServerTool(Name = "planning_current_task", ReadOnly = true, Idempotent = true)]
    [Description("Planning ceremony, step 2: the current task of a feature, or a stop/empty state whose reason is the first thing in the payload. Obey a STOP line — ask the user, do not work around it.")]
    public async Task<JsonElement> CurrentTask(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature to take the current task from.")] string slug,
        CancellationToken ct)
    {
        var payload = await Guarded(() => _api.GetAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}/current", ct));
        return LeadWithStopReason(payload);
    }

    [McpServerTool(Name = "planning_create_feature", Destructive = false, Idempotent = false)]
    [Description("Creates a new feature on the planning board, optionally with a priority (p0 critical, p1 high, p2 normal — the default, p3 low). Follow with planning_upsert_artifact to write its spec.")]
    public async Task<JsonElement> CreateFeature(
        [Description("Id of the project to create the feature in.")] Guid projectId,
        [Description("Slug of the new feature: lowercase alphanumeric with hyphens, max 64 chars.")] string slug,
        [Description("Human-readable feature title.")] string title,
        [Description("Optional markdown body of the feature document (goal, requirements, non-goals).")] string? body = null,
        [Description("Optional priority: p0 (critical), p1 (high), p2 (normal — the default when omitted), p3 (low).")] string? priority = null,
        CancellationToken ct = default)
    {
        var payload = new JsonObject
        {
            ["slug"] = slug,
            ["title"] = title,
            ["body"] = body
        };

        if (priority is not null)
        {
            payload["priority"] = NormalizePriority(priority);
        }

        var feature = await Guarded(() => _api.PostAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            payload,
            ct));
        return CompactFeature(feature);
    }

    [McpServerTool(Name = "planning_upsert_artifact", Destructive = false, Idempotent = false)]
    [Description("Creates an artifact for a feature, or updates the existing artifact of the same kind and order as a new version — the most expensive write: resends the full body, so prefer planning_patch_artifact for one XML section or one acceptance item and planning_append_log for a log line. Artifacts are created draft; validation goes through planning_validate. Editing a validated artifact requires a logNote — not needed on create. Task artifacts also take a brief: a one-line summary (max 500 chars) shown next to the task; other kinds reject it.")]
    public async Task<JsonElement> UpsertArtifact(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature to add or update the artifact in.")] string featureSlug,
        [Description("Artifact kind: spec, plan, task or result.")] string kind,
        [Description("Markdown body of the artifact with its XML sections.")] string body,
        [Description("Order of a task artifact within the feature; document kinds ignore it.")] int? order = null,
        [Description("Optional review level of a task: light or hard.")] string? review = null,
        [Description("Change note recorded in the artifact log; required when editing a validated artifact.")] string? logNote = null,
        [Description("Optional one-line brief of a task artifact, max 500 chars; not allowed for other kinds.")] string? brief = null,
        CancellationToken ct = default)
    {
        var payload = new JsonObject
        {
            ["kind"] = NormalizeKind(kind),
            ["body"] = body,
            ["order"] = order,
            ["review"] = review is null ? null : NormalizeReview(review),
            ["logNote"] = logNote
        };

        if (brief is not null)
        {
            payload["brief"] = brief;
        }

        var artifact = await Guarded(() => _api.PostAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(featureSlug)}/artifacts",
            payload,
            ct));
        return CompactArtifact(artifact);
    }

    [McpServerTool(Name = "planning_set_status", Destructive = false, Idempotent = false)]
    [Description("Planning ceremony, step 3: moves an artifact to a new status and records why in the artifact log. Allowed statuses per kind — spec/plan/result: draft, validated; task: todo, inProgress, review, done, blocked. Validated is the terminal for documents; result has no done. The logNote is mandatory on every move — an unexplained status move is rejected. Each move appends one line to the artifact's <log> section, shaped '- YYYY-MM-DD [agent] — <logNote>'. Transitions are free-form: any allowed status of the kind can be reached from any current status — there is no enforced sequence. A task closed done with unticked acceptance-criteria items still moves, and the response carries a non-blocking warning counting them.")]
    public async Task<JsonElement> SetStatus(
        [Description("Id of the artifact to move.")] Guid artifactId,
        [Description("New status: draft, validated, todo, inProgress, review, done or blocked.")] string status,
        [Description("Required note recorded as a dated log line explaining the status move.")] string logNote,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(logNote))
        {
            throw new McpException(StatusNoteRequired);
        }

        var normalized = NormalizeStatus(status);
        var payload = await Guarded(() => _api.PostAsync(
            $"/api/agent/planning/artifacts/{artifactId}/status",
            new { status = normalized, logNote },
            ct));

        var confirmation = new JsonObject
        {
            ["id"] = artifactId.ToString(),
            ["status"] = normalized,
            ["lastLogLine"] = logNote
        };
        CopyWarnings(payload, confirmation);
        return confirmation.Deserialize<JsonElement>();
    }

    [McpServerTool(Name = "planning_set_current", Destructive = false, Idempotent = true)]
    [Description("Pins a feature as the current one on the planning board, or unpins it. Pinning one feature unpins any other — at most one feature is current per project.")]
    public async Task<string> SetCurrent(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature to pin or unpin, as shown on the board.")] string slug,
        [Description("True to pin the feature as current; false to unpin it.")] bool isCurrent,
        CancellationToken ct = default)
    {
        await Guarded(() => _api.SetPlanningFeatureCurrent(projectId, slug, isCurrent, ct));
        return isCurrent
            ? $"Feature '{slug}' pinned as current."
            : $"Feature '{slug}' unpinned.";
    }

    [McpServerTool(Name = "planning_set_feature_status", Destructive = false, Idempotent = false)]
    [Description("Moves a feature status on the planning board — only on the user's explicit request in the current session, never on the agent's own initiative. Allowed targets: done (close, from any non-done status) or inProgress (review rollback); every other manual move belongs to the human door in the Overmind UI. The logNote is mandatory, lands as a dated line in the feature log, and must carry the provenance, e.g. 'by user request in-session'.")]
    public async Task<string> SetFeatureStatus(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature to move, as shown on the board.")] string slug,
        [Description("New status: done or inProgress — the only agent-door targets.")] string status,
        [Description("Required note recorded as a dated log line; carries the provenance, e.g. 'by user request in-session'.")] string logNote,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(logNote))
        {
            throw new McpException(FeatureStatusNoteRequired);
        }

        var normalized = NormalizeFeatureStatus(status);
        await Guarded(() => _api.PostAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}/status",
            new { status = normalized, logNote },
            ct));
        return $"Feature status moved to '{normalized}'.";
    }

    [McpServerTool(Name = "planning_append_log", Destructive = false, Idempotent = false)]
    [Description("Planning ceremony, step 3 companion: appends a dated line to an artifact's log without touching its body — the cheapest write: one dated line, body is not resent. Use it to report progress between status moves.")]
    public async Task<string> AppendLog(
        [Description("Id of the artifact to append the note to.")] Guid artifactId,
        [Description("Note text recorded as a dated log line.")] string note,
        CancellationToken ct = default)
    {
        await Guarded(() => _api.PostAsync(
            $"/api/agent/planning/artifacts/{artifactId}/log",
            new { note },
            ct));
        return "Log line appended.";
    }

    [McpServerTool(Name = "planning_patch_artifact", Destructive = false, Idempotent = false)]
    [Description("Changes one artifact in place without resending its body — the middle tier: replaces one XML section or ticks one acceptance item, rest of the body untouched. Give section plus content to replace one section, or acIndex/acText to tick one acceptance-criteria item; never both. A missing section or item is a 400 that lists what is available. The log section is append-only: patching it is a 400 that points at planning_append_log, and history is rewritten only through a full upsert. Ticking an already-ticked item is a successful no-op, so a retried tick is safe; a replaced section is a real change every time. Editing a validated artifact requires a logNote.")]
    public async Task<string> PatchArtifact(
        [Description("Id of the artifact to patch.")] Guid artifactId,
        [Description("Name of the XML section to replace, e.g. goal.")] string? section = null,
        [Description("Full replacement content of the section.")] string? content = null,
        [Description("Index of the acceptance-criteria item to tick, as listed by the 400 diagnostics.")] int? acIndex = null,
        [Description("Exact text of the acceptance-criteria item to tick, without the checkbox marker.")] string? acText = null,
        [Description("Change note recorded in the artifact log; required when the artifact is validated.")] string? logNote = null,
        CancellationToken ct = default)
    {
        var op = PatchOpOf(section, content, acIndex, acText);
        var patch = await Guarded(() => _api.PostAsync(
            $"/api/agent/planning/artifacts/{artifactId}/patch",
            new { op, section, content, acIndex, acText, logNote },
            ct));
        return $"{patch.GetProperty("kind").GetString()} artifact {patch.GetProperty("artifactId").GetString()} patched — " +
            $"section '{patch.GetProperty("section").GetString()}', version {patch.GetProperty("version").GetInt32()}, " +
            $"{patch.GetProperty("bodyChars").GetInt32()} body chars.";
    }

    [McpServerTool(Name = "planning_validate", Destructive = false, Idempotent = false)]
    [Description("Validates a spec, plan or result artifact. When the project requires human validation this fails with a STOP error — ask the user to approve it in the Overmind UI instead of retrying. asMigrated is the migration escape hatch for artifacts pre-approved before import (e.g. already frozen on disk): it skips the human gate, requires a logNote carrying the provenance, and leaves a 'validated as migrated — pre-approved on disk' marker line in the artifact log.")]
    public async Task<CallToolResult> Validate(
        [Description("Id of the artifact to validate.")] Guid artifactId,
        [Description("True only for artifacts pre-approved before migration (e.g. already frozen on disk); skips the human gate and leaves a loud migration marker in the log.")] bool? asMigrated = null,
        [Description("Required with asMigrated: provenance of the pre-approval, e.g. 'frozen v9 on disk, user-approved'; recorded inside the marker line.")] string? logNote = null,
        CancellationToken ct = default)
    {
        if (asMigrated == true && string.IsNullOrWhiteSpace(logNote))
        {
            throw new McpException(MigratedNoteRequired);
        }

        try
        {
            await Guarded(() => _api.PostAsync(
                $"/api/agent/planning/artifacts/{artifactId}/validate",
                new { asMigrated, logNote },
                ct));
        }
        catch (McpException ex) when (ex.Message.StartsWith("STOP:", StringComparison.Ordinal))
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = ex.Message }]
            };
        }

        var text = asMigrated == true
            ? "Artifact validated as migrated — provenance line added to the log."
            : "Artifact validated.";
        return new CallToolResult { Content = [new TextContentBlock { Text = text }] };
    }

    [McpServerTool(Name = "planning_materialize", Destructive = false, Idempotent = false)]
    [Description("Triggers a materialization pass: rewrites the BACKLOG/ mirror from the board, writing every clean file and skipping guarded ones (untracked or locally-edited) with a reason — a rerun never silently overwrites disk content. A normal board mutation already triggers a pass automatically; call this after an import, a strategy switch, or when onboard reports skips or a stale lastRun. force overwrites guarded files with per-file warnings instead of skipping them; transcribe hand edits to the board before forcing.")]
    public async Task<JsonElement> Materialize(
        [Description("Id of the project to materialize.")] Guid projectId,
        [Description("True to overwrite guarded files with per-file warnings instead of skipping them; transcribe hand edits to the board before forcing.")] bool force = false,
        CancellationToken ct = default)
    {
        var report = await Guarded(() => _api.PostAsync(
            $"/api/projects/{projectId}/planning/materialize",
            new { force },
            ct));
        return CompactMaterialization(report, force);
    }

    private static void CopyWarnings(JsonElement payload, JsonObject confirmation)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("warnings", out var warnings) ||
            warnings.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        confirmation["warnings"] = JsonNode.Parse(warnings.GetRawText());
    }

    private static async Task<JsonElement> Guarded(Func<Task<JsonElement>> action)
    {
        try
        {
            return await action();
        }
        catch (ApiStatusException ex)
        {
            throw new McpException(StopDetailOf(ex) ?? ex.Message);
        }
        catch (Exception ex) when (ex is ApiNotFoundException or ApiUnreachableException)
        {
            throw new McpException(ex.Message);
        }
    }

    // OvermindApiClient packs the ProblemDetails detail behind "): "; the gate's STOP text must lead the agent-visible error.
    private static string? StopDetailOf(ApiStatusException ex)
    {
        var boundary = ex.Message.IndexOf("): ", StringComparison.Ordinal);
        if (boundary < 0)
        {
            return null;
        }

        var detail = ex.Message[(boundary + 3)..];
        return detail.StartsWith("STOP:", StringComparison.Ordinal) ? detail : null;
    }

    private static JsonElement LeadWithStopReason(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("state", out var state) ||
            state.ValueKind != JsonValueKind.String ||
            state.GetString() != "stop" ||
            !payload.TryGetProperty("reason", out var reason) ||
            reason.ValueKind != JsonValueKind.String)
        {
            return payload;
        }

        var reordered = new JsonObject { ["reason"] = reason.GetString() };
        foreach (var property in payload.EnumerateObject())
        {
            if (property.Name != "reason")
            {
                reordered[property.Name] = JsonNode.Parse(property.Value.GetRawText());
            }
        }

        return reordered.Deserialize<JsonElement>();
    }

    private static JsonElement CompactArtifact(JsonElement artifact)
    {
        var compact = new JsonObject
        {
            ["id"] = artifact.GetProperty("id").GetString(),
            ["kind"] = artifact.GetProperty("kind").GetString(),
            ["status"] = artifact.GetProperty("status").GetString(),
            ["version"] = artifact.GetProperty("version").GetInt32(),
            ["order"] = artifact.GetProperty("order").GetInt32(),
            ["frozenAt"] = artifact.GetProperty("frozenAt").GetString(),
            ["updatedAt"] = artifact.GetProperty("updatedAt").GetString(),
            ["bodyChars"] = artifact.GetProperty("body").GetString()?.Length ?? 0
        };

        if (artifact.TryGetProperty("brief", out var brief) && brief.ValueKind == JsonValueKind.String)
        {
            compact["brief"] = brief.GetString();
        }

        return compact.Deserialize<JsonElement>();
    }

    private static JsonElement CompactFeature(JsonElement feature)
    {
        var compact = new JsonObject
        {
            ["slug"] = feature.GetProperty("slug").GetString(),
            ["status"] = feature.GetProperty("status").GetString(),
            ["priority"] = feature.GetProperty("priority").GetString(),
            ["version"] = feature.GetProperty("version").GetInt32(),
            ["updatedAt"] = feature.GetProperty("updatedAt").GetString(),
            ["bodyChars"] = feature.GetProperty("body").GetString()?.Length ?? 0
        };
        return compact.Deserialize<JsonElement>();
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

    private static string NormalizeKind(string kind)
    {
        var trimmed = kind.Trim();
        if (!ArtifactKinds.Contains(trimmed))
        {
            throw new McpException($"Invalid artifact kind '{kind}'. Expected one of: spec, plan, task, result.");
        }

        return trimmed.ToLowerInvariant();
    }

    private static string NormalizeReview(string review)
    {
        var trimmed = review.Trim();
        if (!ReviewLevels.Contains(trimmed))
        {
            throw new McpException($"Invalid review level '{review}'. Expected one of: light, hard.");
        }

        return trimmed.ToLowerInvariant();
    }

    private static string NormalizePriority(string priority)
    {
        var trimmed = priority.Trim();
        if (!FeaturePriorities.Contains(trimmed))
        {
            throw new McpException($"Invalid feature priority '{priority}'. Expected one of: p0, p1, p2, p3.");
        }

        return trimmed.ToLowerInvariant();
    }

    private static string NormalizeStatus(string status)
    {
        if (!ArtifactStatuses.TryGetValue(status.Trim(), out var normalized))
        {
            throw new McpException(
                $"Invalid artifact status '{status}'. Expected one of: draft, validated, todo, inProgress, review, done, blocked.");
        }

        return normalized;
    }

    private static string NormalizeFeatureStatus(string status)
    {
        if (!AgentFeatureStatuses.TryGetValue(status.Trim(), out var normalized))
        {
            throw new McpException(
                $"Invalid feature status '{status}' for the agent door. Allowed targets: done (close, from any non-done status) or inProgress (review rollback); every other manual move is human-door only in the Overmind UI.");
        }

        return normalized;
    }

    private static string PatchOpOf(string? section, string? content, int? acIndex, string? acText)
    {
        var wantsSection = section is not null || content is not null;
        var wantsTick = acIndex is not null || acText is not null;

        if (wantsSection == wantsTick)
        {
            throw new McpException(PatchShapeError);
        }

        if (wantsSection)
        {
            if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(content))
            {
                throw new McpException("Replacing a section needs a non-blank section and its full replacement content.");
            }

            return "replace_section";
        }

        if (acIndex is not null && acText is not null)
        {
            throw new McpException("Tick one acceptance-criteria item by acIndex or by exact acText, not both.");
        }

        if (acText is not null && string.IsNullOrWhiteSpace(acText))
        {
            throw new McpException("Ticking needs acIndex or the exact non-blank acText of the item.");
        }

        return "tick_ac";
    }
}
