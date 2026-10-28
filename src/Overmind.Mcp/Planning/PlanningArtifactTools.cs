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
public sealed class PlanningArtifactTools
{
    private const string AcIndexBasisLine =
        "acIndex is 1-based; numbering matches the materialized mirror; acText is the recommended selector";

    private const string DoorSource = "agent";

    private const string ArtifactAliasDescription =
        "Optional alias instead of artifactId: '<slug>/T<order>' addresses a task by its order, " +
        "'<slug>/S', '<slug>/P' and '<slug>/R' address the single spec, plan or result. " +
        "Resolved against the feature roster, so it needs projectId, and it is mutually exclusive with artifactId.";

    private const string AlreadyTickedNoChangeText = "already ticked — no change";

    private const string AlreadyUntickedNoChangeText = "already unticked — no change";

    private readonly OvermindApiClient _api;
    private readonly MutationGuard _mutations;
    private readonly PlanningArtifactAliasResolver _aliases;
    private readonly ProjectResolver _projects;

    public PlanningArtifactTools(
        OvermindApiClient api,
        MutationGuard mutations,
        PlanningArtifactAliasResolver aliases,
        ProjectResolver projects)
    {
        _api = api;
        _mutations = mutations;
        _aliases = aliases;
        _projects = projects;
    }

    [McpServerTool(Name = "planning_read_artifact", ReadOnly = true, Idempotent = true)]
    [Description("Reads one artifact by id — without sections, one artifact, full body; with sections, the cheapest read: only the named sections. Sections reads are deterministic: located on the canonicalized form (the same one writes produce), found sections return in body order regardless of request order, and a name the body carries never lands in missingSections; the full read stays byte-faithful to storage. missingSections names requested-but-absent sections; a read never fails on a missing one. The review field appears on task payloads only. Task payloads also carry blockedBy — the sibling task orders this task waits on, [] when it has no edges; non-task kinds omit the field. Bodies may carry pointer markers — case-sensitive 'pointer:' as the first non-whitespace token naming either a repo-relative path with an optional #anchor or a grimoire crew-memory id ('grimoire://memory/<id>'); the server never reads the targets. Parsed pointers ride the artifact projections (planning_current_task payload, planning_list_artifacts roster rows), not this read.")]
    public async Task<JsonElement> ReadArtifact(
        [Description("Id of the artifact to read, as handed out by planning_current_task; omit it to address the artifact by alias + projectId.")] Guid? artifactId = null,
        [Description("Optional comma/space-separated section names, e.g. log,obstacles — narrows the body to those sections, returned in body order.")] string? sections = null,
        [Description("Id of the project the feature belongs to; required when alias is used.")] Guid? projectId = null,
        [Description(ArtifactAliasDescription)] string? alias = null,
        CancellationToken ct = default)
    {
        var id = await _aliases.AddressArtifactAsync(artifactId, projectId, alias, ct);
        var url = $"/api/agent/planning/artifacts/{id}";
        if (!string.IsNullOrWhiteSpace(sections))
        {
            url += $"?sections={Uri.EscapeDataString(sections)}";
        }

        var artifact = await Guarded(() => _api.GetAsync(url, ct));
        return KeepReviewOnlyOnTasks(artifact);
    }

