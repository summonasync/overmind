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

    private const string StatusNoteRequired =
        "logNote is required for status moves: every status change is recorded as a dated line in the artifact <log>.";

    private const string FeatureStatusNoteRequired =
        "logNote is required for feature status moves: the note is recorded as a dated line in the feature log and carries the provenance (who asked for the move).";

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
    [Description("Reads one feature in full: its body plus every spec, plan, task and result artifact with statuses and log lines.")]
    public Task<JsonElement> ReadFeature(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature, as shown on the board.")] string slug,
        CancellationToken ct) =>
        Guarded(() => _api.GetAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}", ct));

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
    [Description("Creates a new feature on the planning board. Follow with planning_upsert_artifact to write its spec.")]
    public Task<JsonElement> CreateFeature(
        [Description("Id of the project to create the feature in.")] Guid projectId,
        [Description("Slug of the new feature: lowercase alphanumeric with hyphens, max 64 chars.")] string slug,
        [Description("Human-readable feature title.")] string title,
        [Description("Optional markdown body of the feature document (goal, requirements, non-goals).")] string? body = null,
        CancellationToken ct = default) =>
        Guarded(() => _api.PostAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { slug, title, body },
            ct));

    [McpServerTool(Name = "planning_upsert_artifact", Destructive = false, Idempotent = false)]
    [Description("Creates an artifact for a feature, or updates the existing artifact of the same kind and order as a new version. Editing a validated artifact requires a logNote. Task artifacts also take a brief: a one-line summary (max 500 chars) shown next to the task; other kinds reject it.")]
    public Task<JsonElement> UpsertArtifact(
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

        return Guarded(() => _api.PostAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(featureSlug)}/artifacts",
            payload,
            ct));
    }

    [McpServerTool(Name = "planning_set_status", Destructive = false, Idempotent = false)]
    [Description("Planning ceremony, step 3: moves an artifact to a new status and records why in the artifact log. The logNote is mandatory — an unexplained status move is rejected.")]
    public async Task<string> SetStatus(
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
        await Guarded(() => _api.PostAsync(
            $"/api/agent/planning/artifacts/{artifactId}/status",
            new { status = normalized, logNote },
            ct));
        return $"Artifact status moved to '{normalized}'.";
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
    [Description("Planning ceremony, step 3 companion: appends a dated line to an artifact's log without touching its body. Use it to report progress between status moves.")]
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

    [McpServerTool(Name = "planning_validate", Destructive = false, Idempotent = false)]
    [Description("Validates a spec, plan or result artifact. When the project requires human validation this fails with a STOP error — ask the user to approve it in the Overmind UI instead of retrying.")]
    public async Task<CallToolResult> Validate(
        [Description("Id of the artifact to validate.")] Guid artifactId,
        CancellationToken ct)
    {
        try
        {
            await Guarded(() => _api.PostAsync(
                $"/api/agent/planning/artifacts/{artifactId}/validate",
                new { },
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

        return new CallToolResult { Content = [new TextContentBlock { Text = "Artifact validated." }] };
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
}
