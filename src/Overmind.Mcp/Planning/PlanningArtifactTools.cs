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
    private const string ArtifactAliasDescription =
        "Optional alias instead of artifactId: <slug>/T<order>.";

    private const string DoorSource = "agent";

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
    [Description("One artifact by id or alias — the full body, or only the named sections, which is cheaper. Task payloads carry review and blockedBy. The named-sections read is the cheapest read on the board.")]
    public async Task<JsonElement> ReadArtifact(
        [Description("Artifact id; omit it when using alias.")] Guid? artifactId = null,
        [Description("Comma-separated section names to narrow the body.")] string? sections = null,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("Unified diff between two artifact versions — hunks only, never bodies. Omit both versions for the two most recent.")]
    public async Task<JsonElement> ReadArtifactDiff(
        [Description("Artifact id; omit it when using alias.")] Guid? artifactId = null,
        [Description("Older version; defaults to toVersion - 1.")] int? fromVersion = null,
        [Description("Newer version; defaults to the current one.")] int? toVersion = null,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("Creates an artifact or replaces its whole body — the most expensive write. One section or AC tick: planning_patch_artifact; N tasks: planning_upsert_tasks.")]
    public async Task<JsonElement> UpsertArtifact(
        [Description("Feature slug.")] string featureSlug,
        [Description("Artifact kind: spec, plan, task or result.")] string kind,
        [Description("Full markdown body of the artifact.")] string body,
        [Description("Task order within the feature; omit to append.")] int? order = null,
        [Description("Review level: light or hard.")] string? review = null,
        [Description("Required when editing a validated artifact.")] string? logNote = null,
        [Description("One-line task summary.")] string? brief = null,
        [Description("Sibling task orders this task waits on.")] int[]? blockedBy = null,
        [Description("Task only: planning_current_task issues this task even while the feature carries an open feature-level blocker.")] bool? independent = null,
        [Description("Validate without persisting anything.")] bool? dryRun = null,
        [Description("Optional project id.")] Guid? projectId = null,
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

        if (independent is not null)
        {
            payload["independent"] = independent;
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
    [Description("Creates or updates N task artifacts atomically — any failing item rolls the batch back. One document: planning_upsert_artifact.")]
    public async Task<JsonElement> UpsertTasks(
        [Description("Feature slug.")] string featureSlug,
        [Description("Array of task items, each with body plus the single-upsert fields.")] JsonElement tasks,
        [Description("Validate the batch without persisting.")] bool? dryRun = null,
        [Description("Optional project id.")] Guid? projectId = null,
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

    internal static JsonArray BuildTaskItems(JsonElement tasks)
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
                $"each item must be an object with body, order, review, logNote, brief, blockedBy, independent. {TaskItemsAcceptedForm}");
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

        if (item.TryGetProperty("independent", out var independent) &&
            independent.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            payload["independent"] = independent.GetBoolean();
        }

        return payload;
    }

    internal static JsonElement CompactTaskBatch(JsonElement batch)
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

            if (artifact.TryGetProperty("independent", out var independent) &&
                independent.ValueKind == JsonValueKind.True)
            {
                compact["independent"] = true;
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
    [Description("Moves an artifact status; logNote is mandatory. spec/plan/result take draft or validated, tasks todo, inProgress, review, done or blocked.")]
    public async Task<JsonElement> SetStatus(
        [Description("New status for the artifact kind.")] string status,
        [Description("Why the status moved.")] string logNote,
        [Description("Artifact id; omit it when using alias.")] Guid? artifactId = null,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("Appends one dated line to an artifact's log, body untouched — the cheapest write.")]
    public async Task<JsonElement> AppendLog(
        [Description("The log line text.")] string note,
        [Description("Artifact id; omit it when using alias.")] Guid? artifactId = null,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("The cheap correction tier: rewrites the text of the newest log line on an artifact, and only your own; older or foreign lines 409.")]
    public async Task<JsonElement> EditLog(
        [Description("New single-line text of that log line.")] string text,
        [Description("Artifact id; omit it when using alias.")] Guid? artifactId = null,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("The middle-tier write — one section or heading block, one AC tick, one task brief, or one result amendment, body never resent. Whole body: planning_upsert_artifact; feature sections: planning_patch_feature.")]
    public async Task<CallToolResult> PatchArtifact(
        [Description("Artifact id; omit it when using alias.")] Guid? artifactId = null,
        [Description("XML section name, or '## Heading' on spec/plan/result.")] string? section = null,
        [Description("Replacement content; omit to insert the section empty.")] string? content = null,
        [Description("1-based acceptance-criteria item number.")] int? acIndex = null,
        [Description("Exact AC item text; the preferred selector.")] string? acText = null,
        [Description("True to clear the checkbox instead of ticking.")] bool untick = false,
        [Description("One-line task summary.")] string? brief = null,
        [Description("True to append a dated result amendment.")] bool amend = false,
        [Description("True to tombstone the resolved block.")] bool remove = false,
        [Description("Reason carried in the tombstone line.")] string? reason = null,
        [Description("Required when the artifact is validated.")] string? logNote = null,
        [Description("Optional project id.")] Guid? projectId = null,
        [Description(ArtifactAliasDescription)] string? alias = null,
        [Description(OperationIdDescription)] Guid? operationId = null,
        CancellationToken ct = default)
    {
        var op = PatchOpOf(section, content, acIndex, acText, brief, untick, amend, remove, reason);
        var id = await _aliases.AddressArtifactAsync(artifactId, projectId, alias, ct);
        var patch = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/planning/artifacts/{id}/patch",
            new { op, section, content, acIndex, acText, brief, logNote, reason, source = DoorSource },
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

        if (patch.TryGetProperty("warnings", out var warnings) &&
            warnings.ValueKind == JsonValueKind.Array && warnings.GetArrayLength() > 0)
        {
            echo["warnings"] = JsonNode.Parse(warnings.GetRawText());
        }

        CarryReplayedFlag(echo, patch);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = echo.ToJsonString() }]
        };
    }

    [McpServerTool(Name = "planning_delete_artifact", Destructive = true, Idempotent = false)]
    [Description("Soft-deletes a draft artifact: the materialized file goes, body and versions stay. Validated artifacts are the human's door. Destructive.")]
    public async Task<CallToolResult> DeleteArtifact(
        [Description("Artifact id; omit it when using alias.")] Guid? artifactId = null,
        [Description("Reason for the audit line.")] string? logNote = null,
        [Description("Door marker; defaults to agent.")] string? source = null,
        [Description("Optional project id.")] Guid? projectId = null,
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
    [Description("Marks a spec, plan or result validated. Spec and plan need a human approval reply newer than the artifact's last edit.")]
    public async Task<CallToolResult> Validate(
        [Description("Id of the artifact to validate.")] Guid artifactId,
        [Description("Skip the human gate for a pre-approved import.")] bool? asMigrated = null,
        [Description("Required with asMigrated: the provenance.")] string? logNote = null,
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
        string? section, string? content, int? acIndex, string? acText, string? brief, bool untick, bool amend,
        bool remove, string? reason)
    {
        var wantsSection = section is not null || content is not null;
        var wantsAc = acIndex is not null || acText is not null;

        if (amend)
        {
            if (brief is not null || section is not null || acIndex is not null || acText is not null ||
                untick || remove || reason is not null)
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
            if (wantsSection || wantsAc || untick || remove || reason is not null)
            {
                throw new McpException(PatchShapeError);
            }

            if (string.IsNullOrWhiteSpace(brief) || brief.Contains('\r') || brief.Contains('\n'))
            {
                // Length is softened server-side (266): the brief cap answers with warnings[],
                // only blank and multi-line stay pre-flight rejects here.
                throw new McpException("Setting a brief needs non-blank single-line text.");
            }

            return "set_brief";
        }

        if (remove)
        {
            if (wantsAc || untick)
            {
                throw new McpException(PatchShapeError);
            }

            if (string.IsNullOrWhiteSpace(section) || content is not null)
            {
                throw new McpException(
                    "The tombstone mode needs the section to remove; content must be omitted — the block is replaced by the <removed> line, not new content.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new McpException(
                    "remove=true needs reason: it is carried in the '<removed YYYY-MM-DD — reason>' tombstone line and is what keeps the block from being silently resurrected.");
            }

            return "remove_section";
        }

        if (reason is not null)
        {
            throw new McpException(
                "reason belongs to the tombstone mode: pair it with remove=true and the section to remove.");
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