    [McpServerTool(Name = "planning_read_artifact_diff", ReadOnly = true, Idempotent = true)]
    [Description("Diffs two versions of one artifact as a unified diff — the read between planning_read_artifact and nothing: no full bodies, only the change hunks. fromVersion/toVersion optional: omitting both diffs the two most recent versions. Adjacent versions may carry log-only bumps, so an empty diff between neighbors is a valid answer, not an error. Trailing-newline semantics: a trailing newline is ignored, so 'x' vs 'x\\n' diffs as empty (unlike git there is no \\ No newline marker). An unknown from/to version fails with ArtifactVersionNotFound 404; from == to or from > to fails with InvalidVersionRange 400.")]
    public async Task<JsonElement> ReadArtifactDiff(
        [Description("Id of the artifact to diff; omit it to address the artifact by alias + projectId.")] Guid? artifactId = null,
        [Description("Optional older side of the range; defaults to toVersion - 1.")] int? fromVersion = null,
        [Description("Optional newer side of the range; defaults to the artifact's current version.")] int? toVersion = null,
        [Description("Id of the project the feature belongs to; required when alias is used.")] Guid? projectId = null,
        [Description(ArtifactAliasDescription)] string? alias = null,
        CancellationToken ct = default)
    {
        var id = await _aliases.AddressArtifactAsync(artifactId, projectId, alias, ct);
        var url = $"/api/agent/planning/artifacts/{id}/diff";

        var query = new List<string>(2);
        if (fromVersion is not null)
        {
            query.Add($"from={fromVersion}");
        }

        if (toVersion is not null)
        {
            query.Add($"to={toVersion}");
        }

        if (query.Count > 0)
        {
            url += $"?{string.Join('&', query)}";
        }

        return await Guarded(() => _api.GetAsync(url, ct));
    }

