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
    [Description("The full feature board: statuses, priorities, claim holders, task counts; filters compose. The work order is planning_task_queue.")]
    public async Task<JsonElement> ListFeatures(
        [Description("Optional project id.")] Guid? projectId = null,
        [Description("Optional status filter.")] string? status = null,
        [Description("Optional filter on the claim owner alias.")] string? claimedByAlias = null,
        [Description("Optional lane roster name filter.")] string? lane = null,
        [Description("Optional priority band filter.")] string? priority = null,
        [Description("True to return only unclaimed features.")] bool? unclaimedOnly = null,
        [Description("Comma-separated keys to project each row to.")] string? fields = null,
        [Description("Predefined cheap rows; exclusive with fields.")] bool compact = false,
        CancellationToken ct = default)
    {
        var requested = PlanningFieldProjection.ResolveRequested(fields, compact, PlanningFieldProjection.BoardFields);
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var url = $"/api/agent/projects/{resolved}/planning/features{BoardQuery(status, claimedByAlias, lane, priority, unclaimedOnly)}";
        var board = await Guarded(() => _api.GetAsync(url, ct));

        if (compact)
        {
            return PlanningFieldProjection.CompactRows(board, null);
        }

        return requested.Length == 0 ? board : PlanningFieldProjection.ProjectRows(board, null, requested);
    }

    private static string BoardQuery(
        string? status, string? claimedByAlias, string? lane, string? priority, bool? unclaimedOnly)
    {
        var query = new List<string>();
        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
            }
        }

        Add("status", status);
        Add("claimedByAlias", claimedByAlias);
        Add("lane", lane);
        Add("priority", priority);
        if (unclaimedOnly == true)
        {
            query.Add("unclaimedOnly=true");
        }

        return query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
    }

    [McpServerTool(Name = "planning_read_feature", ReadOnly = true, Idempotent = true)]
    [Description("One feature with every artifact body and log — the most expensive read. Shrink it with includeArtifacts=false or sections; roster only: planning_list_artifacts.")]
    public async Task<JsonElement> ReadFeature(
        [Description("Feature slug.")] string slug,
        [Description("False for body plus roster summary only.")] bool? includeArtifacts = null,
        [Description("Comma-separated section names to narrow the body.")] string? sections = null,
        [Description("Comma-separated payload keys to project.")] string? fields = null,
        [Description("Predefined cheap shape; exclusive with fields.")] bool compact = false,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var requested = PlanningFieldProjection.ResolveRequested(fields, compact, PlanningFieldProjection.FeatureReadFields);
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var url = $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}";
        var query = new List<string>();
        if (includeArtifacts == false)
        {
            query.Add("includeArtifacts=false");
        }

        if (!string.IsNullOrWhiteSpace(sections))
        {
            query.Add($"sections={Uri.EscapeDataString(sections)}");
        }

        if (query.Count > 0)
        {
            url += $"?{string.Join('&', query)}";
        }

        var feature = await Guarded(() => _api.GetAsync(url, ct));
        var filtered = KeepReviewOnlyOnTaskArtifacts(feature);

        if (compact)
        {
            return PlanningFieldProjection.CompactFeatureRead(filtered);
        }

        return requested.Length == 0 ? filtered : PlanningFieldProjection.ProjectObject(filtered, requested);
    }

    [McpServerTool(Name = "planning_list_artifacts", ReadOnly = true, Idempotent = true)]
    [Description("Metadata-only roster of one feature's artifacts — the cheapest look at what it contains. Bodies: planning_read_artifact.")]
    public async Task<JsonElement> ListArtifacts(
        [Description("Feature slug.")] string slug,
        [Description("Optional artifact kind filter: spec, plan, task or result.")] string? kind = null,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("The task to work now, or a stop/held/empty state whose reason leads the payload. Obey a STOP line — ask the user.")]
    public async Task<JsonElement> CurrentTask(
        [Description("Feature slug.")] string slug,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var payload = await Guarded(() => _api.GetAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/current", ct));
        return LeadWithStopReason(payload);
    }

    [McpServerTool(Name = "planning_create_feature", Destructive = false, Idempotent = false)]
    [Description("Creates a feature on the board; omit slug and the Host generates '{NNN}-{slug}'. Then write and validate the spec.")]
    public async Task<JsonElement> CreateFeature(
        [Description("Feature title.")] string title,
        [Description("Optional slug; omit to auto-generate.")] string? slug = null,
        [Description("Optional feature body markdown.")] string? body = null,
        [Description("Optional priority: p0, p1, p2 or p3.")] string? priority = null,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("Moves a feature status, on the user's explicit word only. done needs a result plus a fresh approval reply; inProgress rolls back review.")]
    public async Task<JsonElement> SetFeatureStatus(
        [Description("Feature slug.")] string slug,
        [Description("New status: done or inProgress.")] string status,
        [Description("Provenance note for the feature log.")] string logNote,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("Edits a feature's title, body, priority, lane or codeSurfaces; logNote is mandatory. Status: planning_set_feature_status; one section: planning_patch_feature.")]
    public async Task<JsonElement> UpdateFeature(
        [Description("Feature slug.")] string slug,
        [Description("Why the edit happened (log line).")] string logNote,
        [Description("New title; omit to keep.")] string? title = null,
        [Description("New full body; omit to keep.")] string? body = null,
        [Description("New priority; omit to keep.")] string? priority = null,
        [Description("Lane name to assign; empty clears.")] string? lane = null,
        [Description("Repo-relative globs this feature touches.")] string[]? codeSurfaces = null,
        [Description("Optional project id.")] Guid? projectId = null,
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

    [McpServerTool(Name = "planning_patch_feature", Destructive = false, Idempotent = false)]
    [Description("Replaces or inserts one XML <section> of a feature body without resending it. Feature bodies have no heading or AC modes.")]
    public async Task<JsonElement> PatchFeature(
        [Description("Feature slug.")] string slug,
        [Description("XML section name, e.g. goal.")] string section,
        [Description("Why the section changed (log line).")] string logNote,
        [Description("New section content; omit to insert it empty.")] string? content = null,
        [Description("True with reason to tombstone the section.")] bool remove = false,
        [Description("Reason carried in the tombstone line.")] string? reason = null,
        [Description("Optional project id.")] Guid? projectId = null,
        [Description(OperationIdDescription)] Guid? operationId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(logNote))
        {
            throw new McpException(FeaturePatchNoteRequired);
        }

        var payload = new JsonObject
        {
            ["section"] = section.Trim(),
            ["logNote"] = logNote,
            ["remove"] = remove
        };

        if (!string.IsNullOrWhiteSpace(content))
        {
            payload["content"] = content;
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            payload["reason"] = reason.Trim();
        }

        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        return await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/patch",
            payload,
            ct, operationId), FeatureLogReadback(resolved, slug, logNote), ct);
    }

    [McpServerTool(Name = "planning_task_queue", ReadOnly = true, Idempotent = true)]
    [Description("The work order: non-done features in priority-then-queue order with phase, task counts, blockers and materializer lag.")]
    public async Task<JsonElement> TaskQueue(
        [Description("Optional project id.")] Guid? projectId = null,
        [Description("Optional cap on the rows returned.")] int? limit = null,
        [Description("Comma-separated keys to project each row to.")] string? fields = null,
        [Description("Predefined cheap rows; exclusive with fields.")] bool compact = false,
        CancellationToken ct = default)
    {
        var requested = PlanningFieldProjection.ResolveRequested(fields, compact, PlanningFieldProjection.QueueFields);
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var url = $"/api/agent/projects/{resolved}/planning/task-queue";
        if (limit is not null)
        {
            url += $"?limit={limit}";
        }

        var queue = await Guarded(() => _api.GetAsync(url, ct));

        if (compact)
        {
            return PlanningFieldProjection.CompactRows(queue, PlanningFieldProjection.QueueRowsProperty);
        }

        return requested.Length == 0
            ? queue
            : PlanningFieldProjection.ProjectRows(queue, PlanningFieldProjection.QueueRowsProperty, requested);
    }

    [McpServerTool(Name = "planning_move_feature", Destructive = false, Idempotent = false)]
    [Description("Moves a feature within its priority band: up, down, first, last, toPosition. Crossing a band: planning_set_feature_priority.")]
    public async Task<CallToolResult> MoveFeature(
        [Description("Feature slug.")] string slug,
        [Description("One of up, down, first, last, toPosition.")] string move,
        [Description("1-based spot, for toPosition only.")] int? position = null,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("Claims a feature for your session — the mutation lock. A live foreign holder 409s by name; stale or released claims are take-over-able.")]
    public async Task<CallToolResult> ClaimFeature(
        [Description("Feature slug.")] string slug,
        [Description("Optional alias; defaults to the session's.")] string? alias = null,
        [Description("Optional vendor; defaults to the session's.")] string? vendor = null,
        [Description("True to skip the lane-claim check.")] bool overrideLaneCheck = false,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("Releases your own claim; a foreign session gets a 409. No-op when unclaimed.")]
    public async Task<CallToolResult> UnclaimFeature(
        [Description("Feature slug.")] string slug,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("Changes a feature's priority band; it re-enters at the new band's tail. Only on the user's explicit word. Within a band: planning_move_feature.")]
    public async Task<JsonElement> SetFeaturePriority(
        [Description("Feature slug.")] string slug,
        [Description("New priority: p0, p1, p2 or p3.")] string priority,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("Pins a feature's explicit branch and/or worktree path, overriding the naming-convention discovery planning_feature_code does.")]
    public async Task<CallToolResult> PinFeatureCode(
        [Description("Feature slug.")] string slug,
        [Description("Branch to pin; null clears it.")] string? branch = null,
        [Description("Worktree path to pin; null clears it.")] string? worktreePath = null,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("A feature's dependency edges: blockedBy and blocks with status and closed. An unclosed blocker stops planning_current_task.")]
    public async Task<JsonElement> ListFeatureDependencies(
        [Description("Feature slug.")] string slug,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);

        return await Guarded(() => _api.GetAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(slug)}/dependencies", ct));
    }

    [McpServerTool(Name = "planning_add_feature_dependency", Destructive = false, Idempotent = false)]
    [Description("Adds a blocker: no tasks issue until it reaches done or review. Self-reference and cycles are rejected.")]
    public async Task<JsonElement> AddFeatureDependency(
        [Description("Feature slug.")] string slug,
        [Description("Slug of the blocking feature.")] string blockedBySlug,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("Removes one blocker; a link that is not there is a no-op.")]
    public async Task<JsonElement> RemoveFeatureDependency(
        [Description("Feature slug.")] string slug,
        [Description("Slug of the blocker to remove.")] string blockedBySlug,
        [Description("Optional project id.")] Guid? projectId = null,
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
