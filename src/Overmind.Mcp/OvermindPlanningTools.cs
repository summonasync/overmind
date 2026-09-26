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

    private static readonly HashSet<string> QueueMoves = new(StringComparer.OrdinalIgnoreCase)
    {
        "up", "down", "first", "last", "toPosition"
    };

    private const string StatusNoteRequired =
        "logNote is required for status moves: every status change is recorded as a dated line in the artifact <log>.";

    private const string FeatureStatusNoteRequired =
        "logNote is required for feature status moves: the note is recorded as a dated line in the feature log and carries the provenance (who asked for the move).";

    private const string PatchShapeError =
        "A patch carries either a section with its replacement content, or an acceptance-criteria tick (acIndex or exact acText) — not both and not neither.";

    private const string MigratedNoteRequired =
        "logNote is required with asMigrated: the migration marker must record where the pre-approval came from.";

    private const string DoorSource = "agent";

    private const int SkipHeadLimit = 5;

    private readonly OvermindApiClient _api;

    public OvermindPlanningTools(OvermindApiClient api)
    {
        _api = api;
    }

    [McpServerTool(Name = "planning_list_features", ReadOnly = true, Idempotent = true)]
    [Description("Planning ceremony, step 1: the feature board of a project — slugs, statuses, spec/plan validation and task progress. Read it before picking work. This is the full board view, not the work order: for what's next read planning_task_queue — the board order IS the queue, and that tool is its projection.")]
    public Task<JsonElement> ListFeatures(
        [Description("Id of the project whose feature board to read.")] Guid projectId,
        CancellationToken ct) =>
        Guarded(() => _api.GetAsync($"/api/agent/projects/{projectId}/planning/features", ct));

    [McpServerTool(Name = "planning_read_feature", ReadOnly = true, Idempotent = true)]
    [Description("Reads one feature in full: its body plus every spec, plan, task and result artifact with statuses and log lines — the most expensive read: the whole feature with all artifacts; prefer planning_read_artifact for one document. The review field appears on task payloads only.")]
    public async Task<JsonElement> ReadFeature(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature, as shown on the board.")] string slug,
        CancellationToken ct)
    {
        var feature = await Guarded(() => _api.GetAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}", ct));
        return KeepReviewOnlyOnTaskArtifacts(feature);
    }

    [McpServerTool(Name = "planning_list_artifacts", ReadOnly = true, Idempotent = true)]
    [Description("Metadata-only roster of one feature's artifacts — the cheapest way to see what a feature contains; planning_read_artifact for one body, planning_read_feature for the whole feature.")]
    public Task<JsonElement> ListArtifacts(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature, as shown on the board.")] string slug,
        [Description("Optional artifact kind filter: spec, plan, task or result — the server validates the value and rejects unknown ones with a 400.")] string? kind = null,
        CancellationToken ct = default)
    {
        var url = $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}/artifacts";
        if (!string.IsNullOrWhiteSpace(kind))
        {
            url += $"?kind={Uri.EscapeDataString(kind)}";
        }

        return Guarded(() => _api.GetAsync(url, ct));
    }

    [McpServerTool(Name = "planning_read_artifact", ReadOnly = true, Idempotent = true)]
    [Description("Reads one artifact by id — without sections, one artifact, full body; with sections, the cheapest read: only the named sections. missingSections names requested-but-absent sections; a read never fails on a missing one. The review field appears on task payloads only.")]
    public async Task<JsonElement> ReadArtifact(
        [Description("Id of the artifact to read, as handed out by planning_current_task.")] Guid artifactId,
        [Description("Optional comma/space-separated section names, e.g. log,obstacles — narrows the body to those sections.")] string? sections = null,
        CancellationToken ct = default)
    {
        var url = $"/api/agent/planning/artifacts/{artifactId}";
        if (!string.IsNullOrWhiteSpace(sections))
        {
            url += $"?sections={Uri.EscapeDataString(sections)}";
        }

        var artifact = await Guarded(() => _api.GetAsync(url, ct));
        return KeepReviewOnlyOnTasks(artifact);
    }

    private static JsonElement KeepReviewOnlyOnTaskArtifacts(JsonElement feature)
    {
        if (feature.ValueKind != JsonValueKind.Object ||
            !feature.TryGetProperty("artifacts", out var artifacts) ||
            artifacts.ValueKind != JsonValueKind.Array)
        {
            return feature;
        }

        var node = JsonNode.Parse(feature.GetRawText())!.AsObject();
        foreach (var artifact in node["artifacts"]!.AsArray())
        {
            if (artifact?["kind"]?.GetValue<string>().Equals("task", StringComparison.OrdinalIgnoreCase) != true)
            {
                artifact!.AsObject().Remove("review");
            }
        }

        return node.Deserialize<JsonElement>();
    }

    private static JsonElement KeepReviewOnlyOnTasks(JsonElement artifact)
    {
        if (artifact.ValueKind != JsonValueKind.Object ||
            artifact.GetProperty("kind").GetString()?.Equals("task", StringComparison.OrdinalIgnoreCase) == true)
        {
            return artifact;
        }

        var node = JsonNode.Parse(artifact.GetRawText())!.AsObject();
        node.Remove("review");
        return node.Deserialize<JsonElement>();
    }

    [McpServerTool(Name = "planning_current_task", ReadOnly = true, Idempotent = true)]
    [Description("Planning ceremony, step 2: the current task of a feature, or a stop/held/empty state whose reason leads the payload. Obey a STOP line — ask the user, do not work around it. A feature with unclosed blockers returns a stop state naming them — reads stay open, task issuance does not. Held means every remaining task sits in review; the all-done states tell you to write the result artifact, validate a drafted one, or close the feature after validation; task payloads carry soft spec/plan warnings (never STOP).")]
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
    [Description("Creates a new feature on the planning board, optionally with a priority (p0 critical, p1 high, p2 normal — the default, p3 low). Follow with planning_upsert_artifact to write its spec. Features cannot be deleted or archived via MCP — the UI owns feature deletion.")]
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
    [Description("Creates an artifact for a feature, or updates the existing artifact of the same kind and order as a new version — the most expensive write: resends the full body, so prefer planning_patch_artifact for one XML section or one acceptance item and planning_append_log for a log line. Artifacts are created draft; validation goes through planning_validate. Editing a validated artifact requires a logNote — not needed on create. Expected XML sections — task: goal, scope, anti-scope, constraints, acceptance-criteria, refs, obstacles, log; spec/plan/result: free-form markdown with <log> as the only machine-parsed section. Order is a sort key within the feature — lowest order first; it drives planning_current_task picking; omitted order appends after the current max (first append lands on 0; materialized backlog files are numbered 1-based). Task artifacts also take a brief: a one-line summary (max 500 chars) shown next to the task; other kinds reject it. Features cannot be deleted or archived via MCP — the UI owns feature deletion.")]
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
            ["logNote"] = logNote,
            ["source"] = DoorSource
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

    [McpServerTool(Name = "planning_upsert_tasks", Destructive = false, Idempotent = false)]
    [Description("Creates or updates N task artifacts of a feature in one call — the batch counterpart of planning_upsert_artifact, for decomposing a plan into tasks. Atomic: all-or-nothing — items are staged and committed together, and any failing item rolls back the whole batch, so a retry starts clean; a failing item is reported with its index (tasks[2]: …). Exactly one materialization sync and one feature auto-transition evaluation run after the batch, never per item — creating tasks while the feature is in review reopens it once. Each item carries the single-upsert fields: body (required), order, review light/hard, brief (task-only, one line, max 500 chars), and logNote under the same rules as planning_upsert_artifact — optional, required only when the item edits an existing body that demands one. Order is a sort key within the feature — lowest order first; it drives planning_current_task picking; omitted order appends after the current max (first append lands on 0; materialized backlog files are numbered 1-based). An item whose order hits an existing task updates that task in place.")]
    public async Task<JsonElement> UpsertTasks(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature to create or update tasks in.")] string featureSlug,
        [Description("Array of task items, each an object: body (required), order, review, logNote, brief.")] JsonElement tasks,
        CancellationToken ct = default)
    {
        var payload = new JsonObject
        {
            ["tasks"] = BuildTaskItems(tasks)
        };

        var batch = await Guarded(() => _api.PostAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(featureSlug)}/artifacts/tasks",
            payload,
            ct));
        return CompactTaskBatch(batch);
    }

    private static JsonArray BuildTaskItems(JsonElement tasks)
    {
        if (tasks.ValueKind != JsonValueKind.Array)
        {
            throw new McpException("tasks must be an array of task items.");
        }

        if (tasks.GetArrayLength() == 0)
        {
            throw new McpException("tasks must carry at least one item.");
        }

        var items = new JsonArray();
        var index = 0;

        foreach (var item in tasks.EnumerateArray())
        {
            try
            {
                items.Add(BuildTaskItem(item));
            }
            catch (McpException ex)
            {
                throw new McpException($"tasks[{index}]: {ex.Message}");
            }

            index++;
        }

        return items;
    }

    private static JsonObject BuildTaskItem(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw new McpException("each item must be an object with body, order, review, logNote, brief.");
        }

        if (!item.TryGetProperty("body", out var body) ||
            body.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(body.GetString()))
        {
            throw new McpException("body is required and must be a non-blank string.");
        }

        var payload = new JsonObject { ["body"] = body.GetString() };

        if (item.TryGetProperty("order", out var order) && order.ValueKind is JsonValueKind.Number)
        {
            if (!order.TryGetInt32(out var orderValue))
            {
                throw new McpException("order must be an integer.");
            }

            payload["order"] = orderValue;
        }

        if (item.TryGetProperty("review", out var review) && review.ValueKind == JsonValueKind.String)
        {
            payload["review"] = NormalizeReview(review.GetString()!);
        }

        if (item.TryGetProperty("logNote", out var logNote) && logNote.ValueKind == JsonValueKind.String)
        {
            payload["logNote"] = logNote.GetString();
        }

        if (item.TryGetProperty("brief", out var brief) && brief.ValueKind == JsonValueKind.String)
        {
            payload["brief"] = brief.GetString();
        }

        return payload;
    }

    private static JsonElement CompactTaskBatch(JsonElement batch)
    {
        var tasks = new JsonArray();
        foreach (var artifact in batch.GetProperty("artifacts").EnumerateArray())
        {
            var compact = new JsonObject
            {
                ["id"] = artifact.GetProperty("id").GetString(),
                ["order"] = artifact.GetProperty("order").GetInt32(),
                ["status"] = artifact.GetProperty("status").GetString(),
                ["version"] = artifact.GetProperty("version").GetInt32(),
                ["bodyChars"] = artifact.GetProperty("body").GetString()?.Length ?? 0
            };

            if (artifact.TryGetProperty("brief", out var brief) && brief.ValueKind == JsonValueKind.String)
            {
                compact["brief"] = brief.GetString();
            }

            tasks.Add(compact);
        }

        var result = new JsonObject
        {
            ["created"] = batch.GetProperty("created").GetInt32(),
            ["updated"] = batch.GetProperty("updated").GetInt32(),
            ["tasks"] = tasks
        };
        return result.Deserialize<JsonElement>();
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

        return MutationEcho(payload, "id");
    }

    [McpServerTool(Name = "planning_set_current", Destructive = false, Idempotent = true)]
    [Description("Pins a feature as the current one on the planning board, or unpins it. Pinning one feature unpins any other — at most one feature is current per project. Pinning a feature with unclosed blockers is refused with a STOP error naming them; the human door in the Overmind UI is never blocked.")]
    public async Task<CallToolResult> SetCurrent(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature to pin or unpin, as shown on the board.")] string slug,
        [Description("True to pin the feature as current; false to unpin it.")] bool isCurrent,
        CancellationToken ct = default)
    {
        try
        {
            var echo = await Guarded(() => _api.SetPlanningFeatureCurrent(projectId, slug, isCurrent, ct));
            return EchoResult(MutationEcho(echo, "slug"));
        }
        catch (McpException ex) when (ex.Message.StartsWith("STOP:", StringComparison.Ordinal))
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = ex.Message }]
            };
        }
    }

    [McpServerTool(Name = "planning_set_feature_status", Destructive = false, Idempotent = false)]
    [Description("Moves a feature status on the planning board — only on the user's explicit request in the current session, never on the agent's own initiative. Allowed targets: done (close, from any non-done status) or inProgress (review rollback); every other manual move belongs to the human door in the Overmind UI. The logNote is mandatory, lands as a dated line in the feature log, and must carry the provenance, e.g. 'by user request in-session'.")]
    public async Task<JsonElement> SetFeatureStatus(
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
        var echo = await Guarded(() => _api.PostAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}/status",
            new { status = normalized, logNote },
            ct));
        return MutationEcho(echo, "slug");
    }

    [McpServerTool(Name = "planning_task_queue", ReadOnly = true, Idempotent = true)]
    [Description("Reads the planning queue of a project — the what's-next projection of the board: non-done features sorted priority asc, then queue position; done features drop out. The board order IS the queue: first on the board is next in the queue, and the dashboard board shows exactly this order. The human reorders it by dragging cards within a priority band; the agent moves features with planning_move_feature, and crossing a band is planning_set_feature_priority with the user's explicit approval. Entries are compact: slug, number, title, priority, status, queuePosition, specStatus, planStatus, taskDone, taskTotal, readiness 0-3, blockedBy, isBlocked, isCurrent. limit truncates to the first N entries; omit it for the full queue.")]
    public Task<JsonElement> TaskQueue(
        [Description("Id of the project whose queue to read.")] Guid projectId,
        [Description("Optional positive int; truncates the queue to the first N entries.")] int? limit = null,
        CancellationToken ct = default)
    {
        var url = $"/api/agent/projects/{projectId}/planning/task-queue";
        if (limit is not null)
        {
            url += $"?limit={limit}";
        }

        return Guarded(() => _api.GetAsync(url, ct));
    }

    [McpServerTool(Name = "planning_move_feature", Destructive = false, Idempotent = false)]
    [Description("Moves a feature within its priority band on the planning queue — band-scoped only; crossing bands means planning_set_feature_priority. Moves: 'up' and 'down' swap with the adjacent non-done feature (no-op at the band edges), 'first' and 'last' jump to the band head or tail, 'toPosition' takes the absolute 1-based spot in position. The server resolves neighbors and renumbers, so no band-wide read is needed. Contract violations (unknown move, position only for toPosition, out-of-range) return error text naming the contract; the human reorders by dragging on the dashboard board.")]
    public async Task<CallToolResult> MoveFeature(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature to move, as shown on the board.")] string slug,
        [Description("One of: up, down, first, last, toPosition.")] string move,
        [Description("1-based spot in the feature's priority band; required only when move is toPosition.")] int? position = null,
        CancellationToken ct = default)
    {
        var contract = QueueMoveContractError(move, position);
        if (contract is not null)
        {
            return ContractViolation(contract);
        }

        JsonElement moved;
        try
        {
            moved = await Guarded(() => _api.PostAsync(
                $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}/queue-move",
                new { move = move.Trim().ToLowerInvariant(), position },
                ct));
        }
        catch (McpException ex)
        {
            return ContractViolation(DetailAfterOperationMark(ex.Message) ?? ex.Message);
        }

        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = $"Feature '{slug}' is now at queue position {moved.GetProperty("queuePosition").GetInt32()} " +
                        $"in band '{moved.GetProperty("priority").GetString()}'."
                }
            ]
        };
    }

    [McpServerTool(Name = "planning_set_feature_priority", Destructive = false, Idempotent = false)]
    [Description("Changes a feature priority (p0 critical, p1 high, p2 normal, p3 low) — the feature re-enters at the tail of the new band. Band crossings are the weightier act: only on the user's explicit approval in the current session, never on the agent's own initiative. Moves within one band belong to planning_move_feature or the human board drag.")]
    public async Task<JsonElement> SetFeaturePriority(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature to re-prioritize, as shown on the board.")] string slug,
        [Description("New priority: p0 (critical), p1 (high), p2 (normal), p3 (low).")] string priority,
        CancellationToken ct = default)
    {
        var normalized = NormalizePriority(priority);
        var feature = await Guarded(() => _api.PutAsync(
            $"/api/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}",
            new { priority = normalized },
            ct));
        return CompactFeature(feature);
    }

    [McpServerTool(Name = "planning_list_feature_dependencies", ReadOnly = true, Idempotent = true)]
    [Description("Lists the dependency graph of a feature: blockedBy (features this one waits for) and blocks (features waiting on this one), each entry carrying slug, status and closed — a dependency closes when its blocker reaches done or review. An unclosed blockedBy stops planning_current_task and planning_set_current.")]
    public Task<JsonElement> ListFeatureDependencies(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature, as shown on the board.")] string slug,
        CancellationToken ct = default) =>
        Guarded(() => _api.GetAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}/dependencies", ct));

    [McpServerTool(Name = "planning_add_feature_dependency", Destructive = false, Idempotent = false)]
    [Description("Adds a blocker to a feature: the feature stops issuing tasks through planning_current_task until the blocker closes (done or review). Rejects self-reference and any edge that completes a dependency cycle, naming the chain. Same-project only.")]
    public Task<JsonElement> AddFeatureDependency(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature that gains the blocker.")] string slug,
        [Description("Slug of the blocking feature, as shown on the board.")] string blockedBySlug,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(blockedBySlug))
        {
            throw new McpException("blockedBySlug is required: the feature slug of the blocker, as shown on the board.");
        }

        return Guarded(() => _api.PostAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}/dependencies",
            new { blockedBy = blockedBySlug.Trim() },
            ct));
    }

    [McpServerTool(Name = "planning_remove_feature_dependency", Destructive = false, Idempotent = false)]
    [Description("Removes one blocker from a feature; removing a link that is not there is a no-op. Lifting the last unclosed blocker unblocks the feature for planning_current_task and planning_set_current.")]
    public Task<JsonElement> RemoveFeatureDependency(
        [Description("Id of the project the feature belongs to.")] Guid projectId,
        [Description("Slug of the feature whose blocker is removed.")] string slug,
        [Description("Slug of the blocker to remove.")] string blockedBySlug,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(blockedBySlug))
        {
            throw new McpException("blockedBySlug is required: the feature slug of the blocker to remove.");
        }

        return Guarded(() => _api.DeleteAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}/dependencies" +
            $"/{Uri.EscapeDataString(blockedBySlug.Trim())}",
            ct));
    }

    [McpServerTool(Name = "planning_append_log", Destructive = false, Idempotent = false)]
    [Description("Planning ceremony, step 3 companion: appends a dated line to an artifact's log without touching its body — the cheapest write: one dated line, body is not resent. Use it to report progress between status moves.")]
    public async Task<JsonElement> AppendLog(
        [Description("Id of the artifact to append the note to.")] Guid artifactId,
        [Description("Note text recorded as a dated log line.")] string note,
        CancellationToken ct = default)
    {
        var echo = await Guarded(() => _api.PostAsync(
            $"/api/agent/planning/artifacts/{artifactId}/log",
            new { note, source = DoorSource },
            ct));
        return MutationEcho(echo, "id");
    }

    private const string AlreadyTickedNoChangeText = "already ticked — no change";

    [McpServerTool(Name = "planning_patch_artifact", Destructive = false, Idempotent = false)]
    [Description("Changes one artifact in place without resending its body — the middle tier: replaces one XML section or ticks one acceptance item, rest of the body untouched. Give section plus content to replace one section, or acIndex/acText to tick one acceptance-criteria item; never both. A missing section or item is a 400 that lists what is available. The log section is append-only: patching it is a 400 that points at planning_append_log, and history is rewritten only through a full upsert. Ticking an already-ticked item is a successful no-op rendering 'already ticked — no change'; a replaced section is a real change every time. Editing a validated artifact requires a logNote.")]
    public async Task<CallToolResult> PatchArtifact(
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
            new { op, section, content, acIndex, acText, logNote, source = DoorSource },
            ct));

        if (patch.TryGetProperty("alreadyTicked", out var alreadyTicked) &&
            alreadyTicked.ValueKind == JsonValueKind.True)
        {
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = AlreadyTickedNoChangeText }]
            };
        }

        var echo = new JsonObject
        {
            ["id"] = patch.GetProperty("artifactId").GetString(),
            ["version"] = patch.GetProperty("version").GetInt32(),
            ["status"] = patch.GetProperty("status").GetString(),
            ["lastLogLine"] = patch.GetProperty("lastLogLine").GetString(),
            ["section"] = patch.GetProperty("section").GetString(),
            ["bodyChars"] = patch.GetProperty("bodyChars").GetInt32()
        };

        if (patch.TryGetProperty("statusChanged", out var statusChanged) &&
            statusChanged.ValueKind == JsonValueKind.String)
        {
            echo["statusChanged"] = statusChanged.GetString();
        }

        if (patch.TryGetProperty("featureStatusChanged", out var featureStatusChanged) &&
            featureStatusChanged.ValueKind == JsonValueKind.String)
        {
            echo["featureStatusChanged"] = featureStatusChanged.GetString();
        }

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = echo.ToJsonString() }]
        };
    }

    [McpServerTool(Name = "planning_validate", Destructive = false, Idempotent = false)]
    [Description("Validates a spec, plan or result artifact. When the project requires human validation this fails with a STOP error — ask the user to approve it in the Overmind UI instead of retrying. Validating a spec or plan here requires human-approval proof: send overmind_notify(kind=approval, featureSlug=<the feature's slug>), wait for the human's reply in the notification center, then validate (notify → reply → validate). The reply must be newer than the artifact's last update — every edit voids it, and re-validation needs a fresh reply; without proof the call fails with a ValidationProofRequired 409. The human door (UI) needs no proof, and neither does asMigrated — the migration escape hatch for artifacts pre-approved before import (e.g. already validated on disk): it skips the human gate, requires a logNote carrying the provenance, and leaves a 'validated as migrated — pre-approved on disk' marker line in the artifact log.")]
    public async Task<CallToolResult> Validate(
        [Description("Id of the artifact to validate.")] Guid artifactId,
        [Description("True only for artifacts pre-approved before migration (e.g. already validated on disk); skips the human gate and leaves a loud migration marker in the log.")] bool? asMigrated = null,
        [Description("Required with asMigrated: provenance of the pre-approval, e.g. 'validated v9 on disk, user-approved'; recorded inside the marker line.")] string? logNote = null,
        CancellationToken ct = default)
    {
        if (asMigrated == true && string.IsNullOrWhiteSpace(logNote))
        {
            throw new McpException(MigratedNoteRequired);
        }

        try
        {
            var echo = await Guarded(() => _api.PostAsync(
                $"/api/agent/planning/artifacts/{artifactId}/validate",
                new { asMigrated, logNote },
                ct));
            return EchoResult(MutationEcho(echo, "id"));
        }
        catch (McpException ex) when (ex.Message.StartsWith("STOP:", StringComparison.Ordinal))
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = ex.Message }]
            };
        }
    }

    [McpServerTool(Name = "planning_import_feature", Destructive = false, Idempotent = false)]
    [Description("Imports one materialized feature folder from disk in a single call — bodies never pass through the model. Give the absolute path of the folder that holds feature.md, under the project root (e.g. /repo/BACKLOG/058-agent-side-import); spec.md, plan.md, result.md and tasks/NN-*.md are picked up by convention, statuses are restored verbatim with [import] markers in the logs, and existing artifacts are re-imported as new versions. Relative paths and paths outside the project root are rejected. The response reports created vs updated per feature and artifact plus per-file notes.")]
    public Task<JsonElement> ImportFeature(
        [Description("Id of the project to import the feature into.")] Guid projectId,
        [Description("Absolute path of the feature folder under the project root — the directory containing feature.md.")] string path,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new McpException("path is required: the feature folder to import, e.g. /repo/BACKLOG/058-agent-side-import.");
        }

        return Guarded(() => _api.PostAsync(
            $"/api/agent/projects/{projectId}/planning/import",
            new { path = path.Trim() },
            ct));
    }

    [McpServerTool(Name = "planning_materialize", Destructive = false, Idempotent = false)]
    [Description("Triggers a materialization pass: the full plan is computed first, then written atomically — any guarded file (untracked at a planned path, or tracked with local edits not on the board) aborts the whole run with a 409 listing every refusal, and nothing is written that run. Skips in the report are byte-identical no-ops (content already matches the board), not guarded files. A normal board mutation already triggers a pass automatically; call this after an import, a strategy switch, or when onboard reports a failed attempt or stale mirror state. force adopts guarded files with per-file warnings instead of refusing; transcribe hand edits to the board before forcing.")]
    public async Task<JsonElement> Materialize(
        [Description("Id of the project to materialize.")] Guid projectId,
        [Description("True to overwrite guarded files with per-file warnings instead of refusing them; transcribe hand edits to the board before forcing.")] bool force = false,
        CancellationToken ct = default)
    {
        var report = await Guarded(() => _api.PostAsync(
            $"/api/projects/{projectId}/planning/materialize",
            new { force },
            ct));
        return CompactMaterialization(report, force);
    }

    [McpServerTool(Name = "planning_materialize_status", ReadOnly = true, Idempotent = true)]
    [Description("Mirror diagnostics without filesystem access: returns the project's materialization state as one structured payload — lastSuccess vs lastAttempt (lastAttemptReason carries the failure when the attempt did not succeed), the persisted last-run report per file (startedAt, finishedAt, atomic, written paths, skipped and refused with reasons), trackedFiles, stalePaths (tracked files the last run neither wrote nor skipped, plus tracked files whose disk bytes drifted), strategy and targetDir. lastRun is null when the failed attempt persisted no report — read it as no report for this attempt. Read-only: never triggers a pass; use planning_materialize for that.")]
    public Task<JsonElement> MaterializeStatus(
        [Description("Id of the project to inspect.")] Guid projectId,
        CancellationToken ct = default) =>
        Guarded(() => _api.GetAsync(
            $"/api/agent/projects/{projectId}/planning/materialization-status",
            ct));

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
        var detail = DetailAfterOperationMark(ex.Message);
        return detail is not null && detail.StartsWith("STOP:", StringComparison.Ordinal) ? detail : null;
    }

    private static string? DetailAfterOperationMark(string message)
    {
        var boundary = message.IndexOf("): ", StringComparison.Ordinal);
        return boundary < 0 ? null : message[(boundary + 3)..];
    }

    private static CallToolResult ContractViolation(string text) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = text }]
    };

    private static string? QueueMoveContractError(string? move, int? position)
    {
        if (string.IsNullOrWhiteSpace(move))
        {
            return "move is required: one of up, down, first, last, toPosition. position carries the 1-based spot and belongs to toPosition alone.";
        }

        var gesture = move.Trim();
        if (!QueueMoves.Contains(gesture))
        {
            return $"Invalid queue move '{move}'. Expected one of: up, down, first, last, toPosition. " +
                "position carries the 1-based spot in the priority band and belongs to toPosition alone.";
        }

        if (string.Equals(gesture, "toPosition", StringComparison.OrdinalIgnoreCase))
        {
            return position switch
            {
                null => "position is required when move is 'toPosition': the 1-based spot in the feature's priority band. " +
                    "The other moves resolve the spot themselves.",
                < 1 => $"position must be a positive int (1-based), got {position}.",
                _ => null
            };
        }

        return position is null
            ? null
            : "position is only allowed when move is 'toPosition'; omit it for up, down, first and last — " +
                "the server swaps with the adjacent non-done feature or jumps to the band edges itself.";
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

    private static JsonElement MutationEcho(JsonElement payload, string idKey)
    {
        var echo = new JsonObject
        {
            ["id"] = payload.GetProperty(idKey).GetString(),
            ["version"] = payload.GetProperty("version").GetInt32(),
            ["status"] = payload.GetProperty("status").GetString(),
            ["lastLogLine"] = payload.GetProperty("lastLogLine").GetString()
        };

        if (payload.TryGetProperty("warnings", out var warnings) &&
            warnings.ValueKind == JsonValueKind.Array &&
            warnings.GetArrayLength() > 0)
        {
            echo["warnings"] = JsonNode.Parse(warnings.GetRawText());
        }

        if (payload.TryGetProperty("featureStatusChanged", out var featureStatusChanged) &&
            featureStatusChanged.ValueKind == JsonValueKind.String)
        {
            echo["featureStatusChanged"] = featureStatusChanged.GetString();
        }

        return echo.Deserialize<JsonElement>();
    }

    private static CallToolResult EchoResult(JsonElement echo) =>
        new() { Content = [new TextContentBlock { Text = echo.GetRawText() }] };

    private static string LastLogLineOf(JsonElement artifact)
    {
        if (!artifact.TryGetProperty("log", out var log) ||
            log.ValueKind != JsonValueKind.Array ||
            log.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var last = log[log.GetArrayLength() - 1];
        return last.ValueKind == JsonValueKind.Object &&
            last.TryGetProperty("text", out var text) &&
            text.ValueKind == JsonValueKind.String
            ? text.GetString() ?? string.Empty
            : string.Empty;
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
            ["bodyChars"] = artifact.GetProperty("body").GetString()?.Length ?? 0,
            ["lastLogLine"] = LastLogLineOf(artifact)
        };

        if (artifact.TryGetProperty("brief", out var brief) && brief.ValueKind == JsonValueKind.String)
        {
            compact["brief"] = brief.GetString();
        }

        if (artifact.TryGetProperty("statusChanged", out var statusChanged) &&
            statusChanged.ValueKind == JsonValueKind.String)
        {
            compact["statusChanged"] = statusChanged.GetString();
        }

        if (artifact.TryGetProperty("featureStatusChanged", out var featureStatusChanged) &&
            featureStatusChanged.ValueKind == JsonValueKind.String)
        {
            compact["featureStatusChanged"] = featureStatusChanged.GetString();
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