    [McpServerTool(Name = "planning_upsert_artifact", Destructive = false, Idempotent = false)]
    [Description("Creates an artifact for a feature, or updates the existing artifact of the same kind and order as a new version — the most expensive write: resends the full body. Artifacts are created draft; validation goes through planning_validate. Editing a validated artifact requires a logNote — not needed on create. Expected XML sections — task: goal, scope, anti-scope, constraints, acceptance-criteria, refs, obstacles, log; spec/plan/result: free-form markdown with <log> as the only machine-parsed section. Order is a sort key within the feature — lowest order first; it drives planning_current_task picking; omitted order appends after the current max (first append lands on 0; materialized backlog files are numbered 1-based). Gaps in order numbering are legal and meaningful — imported backlogs skip tasks that were planned but never written; order doubles as the stable handle (blockedBy edges and {featureSlug}/T{order} aliases address tasks by order), so never renumber existing tasks to close a hole. Task artifacts also take a brief: a one-line summary (max 500 chars) shown next to the task; other kinds reject it. They also take blockedBy: sibling task orders this task waits on — re-upsert replaces the edge set declaratively (empty array clears), omit it to preserve the current edges on update (none on create). dryRun=true runs the full validation path (canonicalization, pointer lint, kind guards, order resolution) and persists NOTHING — no row, no version snapshot, no feature transition, no materialization dispatch; the response echoes the would-be artifact id and version marked dryRun, so a huge body can be shape-checked before the real send. Features cannot be deleted or archived via MCP — the UI owns feature deletion. Without a validated plan the feature never auto-promotes to review — flip it manually via planning_set_feature_status.")]
    public async Task<JsonElement> UpsertArtifact(
        [Description("Slug of the feature to add or update the artifact in.")] string featureSlug,
        [Description("Artifact kind: spec, plan, task or result.")] string kind,
        [Description("Markdown body. Expected XML sections per kind are listed in this tool's description; plain markdown is accepted — for spec/plan/result only the <log> section is machine-parsed.")] string body,
        [Description("Order of a task artifact within the feature; document kinds ignore it. Sort key and stable handle; gaps are legal — do not renumber to close them.")] int? order = null,
        [Description("Optional review level of a task: light or hard.")] string? review = null,
        [Description("Change note recorded in the artifact log; required when editing a validated artifact.")] string? logNote = null,
        [Description("Optional one-line brief of a task artifact, max 500 chars; not allowed for other kinds.")] string? brief = null,
        [Description("Optional task-only blockedBy: sibling task orders of the same feature this task waits on. Re-upsert replaces the edge set declaratively (empty array clears); omitted preserves the current edges on update and sets none on create.")] int[]? blockedBy = null,
        [Description("Validate the full payload and persist nothing — the response echoes the would-be id and version marked dryRun; default false.")] bool? dryRun = null,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        [Description(OperationIdDescription)] Guid? operationId = null,
        CancellationToken ct = default)
    {
        UpsertPayloadLint.PointerLines("body", body);

        var normalizedKind = NormalizeKind(kind);
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var payload = new JsonObject
        {
            ["kind"] = normalizedKind,
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

        if (blockedBy is not null)
        {
            payload["blockedBy"] = new JsonArray([.. blockedBy.Select(o => JsonValue.Create(o))]);
        }

        if (dryRun == true)
        {
            payload["dryRun"] = true;
        }

        async Task<MutationReadback?> Probe(CancellationToken probeCt) =>
            string.IsNullOrWhiteSpace(logNote)
                ? null
                : await UpsertArtifactReadback.ResolveAsync(
                    _api, resolved, featureSlug, normalizedKind, order, logNote, probeCt);

        var artifact = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(featureSlug)}/artifacts",
            payload,
            ct, operationId), Probe, ct);
        return CompactArtifact(artifact);
    }

    [McpServerTool(Name = "planning_upsert_tasks", Destructive = false, Idempotent = false)]
    [Description("Creates or updates N task artifacts of a feature in one call — the batch counterpart of planning_upsert_artifact, for decomposing a plan into tasks. Atomic: all-or-nothing — items are staged and committed together, and any failing item rolls back the whole batch, so a retry starts clean; a failing item is reported with its index (tasks[2]: …). Exactly one materialization pass is enqueued after the batch — it runs shortly after, in the background, never per item — and one feature auto-transition evaluation runs; creating tasks while the feature is in review reopens it once. Each item carries the single-upsert fields: body (required), order, review light/hard, brief (task-only, one line, max 500 chars), blockedBy (sibling task orders this task waits on; empty array clears, omitted preserves the current edges — every response item echoes the resulting edge set as blockedBy, [] when the item ends with no edges, so set, clear via [] and preserve via omit are all observable in the reply), and logNote under the same rules as planning_upsert_artifact — optional, required only when the item edits an existing body that demands one. Order is a sort key within the feature — lowest order first; it drives planning_current_task picking; omitted order appends after the current max (first append lands on 0; materialized backlog files are numbered 1-based). Gaps in order numbering are legal and meaningful — imported backlogs skip tasks that were planned but never written; order doubles as the stable handle (blockedBy edges and {featureSlug}/T{order} aliases address tasks by order), so never renumber existing tasks to close a hole. An item whose order hits an existing task updates that task in place. dryRun=true validates the whole batch (canonicalization, pointer lint, order resolution, edge staging) and persists NOTHING — would-be orders and ids come back marked dryRun; the adapter also lints the envelope (tasks must be an array of objects with body) and pointer lines client-side, before any HTTP call.")]
    public async Task<JsonElement> UpsertTasks(
        [Description("Slug of the feature to create or update tasks in.")] string featureSlug,
        [Description("Array of task items, each an object: body (required), order, review, logNote, brief, blockedBy.")] JsonElement tasks,
        [Description("Validate the whole batch and persist nothing — the response echoes the would-be orders and ids marked dryRun; default false.")] bool? dryRun = null,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        [Description(OperationIdDescription)] Guid? operationId = null,
        CancellationToken ct = default)
    {
        var payload = new JsonObject
        {
            ["tasks"] = BuildTaskItems(tasks)
        };

        if (dryRun == true)
        {
            payload["dryRun"] = true;
        }

        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var batch = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(featureSlug)}/artifacts/tasks",
            payload,
            ct, operationId), readback: null, ct);
        return CompactTaskBatch(batch);
    }

    private static JsonArray BuildTaskItems(JsonElement tasks)
    {
        if (tasks.ValueKind == JsonValueKind.String)
        {
            try
            {
                if (JsonNode.Parse(tasks.GetString()!) is JsonArray encoded)
                {
                    tasks = JsonSerializer.SerializeToElement(encoded);
                }
            }
            catch (JsonException)
            {
                throw new McpException($"tasks must be a JSON array, not an encoded string. {TaskItemsAcceptedForm}");
            }
        }

        if (tasks.ValueKind != JsonValueKind.Array)
        {
            throw new McpException(
                $"tasks must be a JSON array of task items — item fields never sit at the call's top level. {TaskItemsAcceptedForm}");
        }

        if (tasks.GetArrayLength() == 0)
        {
            throw new McpException($"tasks must carry at least one item. {TaskItemsAcceptedForm}");
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
            throw new McpException(
                $"each item must be an object with body, order, review, logNote, brief, blockedBy. {TaskItemsAcceptedForm}");
        }

        if (!item.TryGetProperty("body", out var body) ||
            body.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(body.GetString()))
        {
            throw new McpException(
                $"body is required on every item and must be a non-blank string. {TaskItemsAcceptedForm}");
        }

        UpsertPayloadLint.PointerLines("body", body.GetString());

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

        if (item.TryGetProperty("blockedBy", out var blockedBy))
        {
            if (blockedBy.ValueKind is not JsonValueKind.Array)
            {
                throw new McpException(
                    "blockedBy must be an array of task orders; omit it to preserve the current edges.");
            }

            payload["blockedBy"] = JsonNode.Parse(blockedBy.GetRawText());
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

            if (artifact.TryGetProperty("blockedBy", out var blockedBy) &&
                blockedBy.ValueKind == JsonValueKind.Array)
            {
                compact["blockedBy"] = JsonNode.Parse(blockedBy.GetRawText());
            }

            tasks.Add(compact);
        }

        var result = new JsonObject
        {
            ["created"] = batch.GetProperty("created").GetInt32(),
            ["updated"] = batch.GetProperty("updated").GetInt32(),
            ["tasks"] = tasks
        };

        if (batch.TryGetProperty("dryRun", out var dryRun) && dryRun.ValueKind == JsonValueKind.True)
        {
            result["dryRun"] = true;
        }

        CarryReplayedFlag(result, batch);
        return result.Deserialize<JsonElement>();
    }

    [McpServerTool(Name = "planning_set_status", Destructive = false, Idempotent = false)]
    [Description("Planning ceremony, step 3: moves an artifact to a new status and records why in the artifact log. Allowed statuses per kind — spec/plan/result: draft, validated; task: todo, inProgress, review, done, blocked. Validated is the terminal for documents; result has no done. The logNote is mandatory on every move — an unexplained status move is rejected. Each move appends one line to the artifact's <log> section, shaped '- YYYY-MM-DD [agent] — <logNote>'. Transitions are free-form: any allowed status of the kind can be reached from any current status — there is no enforced sequence. A task closed done with unticked acceptance-criteria items still moves, and the response carries a non-blocking warning counting them. Taking a task (moving it to inProgress, review or done) also promotes its draft/ready feature to inProgress with an [auto] line.")]
    public async Task<JsonElement> SetStatus(
        [Description("New status: draft, validated, todo, inProgress, review, done or blocked.")] string status,
        [Description("Required note recorded as a dated log line explaining the status move.")] string logNote,
        [Description("Id of the artifact to move; omit it to address the artifact by alias + projectId.")] Guid? artifactId = null,
        [Description("Id of the project the feature belongs to; required when alias is used.")] Guid? projectId = null,
        [Description(ArtifactAliasDescription)] string? alias = null,
        [Description(OperationIdDescription)] Guid? operationId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(logNote))
        {
            throw new McpException(StatusNoteRequired);
        }

        var id = await _aliases.AddressArtifactAsync(artifactId, projectId, alias, ct);
        var normalized = NormalizeStatus(status);
        var payload = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/planning/artifacts/{id}/status",
            new { status = normalized, logNote },
            ct, operationId), ArtifactLogReadback(id, logNote), ct);

        return MutationEcho(payload, "id");
    }

    [McpServerTool(Name = "planning_append_log", Destructive = false, Idempotent = false)]
    [Description("Planning ceremony, step 3 companion: appends a dated line to an artifact's log without touching its body — the cheapest write: one dated line, body is not resent.")]
    public async Task<JsonElement> AppendLog(
        [Description("Note text recorded as a dated log line.")] string note,
        [Description("Id of the artifact to append the note to; omit it to address the artifact by alias + projectId.")] Guid? artifactId = null,
        [Description("Id of the project the feature belongs to; required when alias is used.")] Guid? projectId = null,
        [Description(ArtifactAliasDescription)] string? alias = null,
        [Description(OperationIdDescription)] Guid? operationId = null,
        CancellationToken ct = default)
    {
        var id = await _aliases.AddressArtifactAsync(artifactId, projectId, alias, ct);
        var echo = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/planning/artifacts/{id}/log",
            new { note, source = DoorSource },
            ct, operationId), ArtifactLogReadback(id, note), ct);
        return MutationEcho(echo, "id");
    }

    [McpServerTool(Name = "planning_edit_log", Destructive = false, Idempotent = false)]
    [Description("Corrects the text of the newest log line on one artifact — cheap write tier: one line changes, body is not resent. Preconditions: the target must be the artifact's newest <log> line and carry your own actor marker ([agent] on this door, [ui] on the human door) — not-newest and wrong-actor attempts are 409s naming the line's owner; text must be a single line, multiline replacements are refused because the domain refuses splicing a second line into the log. Date and actor marker are preserved byte-for-byte; only the prose after the marker changes. The edit is version-traced: the previous body is snapshotted and recoverable via planning_read_artifact_diff — nothing is deleted. Feature-level logs are out of scope (append-only there); planning_append_log adds a new line, this tool corrects the line already written.")]
    public async Task<JsonElement> EditLog(
        [Description("Replacement single-line text of the newest log line; the date and the [agent]/[ui] marker are preserved.")] string text,
        [Description("Id of the artifact whose newest log line is edited; omit it to address the artifact by alias + projectId.")] Guid? artifactId = null,
        [Description("Id of the project the feature belongs to; required when alias is used.")] Guid? projectId = null,
        [Description(ArtifactAliasDescription)] string? alias = null,
        [Description(OperationIdDescription)] Guid? operationId = null,
        CancellationToken ct = default)
    {
        var id = await _aliases.AddressArtifactAsync(artifactId, projectId, alias, ct);
        var echo = await _mutations.SendAsync(() => _api.PutAsync(
            $"/api/agent/planning/artifacts/{id}/log",
            new { text },
            ct, operationId), ArtifactLogReadback(id, text), ct);
        return MutationEcho(echo, "id");
    }

    [McpServerTool(Name = "planning_patch_artifact", Destructive = false, Idempotent = false)]
    [Description("Changes one artifact in place without resending its body — the middle tier: replaces one XML section, flips one acceptance item, sets the task brief, or appends a result amendment; rest of the body untouched. A section absent from the body is instead INSERTED — with the content, or declared-empty when content is omitted (tasks accept the declared vocabulary only, spec/plan/result any tag name). Alternatively acIndex/acText to tick one acceptance-criteria item (add untick=true to clear the checkbox instead), or brief to set the one-line task summary (task-only, max 500 chars, no version bump, no log line, body untouched); never more than one. " + AcIndexBasisLine + " — fall back to the index when the text is ambiguous. Text matching is entity-normalized: acText '&lt;key&gt;' matches a stored '<key>' and the reverse, so escaped and readable forms are interchangeable on either side. A miss never succeeds silently: an out-of-range acIndex or an unmatched acText is a 400 listing the visible items. A section-replace of an existing section without content, and an unknown section name on a task, are 400s — the latter lists sections present plus the accepted vocabulary. The log section is append-only: patching it is a 400 that points at planning_append_log / planning_edit_log, and history is rewritten only through a full upsert. Flipping an item already in the requested state is a successful no-op rendering 'already ticked — no change' or 'already unticked — no change'; a replaced or inserted section is a real change every time. Tick and untick echo the resolved item as tickedIndex + tickedText. Editing a validated artifact requires a logNote. The amendment door: amend=true together with content, on a kind=result artifact only — content is appended to the body tail as a dated block '## <amendment> yyyy-MM-dd' followed by the content lines, one block per call, existing text untouched; a supersede note is just a line of the content ('Supersedes: <what and why>'). NO demotion — a validated result stays validated untouched, a done feature stays done — and no human proof is required: logNote is mandatory and records the pre-approval; when the append stales a live approval reply the response's validationProofVoided names it. Works on draft results too; on spec/plan/task the door is refused with a 400 naming the result-only scope.")]
    public async Task<CallToolResult> PatchArtifact(
        [Description("Id of the artifact to patch; omit it to address the artifact by alias + projectId.")] Guid? artifactId = null,
        [Description("Name of the XML section to replace or insert, e.g. goal.")] string? section = null,
        [Description("Full replacement content of the section; omit it to insert a missing section declared-empty. With amend=true this carries the amendment text appended as the dated block instead.")] string? content = null,
        [Description("1-based index of the acceptance-criteria item; numbering matches the materialized mirror and the 400 diagnostics.")] int? acIndex = null,
        [Description("Exact text of the acceptance-criteria item, without the checkbox marker — the recommended selector; entity-escaped and readable forms match alike.")] string? acText = null,
        [Description("Set true together with acIndex or acText to clear the checkbox ([x] to [ ]) instead of ticking it.")] bool untick = false,
        [Description("One-line brief to set on a task artifact, max 500 chars; task-only — spec/plan/result reject it; no version bump.")] string? brief = null,
        [Description("Set true together with content to use the amendment door: content becomes a dated '## <amendment> yyyy-MM-dd' block at the body tail of a kind=result artifact — no demotion, logNote records the pre-approval.")] bool amend = false,
        [Description("Change note recorded in the artifact log; required when the artifact is validated.")] string? logNote = null,
        [Description("Id of the project the feature belongs to; required when alias is used.")] Guid? projectId = null,
        [Description(ArtifactAliasDescription)] string? alias = null,
        [Description(OperationIdDescription)] Guid? operationId = null,
        CancellationToken ct = default)
    {
        var op = PatchOpOf(section, content, acIndex, acText, brief, untick, amend);
        var id = await _aliases.AddressArtifactAsync(artifactId, projectId, alias, ct);
        var patch = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/planning/artifacts/{id}/patch",
            new { op, section, content, acIndex, acText, brief, logNote, source = DoorSource },
            ct, operationId), PatchReadback(id, logNote), ct);

        if (patch.TryGetProperty("noChange", out var noChange) &&
            noChange.ValueKind == JsonValueKind.True)
        {
            return new CallToolResult
            {
                Content = [new TextContentBlock
                {
                    Text = op == "untick_ac" ? AlreadyUntickedNoChangeText : AlreadyTickedNoChangeText
                }]
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

        if (patch.TryGetProperty("tickedIndex", out var tickedIndex) &&
            tickedIndex.ValueKind == JsonValueKind.Number)
        {
            echo["tickedIndex"] = tickedIndex.GetInt32();
        }

        if (patch.TryGetProperty("tickedText", out var tickedText) &&
            tickedText.ValueKind == JsonValueKind.String)
        {
            echo["tickedText"] = tickedText.GetString();
        }

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

        if (patch.TryGetProperty("validationProofVoided", out var validationProofVoided) &&
            validationProofVoided.ValueKind == JsonValueKind.String)
        {
            echo["validationProofVoided"] = validationProofVoided.GetString();
        }

        CarryReplayedFlag(echo, patch);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = echo.ToJsonString() }]
        };
    }

    [McpServerTool(Name = "planning_delete_artifact", Destructive = true, Idempotent = false)]
    [Description("Deletes one artifact as a soft delete: the body and version history stay in the database, the materialized file is removed on the next pass, and an audit line lands in the FEATURE's log (the feature log outlives the artifact; logNote is the reason carried in that line). Guard matrix: agents can delete draft artifacts only; validated/done deletion is the human's door in the UI — a refused attempt comes back as a 409 naming the reason. A task with incoming blockedBy edges is refused with 409 too ('clean up dependencies first') — deletion never orphans dependents. No restore and no force flag in v1.")]
    public async Task<CallToolResult> DeleteArtifact(
        [Description("Id of the artifact to delete; omit it to address the artifact by alias + projectId.")] Guid? artifactId = null,
        [Description("Reason carried in the feature-log audit line.")] string? logNote = null,
        [Description("Door-source marker for the audit line; defaults to agent on this door.")] string? source = null,
        [Description("Id of the project the feature belongs to; required when alias is used.")] Guid? projectId = null,
        [Description(ArtifactAliasDescription)] string? alias = null,
        CancellationToken ct = default)
    {
        var id = await _aliases.AddressArtifactAsync(artifactId, projectId, alias, ct);
        var echo = await _mutations.SendAsync(() => _api.DeleteAsync(
            $"/api/agent/planning/artifacts/{id}",
            new { logNote, source = string.IsNullOrWhiteSpace(source) ? DoorSource : source.Trim() },
            ct), readback: null, ct);

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = new JsonObject
            {
                ["id"] = echo.GetProperty("id").GetString(),
                ["kind"] = echo.GetProperty("kind").GetString(),
                ["featureLogLine"] = echo.GetProperty("featureLogLine").GetString()
            }.ToJsonString() }]
        };
    }

    [McpServerTool(Name = "planning_validate", Destructive = false, Idempotent = false)]
    [Description("Validates a spec, plan or result artifact. When the project requires human validation this fails with a STOP error — ask the user to approve it in the Overmind UI instead of retrying. Validating a spec or plan here requires human-approval proof: send overmind_notify(kind=approval, featureSlug=<the feature's slug>), wait for the human's reply in the notification center, then validate (notify → reply → validate). The reply must be newer than the artifact's last update — every edit voids it, and re-validation needs a fresh reply; without proof the call fails with a ValidationProofRequired 409. One fresh reply may validate the spec and the plan of the same feature in one pass — and any body edit to either voids it for both. The human door (UI) needs no proof, and neither does asMigrated — the migration escape hatch for artifacts pre-approved before import (e.g. already validated on disk): it skips the human gate, requires a logNote carrying the provenance, and leaves a 'validated as migrated — pre-approved on disk' marker line in the artifact log.")]
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
            var echo = await _mutations.SendAsync(() => _api.PostAsync(
                $"/api/agent/planning/artifacts/{artifactId}/validate",
                new { asMigrated, logNote },
                ct), ValidateReadback(artifactId, logNote), ct);
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

    private static string PatchOpOf(
        string? section, string? content, int? acIndex, string? acText, string? brief, bool untick, bool amend)
    {
        var wantsSection = section is not null || content is not null;
        var wantsAc = acIndex is not null || acText is not null;

        if (amend)
        {
            if (brief is not null || section is not null || acIndex is not null || acText is not null || untick)
            {
                throw new McpException(PatchShapeError);
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new McpException(
                    "The amendment door needs non-blank content: the text appended to the result as the dated <amendment> block.");
            }

            return "amend_result";
        }

        if (brief is not null)
        {
            if (wantsSection || wantsAc || untick)
            {
                throw new McpException(PatchShapeError);
            }

            if (string.IsNullOrWhiteSpace(brief) || brief.Contains('\r') || brief.Contains('\n') || brief.Length > 500)
            {
                throw new McpException("Setting a brief needs non-blank single-line text of at most 500 chars.");
            }

            return "set_brief";
        }

        if (wantsSection == wantsAc)
        {
            throw new McpException(PatchShapeError);
        }

        if (wantsSection)
        {
            if (untick)
            {
                throw new McpException(PatchShapeError);
            }

            if (string.IsNullOrWhiteSpace(section) || content is not null && string.IsNullOrWhiteSpace(content))
            {
                throw new McpException(
                    "A section patch needs a non-blank section; omit content to insert a missing section declared-empty, or carry the replacement content.");
            }

            return "replace_section";
        }

        if (acIndex is not null && acText is not null)
        {
            throw new McpException("Flip one acceptance-criteria item by acIndex or by exact acText, not both.");
        }

        if (acText is not null && string.IsNullOrWhiteSpace(acText))
        {
            throw new McpException("Ticking or un-ticking needs acIndex or the exact non-blank acText of the item.");
        }

        return untick ? "untick_ac" : "tick_ac";
    }
}
