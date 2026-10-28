using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Overmind.Mcp;

namespace Overmind.Mcp.Planning;

using static Overmind.Mcp.Planning.PlanningToolResponses;

[McpServerToolType]
public sealed class PlanningFeatureTools
{
    private const int ClaimIdentityCap = 32;

    private readonly OvermindApiClient _api;
    private readonly MutationGuard _mutations;
    private readonly ProjectResolver _projects;

    public PlanningFeatureTools(OvermindApiClient api, MutationGuard mutations, ProjectResolver projects)
    {
        _api = api;
        _mutations = mutations;
        _projects = projects;
    }

    [McpServerTool(Name = "planning_list_features", ReadOnly = true, Idempotent = true)]
    [Description("Planning ceremony, step 1: the feature board of a project — slugs, statuses, spec/plan validation and task progress (taskTotal, taskDone, taskCounts: todo/inProgress/review/done/blocked, sums to taskTotal), plus claim state (claimedByAlias, claimedByVendor, claimedAt, claimState alive|stale|ended) on claimed entries — a stale or ended claim can be taken over via planning_claim_feature. Read it before picking work. This is the full board view, not the work order: for what's next read planning_task_queue — the board order IS the queue, and that tool is its projection.")]
    public async Task<JsonElement> ListFeatures(
        [Description("Id of the project whose feature board to read; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);

        return await Guarded(() => _api.GetAsync($"/api/agent/projects/{resolved}/planning/features", ct));
    }

    [McpServerTool(Name = "planning_read_feature", ReadOnly = true, Idempotent = true)]
    [Description("Reads one feature in full: its body plus every spec, plan, task and result artifact with statuses and log lines — the most expensive read: the whole feature with all artifacts; prefer planning_read_artifact for one document. The payload carries the claim state when claimed: claimedByAlias, claimedByVendor, claimedAt, claimState (alive | stale | ended). The review field appears on task payloads only. includeArtifacts=false is the cheap read: body plus a roster summary without artifact bodies or logs.")]
    public async Task<JsonElement> ReadFeature(
        [Description("Slug of the feature, as shown on the board.")] string slug,
        [Description("Set false for the cheap read: the feature body plus a roster summary (kind, order, status, brief for tasks, acTicked/acTotal) without artifact bodies or log lines. Default true returns every artifact in full.")] bool? includeArtifacts = null,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var url = $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}";
        if (includeArtifacts == false)
        {
            url += "?includeArtifacts=false";
        }

        var feature = await Guarded(() => _api.GetAsync(url, ct));
        return KeepReviewOnlyOnTaskArtifacts(feature);
    }

    [McpServerTool(Name = "planning_list_artifacts", ReadOnly = true, Idempotent = true)]
    [Description("Metadata-only roster of one feature's artifacts — the cheapest way to see what a feature contains; planning_read_artifact for one body, planning_read_feature for the whole feature. Every row carries additive pointers: [{path, anchor}] parsed from pointer lines in the body — an artifact can point at a repo file instead of copying it; the server never reads the targets (empty array when none).")]
    public async Task<JsonElement> ListArtifacts(
        [Description("Slug of the feature, as shown on the board.")] string slug,
        [Description("Optional artifact kind filter: spec, plan, task or result — the server validates the value and rejects unknown ones with a 400.")] string? kind = null,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var url = $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/artifacts";
        if (!string.IsNullOrWhiteSpace(kind))
        {
            url += $"?kind={Uri.EscapeDataString(kind)}";
        }

        return await Guarded(() => _api.GetAsync(url, ct));
    }

    [McpServerTool(Name = "planning_current_task", ReadOnly = true, Idempotent = true)]
    [Description("Planning ceremony, step 2: the current task of a feature, or a stop/held/empty state whose reason leads the payload. Obey a STOP line — ask the user, do not work around it. A feature with unclosed blockers returns a stop state naming them — reads stay open, task issuance does not. Held means every remaining task sits in review; the all-done states tell you to write the result artifact, validate a drafted one, or close the feature after validation; task payloads carry soft spec/plan warnings (never STOP). Without a validated plan the feature never auto-promotes to review — flip it manually via planning_set_feature_status.")]
    public async Task<JsonElement> CurrentTask(
        [Description("Slug of the feature to take the current task from.")] string slug,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var payload = await Guarded(() => _api.GetAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/current", ct));
        return LeadWithStopReason(payload);
    }

    [McpServerTool(Name = "planning_create_feature", Destructive = false, Idempotent = false)]
    [Description("Creates a new feature on the planning board, optionally with a priority (p0 critical, p1 high, p2 normal — the default, p3 low). slug is optional: omit it or pass a blank one and the Host generates {next free NNN}-{title-derived slug} — lowercase ASCII derived from the title (Cyrillic transliterated), non-alphanumerics collapsed to single dashes, the slug part shrinking so the total never exceeds 64 while the number survives; an explicit slug always wins. The slug's NNN number prefix must be free: any feature holding that number (numeric compare, 7 = 007) earns a 409 NumberCollision whose detail names the colliding feature and suggests the next free number. Follow with planning_upsert_artifact(kind=spec), then validate via planning_validate — send overmind_notify(kind=approval, featureSlug) and wait for the human's reply first. Features cannot be deleted or archived via MCP — the UI owns feature deletion.")]
    public async Task<JsonElement> CreateFeature(
        [Description("Human-readable feature title.")] string title,
        [Description("Slug of the new feature: lowercase alphanumeric with hyphens, max 64 chars. Optional — omitted or blank generates {next free NNN}-{title-derived slug}; an explicit slug wins.")] string? slug = null,
        [Description("Optional markdown body of the feature document (goal, requirements, non-goals).")] string? body = null,
        [Description("Optional priority: p0 (critical), p1 (high), p2 (normal — the default when omitted), p3 (low).")] string? priority = null,
        [Description("Id of the project to create the feature in; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var normalizedPriority = priority is null ? null : NormalizePriority(priority);
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);

        var payload = new JsonObject
        {
            ["title"] = title,
            ["body"] = body
        };

        if (!string.IsNullOrWhiteSpace(slug))
        {
            payload["slug"] = slug;
        }

        if (normalizedPriority is not null)
        {
            payload["priority"] = normalizedPriority;
        }

        var feature = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features",
            payload,
            ct), readback: null, ct);
        return CompactFeature(feature);
    }

    [McpServerTool(Name = "planning_set_feature_status", Destructive = false, Idempotent = false)]
    [Description("Moves a feature status on the planning board — only on the user's explicit request in the current session, never on the agent's own initiative. Allowed targets: done (close, from any non-done status) or inProgress (review rollback); every other manual move belongs to the human door in the Overmind UI. done is gated: the feature needs a result artifact first (FeatureResultRequired 409 names it), and a fresh human tap — an approval reply newer than the result's last update; without one the call fails with FeatureDoneTapRequired 409 — send overmind_notify(kind=approval, featureSlug=<slug>), wait for the human's reply, then retry, or let the human close the feature in the UI. A successful agent-path done stamps the feature log with reviewed-by provenance (echoed as lastLogLine unless a held claim adds its release line after it). inProgress needs no gate. The logNote is mandatory, lands as a dated line in the feature log, and must carry the provenance, e.g. 'by user request in-session'.")]
    public async Task<JsonElement> SetFeatureStatus(
        [Description("Slug of the feature to move, as shown on the board.")] string slug,
        [Description("New status: done or inProgress — the only agent-door targets.")] string status,
        [Description("Required note recorded as a dated log line; carries the provenance, e.g. 'by user request in-session'.")] string logNote,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        [Description(OperationIdDescription)] Guid? operationId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(logNote))
        {
            throw new McpException(FeatureStatusNoteRequired);
        }

        var normalized = NormalizeFeatureStatus(status);
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var echo = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/status",
            new { status = normalized, logNote },
            ct, operationId), FeatureLogReadback(resolved, slug, logNote), ct);
        return MutationEcho(echo, "slug");
    }

    [McpServerTool(Name = "planning_update_feature", Destructive = false, Idempotent = false)]
    [Description("Partial edit of a feature's title, body, priority, lane, or codeSurfaces — durable what/why stays correctable by agents. logNote is mandatory and lands as a dated line in the feature log. A priority change moves the feature to the tail of its priority band. lane assigns the feature to a lane roster entry by name — an unknown name auto-creates the lane row (empty contour, no priority): the assigning act is the birth, planning_set_lanes cannot create lanes; empty string clears the lane, omit to keep. A lane whose last open feature leaves or closes retires itself. codeSurfaces declares the repo-relative glob patterns this feature touches (e.g. src/web/**) — advisory only: the queue, board and wave-snapshot rows project potentialOverlaps between in-flight features so coordinators see cross-lane collisions; an empty array clears the surfaces, omit to keep. Status is not editable here — use planning_set_feature_status.")]
    public async Task<JsonElement> UpdateFeature(
        [Description("Slug of the feature to edit, as shown on the board.")] string slug,
        [Description("Required note recorded as a dated [agent] line in the feature log — why the edit happened.")] string logNote,
        [Description("New feature title; omit to keep the current one.")] string? title = null,
        [Description("Full replacement body — markdown with its XML sections; omit to keep the current one.")] string? body = null,
        [Description("New priority: p0 (critical), p1 (high), p2 (normal), p3 (low); omit to keep the current one.")] string? priority = null,
        [Description("Lane roster name to assign (an unknown name auto-creates the lane row — the birth path); empty string clears the lane; omit to keep the current one.")] string? lane = null,
        [Description("Repo-relative glob patterns declaring the file surfaces this feature touches (e.g. [\"src/web/**\"]; only * and ** wildcards are supported — ?, [, ], {, } are rejected); empty array clears; omit to keep the current ones.")] string[]? codeSurfaces = null,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        [Description(OperationIdDescription)] Guid? operationId = null,
        CancellationToken ct = default)
    {
        var payload = new JsonObject { ["logNote"] = logNote };

        if (!string.IsNullOrWhiteSpace(title))
        {
            payload["title"] = title;
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            payload["body"] = body;
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            payload["priority"] = priority;
        }

        if (lane is not null)
        {
            payload["lane"] = lane;
        }

        if (codeSurfaces is not null)
        {
            payload["codeSurfaces"] = new JsonArray(codeSurfaces.Select(s => JsonValue.Create(s)).ToArray());
        }

        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        return await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/update",
            payload,
            ct, operationId), FeatureLogReadback(resolved, slug, logNote), ct);
    }

    [McpServerTool(Name = "planning_task_queue", ReadOnly = true, Idempotent = true)]
    [Description("Reads the planning queue of a project — the what's-next projection of the board: non-done features sorted priority asc, then queue position; done features drop out. The board order IS the queue: first on the board is next in the queue, and the dashboard board shows exactly this order. The human reorders it by dragging cards within a priority band; the agent moves features with planning_move_feature, and crossing a band is planning_set_feature_priority with the user's explicit approval. Entries are compact: slug, number, title, priority, status, queuePosition, specStatus, planStatus, taskDone, taskTotal, taskCounts (todo/inProgress/review/done/blocked, sums to taskTotal), readiness 0-3, blockedBy, isBlocked, plus claim state when claimed: claimedByAlias, claimedByVendor, claimedAt and claimState (alive | stale | ended — stale claims are take-over-able via planning_claim_feature). Every entry also carries the ceremony context: phase (drafting | writing spec | planning | plan validated | in progress | review | done — derived server-side from spec/plan/task state, the lifecycle status column stays the source of truth) and briefExcerpt (the first <goal> paragraph capped at ~500 chars; null when the feature has no goal — one-glance ceremony context without opening the feature). Rows also carry the feature timestamps: createdAt (when the feature was created) and updatedAt (when it last changed — the same stamp the board view reports). Every row projects the background materializer's lag — pendingMutations (mutations enqueued but not yet mirrored), lastAttemptAt, lastSuccessAt, stale (true while pendingMutations > 0 or the last attempt failed) — the same state the overmind_onboard materialization block reports; a clean BACKLOG/ tree with stale=true means the mirror is behind. limit truncates to the first N entries; omit it for the full queue.")]
    public async Task<JsonElement> TaskQueue(
        [Description("Id of the project whose queue to read; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        [Description("Optional positive int; truncates the queue to the first N entries.")] int? limit = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var url = $"/api/agent/projects/{resolved}/planning/task-queue";
        if (limit is not null)
        {
            url += $"?limit={limit}";
        }

        return await Guarded(() => _api.GetAsync(url, ct));
    }

    [McpServerTool(Name = "planning_move_feature", Destructive = false, Idempotent = false)]
    [Description("Moves a feature within its priority band on the planning queue — band-scoped only; crossing bands means planning_set_feature_priority. Moves: 'up' and 'down' swap with the adjacent non-done feature (no-op at the band edges), 'first' and 'last' jump to the band head or tail, 'toPosition' takes the absolute 1-based spot in position. The server resolves neighbors and renumbers, so no band-wide read is needed. Contract violations (unknown move, position only for toPosition, out-of-range) return error text naming the contract; the human reorders by dragging on the dashboard board.")]
    public async Task<CallToolResult> MoveFeature(
        [Description("Slug of the feature to move, as shown on the board.")] string slug,
        [Description("One of: up, down, first, last, toPosition.")] string move,
        [Description("1-based spot in the feature's priority band; required only when move is toPosition.")] int? position = null,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var contract = QueueMoveContractError(move, position);
        if (contract is not null)
        {
            return ContractViolation(contract);
        }

        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        JsonElement moved;
        try
        {
            moved = await _mutations.SendAsync(() => _api.PostAsync(
                $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/queue-move",
                new { move = move.Trim().ToLowerInvariant(), position },
                ct), readback: null, ct);
        }
        catch (McpException ex)
        {
            return ContractViolation(MutationGuard.DetailAfterOperationMark(ex.Message) ?? ex.Message);
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

    [McpServerTool(Name = "planning_claim_feature", Destructive = false, Idempotent = false)]
    [Description("Claims the feature for your adapter session — the claim is a hard mutation lock backed by session liveness: feature done → 409; a live foreign session holding the claim → 409 naming the holder (regardless of alias+vendor match — name collisions cannot hijack a live holder); a stale or ended claim by a different session → take-over succeeds and is logged; your own alias+vendor on an ended session (adapter restart) → auto-adopt with a fresh session binding. alias and vendor are optional — omitted values fall back to the identity the session registered with (OVERMIND_ALIAS/OVERMIND_VENDOR), and a claim-locked write that finds a stale/ended claim held by the caller's own alias+vendor adopts it and proceeds instead of 409ing. Lane discipline rides on the claim: a feature whose lane already holds an ALIVE claim by a different alias+vendor pair refuses with 409 naming the holder — unclaimed open features in the lane and the caller's own claims never block, and the stale/ended take-over flow is unchanged. overrideLaneCheck=true is the explicit escape hatch and is echoed back. The response also carries liveSurfaces (globs + owner aliases of running stands watched by the feature's lane that overlap the feature's codeSurfaces) when any match — purely informational, editing is never blocked. Reads stay open; the human door in the Overmind UI is never locked. On 409 the error names the holder — pick a different feature or coordinate; claims clear themselves when the holder session ends or goes stale past the project threshold.")]
    public async Task<CallToolResult> ClaimFeature(
        [Description("Slug of the feature to claim, as shown on the board.")] string slug,
        [Description("Your display alias, max 32 chars (e.g. Dan); omit to use the alias the adapter session registered with.")] string? alias = null,
        [Description("Your vendor string, max 32 chars (e.g. qwen, claude); omit to use the vendor the adapter session registered with.")] string? vendor = null,
        [Description("True to skip the lane-claim check — the explicit escape hatch, echoed in the response.")] bool overrideLaneCheck = false,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var trimmedAlias = BlankToNull(alias);
        var trimmedVendor = BlankToNull(vendor);

        if (trimmedAlias?.Length > ClaimIdentityCap)
        {
            throw new McpException(
                $"Alias '{trimmedAlias}' exceeds the {ClaimIdentityCap}-character cap — shorten it and retry.");
        }

        if (trimmedVendor?.Length > ClaimIdentityCap)
        {
            throw new McpException(
                $"Vendor '{trimmedVendor}' exceeds the {ClaimIdentityCap}-character cap — shorten it and retry.");
        }

        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var payload = new JsonObject { ["overrideLaneCheck"] = overrideLaneCheck };
        if (trimmedAlias is not null)
        {
            payload["alias"] = trimmedAlias;
        }

        if (trimmedVendor is not null)
        {
            payload["vendor"] = trimmedVendor;
        }

        var echo = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/claim",
            payload,
            ct), readback: null, ct);

        var claim = new JsonObject
        {
            ["slug"] = echo.GetProperty("slug").GetString(),
            ["claimedByAlias"] = echo.GetProperty("claimedByAlias").GetString(),
            ["claimedByVendor"] = echo.GetProperty("claimedByVendor").GetString(),
            ["claimedAt"] = echo.GetProperty("claimedAt").GetString(),
            ["claimState"] = echo.GetProperty("claimState").GetString()
        };
        if (echo.TryGetProperty("lane", out var lane) && lane.ValueKind == JsonValueKind.String)
        {
            claim["lane"] = lane.GetString();
        }

        if (echo.TryGetProperty("overrideLaneCheck", out var overridden) && overridden.ValueKind == JsonValueKind.True)
        {
            claim["overrideLaneCheck"] = true;
        }

        if (echo.TryGetProperty("liveSurfaces", out var liveSurfaces) &&
            liveSurfaces.ValueKind == JsonValueKind.Array &&
            liveSurfaces.GetArrayLength() > 0)
        {
            claim["liveSurfaces"] = JsonNode.Parse(liveSurfaces.GetRawText())!.DeepClone();
        }

        return EchoResult(claim.Deserialize<JsonElement>());
    }

    [McpServerTool(Name = "planning_unclaim_feature", Destructive = false, Idempotent = true)]
    [Description("Releases your claim on the feature — holder session only; any other session gets a 409 naming the holder. The human door in the Overmind UI can release any claim without session restrictions. Unclaiming an unclaimed feature is a no-op, and a claim released itself when the holder closed the feature done.")]
    public async Task<CallToolResult> UnclaimFeature(
        [Description("Slug of the feature to unclaim, as shown on the board.")] string slug,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var echo = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/unclaim",
            new { },
            ct), readback: null, ct);

        var claim = new JsonObject
        {
            ["slug"] = echo.GetProperty("slug").GetString(),
            ["claimedByAlias"] = echo.GetProperty("claimedByAlias").GetString(),
            ["claimedByVendor"] = echo.GetProperty("claimedByVendor").GetString(),
            ["claimedAt"] = echo.GetProperty("claimedAt").GetString(),
            ["claimState"] = echo.GetProperty("claimState").GetString()
        };
        return EchoResult(claim.Deserialize<JsonElement>());
    }

    [McpServerTool(Name = "planning_set_feature_priority", Destructive = false, Idempotent = false)]
    [Description("Changes a feature priority (p0 critical, p1 high, p2 normal, p3 low) — the feature re-enters at the tail of the new band. Band crossings are the weightier act: only on the user's explicit approval in the current session, never on the agent's own initiative. Moves within one band belong to planning_move_feature or the human board drag.")]
    public async Task<JsonElement> SetFeaturePriority(
        [Description("Slug of the feature to re-prioritize, as shown on the board.")] string slug,
        [Description("New priority: p0 (critical), p1 (high), p2 (normal), p3 (low).")] string priority,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        [Description(OperationIdDescription)] Guid? operationId = null,
        CancellationToken ct = default)
    {
        var normalized = NormalizePriority(priority);
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var feature = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/priority",
            new { priority = normalized },
            ct, operationId), readback: null, ct);
        return CompactFeature(feature);
    }

    [McpServerTool(Name = "planning_pin_feature_code", Destructive = false, Idempotent = true)]
    [Description("Pins the explicit code location of a feature: a branch and/or a worktree path stored on the feature itself. planning_feature_code matches branches and worktrees by naming convention; a pin overrides that — use it when the code lives on a differently named branch, several candidates match, or the worktree was created by hand. Upsert semantics: the body is a full replace, last write wins, so resending one field keeps only what you send; both fields null or omitted clears the pin entirely, and clearing an empty pin is a no-op success. Every actual change appends one feature log line; the resulting pin is echoed.")]
    public async Task<CallToolResult> PinFeatureCode(
        [Description("Slug of the feature to pin, as shown on the board.")] string slug,
        [Description("Branch to pin as a short branch name as printed by planning_feature_code; null or omitted clears the branch half of the pin.")] string? branch = null,
        [Description("Worktree path to pin; null or omitted clears the worktree half of the pin.")] string? worktreePath = null,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var echo = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/pin-code",
            new { branch = BlankToNull(branch), worktreePath = BlankToNull(worktreePath) },
            ct), readback: null, ct);

        var pin = new JsonObject
        {
            ["slug"] = echo.GetProperty("slug").GetString(),
            ["pinnedBranch"] = PinValue(echo, "pinnedBranch"),
            ["pinnedWorktreePath"] = PinValue(echo, "pinnedWorktreePath"),
            ["lastLogLine"] = echo.GetProperty("lastLogLine").GetString()
        };
        return EchoResult(pin.Deserialize<JsonElement>());
    }

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? PinValue(JsonElement payload, string field) =>
        payload.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    [McpServerTool(Name = "planning_list_feature_dependencies", ReadOnly = true, Idempotent = true)]
    [Description("Lists the dependency graph of a feature: blockedBy (features this one waits for) and blocks (features waiting on this one), each entry carrying slug, status and closed — a dependency closes when its blocker reaches done or review. An unclosed blockedBy stops planning_current_task.")]
    public async Task<JsonElement> ListFeatureDependencies(
        [Description("Slug of the feature, as shown on the board.")] string slug,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);

        return await Guarded(() => _api.GetAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/dependencies", ct));
    }

    [McpServerTool(Name = "planning_add_feature_dependency", Destructive = false, Idempotent = false)]
    [Description("Adds a blocker to a feature: the feature stops issuing tasks through planning_current_task until the blocker closes (done or review). Rejects self-reference and any edge that completes a dependency cycle, naming the chain. Same-project only.")]
    public async Task<JsonElement> AddFeatureDependency(
        [Description("Slug of the feature that gains the blocker.")] string slug,
        [Description("Slug of the blocking feature, as shown on the board.")] string blockedBySlug,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(blockedBySlug))
        {
            throw new McpException("blockedBySlug is required: the feature slug of the blocker, as shown on the board.");
        }

        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        return await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/dependencies",
            new { blockedBy = blockedBySlug.Trim() },
            ct), readback: null, ct);
    }

    [McpServerTool(Name = "planning_remove_feature_dependency", Destructive = false, Idempotent = false)]
    [Description("Removes one blocker from a feature; removing a link that is not there is a no-op. Lifting the last unclosed blocker unblocks the feature for planning_current_task.")]
    public async Task<JsonElement> RemoveFeatureDependency(
        [Description("Slug of the feature whose blocker is removed.")] string slug,
        [Description("Slug of the blocker to remove.")] string blockedBySlug,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(blockedBySlug))
        {
            throw new McpException("blockedBySlug is required: the feature slug of the blocker to remove.");
        }

        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        return await _mutations.SendAsync(() => _api.DeleteAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/dependencies" +
            $"/{Uri.EscapeDataString(blockedBySlug.Trim())}",
            ct), readback: null, ct);
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
}
