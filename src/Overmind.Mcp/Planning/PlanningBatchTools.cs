using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Overmind.Mcp;

namespace Overmind.Mcp.Planning;

using static Overmind.Mcp.Planning.PlanningToolResponses;

[McpServerToolType]
public sealed class PlanningBatchTools
{
    private const string OpsAcceptedForm =
        "ops must be a JSON array of op objects, each carrying op plus the fields of that op — " +
        "e.g. ops=[{\"op\":\"append_log\",\"alias\":\"T2\",\"note\":\"closing\"},{\"op\":\"set_status\",\"alias\":\"T2\",\"status\":\"done\",\"logNote\":\"task closed\"}].";

    private static readonly HashSet<string> OpNames = new(StringComparer.Ordinal)
    {
        "patch", "append_log", "set_status", "upsert_tasks", "notify"
    };

    private readonly OvermindApiClient _api;
    private readonly MutationGuard _mutations;
    private readonly ProjectResolver _projects;

    public PlanningBatchTools(OvermindApiClient api, MutationGuard mutations, ProjectResolver projects)
    {
        _api = api;
        _mutations = mutations;
        _projects = projects;
    }

    [McpServerTool(Name = "planning_batch", Destructive = false, Idempotent = false)]
    [Description("Applies several ceremony ops to ONE feature atomically in a single call — one turn per ceremony step instead of one turn per write: tick an acceptance-criteria item, append the closing log and move the status in one batch. Ops apply in request order under one transaction; any failing op rolls the whole batch back (nothing persists) and its 400 names the index ('ops[1]: …'). Vocabulary (snake_case op names): patch — patch payload of its standalone tool ({op: tick_ac|untick_ac|replace_section|set_brief|amend_result|remove_section} plus that op's fields, same shapes as planning_patch_artifact); append_log — note; set_status — status + logNote; upsert_tasks — tasks[] with the same item shape as planning_upsert_tasks; notify — kind + text plus optional tab/featureSlug/from (featureSlug defaults to the batch feature). patch/append_log/set_status address the artifact by artifactId or alias ('<slug>/T<order>', or the bare 'T<order>'/S/P/R forms — the batch is bound to one feature by the route) and cannot name artifacts created by earlier ops of the same batch (v1). The adapter lints the envelope before any HTTP call: unknown op or missing required params fail locally with no round-trip. Door-level op errors keep the batch atomic; post-commit notification/materialization failures do NOT roll anything back — they ride the response as warnings[]. The result carries featureSlug plus one compact row per op in request order — echo fields only, never artifact bodies.")]
    public async Task<JsonElement> Batch(
        [Description("Slug of the feature every op runs on.")] string featureSlug,
        [Description("Array of ops applied in order, each an object: patch, append_log, set_status, upsert_tasks or notify with that op's required fields.")] JsonElement ops,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        [Description(OperationIdDescription)] Guid? operationId = null,
        CancellationToken ct = default)
    {
        var payload = new JsonObject
        {
            ["ops"] = BuildOps(ops)
        };

        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var batch = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{Uri.EscapeDataString(featureSlug)}/batch",
            payload,
            ct, operationId), readback: null, ct);
        return CompactBatch(batch);
    }

    private static JsonArray BuildOps(JsonElement ops)
    {
        if (ops.ValueKind == JsonValueKind.String)
        {
            try
            {
                if (JsonNode.Parse(ops.GetString()!) is JsonArray encoded)
                {
                    ops = JsonSerializer.SerializeToElement(encoded);
                }
            }
            catch (JsonException)
            {
                throw new McpException($"ops must be a JSON array, not an encoded string. {OpsAcceptedForm}");
            }
        }

        if (ops.ValueKind != JsonValueKind.Array)
        {
            throw new McpException($"ops must be a JSON array of op objects — op fields never sit at the call's top level. {OpsAcceptedForm}");
        }

        if (ops.GetArrayLength() == 0)
        {
            throw new McpException($"ops must carry at least one op. {OpsAcceptedForm}");
        }

        var items = new JsonArray();
        var index = 0;

        foreach (var item in ops.EnumerateArray())
        {
            try
            {
                items.Add(BuildOp(item));
            }
            catch (McpException ex)
            {
                throw new McpException($"ops[{index}]: {ex.Message}");
            }

            index++;
        }

        return items;
    }

    private static JsonObject BuildOp(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw new McpException(
                $"each op must be an object carrying op plus that op's fields. {OpsAcceptedForm}");
        }

        if (!item.TryGetProperty("op", out var op) ||
            op.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(op.GetString()))
        {
            throw new McpException(
                $"op is required on every item and must be a non-blank string. {OpsAcceptedForm}");
        }

        var name = op.GetString()!.Trim();
        if (!OpNames.Contains(name))
        {
            throw new McpException(
                $"Unknown op '{name}'. The batch vocabulary is: patch, append_log, set_status, upsert_tasks, notify.");
        }

        var payload = new JsonObject { ["op"] = name };
        switch (name)
        {
            case "patch":
                CopyAddressing(payload, item, name);
                if (!item.TryGetProperty("patch", out var patch) || patch.ValueKind != JsonValueKind.Object)
                {
                    throw new McpException(
                        "patch requires the patch payload of its standalone tool, e.g. " +
                        "{\"op\":\"tick_ac\",\"acIndex\":1,\"logNote\":\"ticked\"} — section/AC/brief/amend shapes as in planning_patch_artifact.");
                }

                payload["patch"] = JsonNode.Parse(patch.GetRawText())!.AsObject();
                break;
            case "append_log":
                CopyAddressing(payload, item, name);
                payload["note"] = RequireString(item, name, "note");
                break;
            case "set_status":
                CopyAddressing(payload, item, name);
                payload["status"] = RequireString(item, name, "status");
                payload["logNote"] = RequireString(item, name, "logNote");
                break;
            case "upsert_tasks":
                if (!item.TryGetProperty("tasks", out var tasks) || tasks.ValueKind is JsonValueKind.Null)
                {
                    throw new McpException(
                        "upsert_tasks requires a non-empty tasks[] with the same item shape as planning_upsert_tasks — " +
                        "e.g. {\"op\":\"upsert_tasks\",\"tasks\":[{\"body\":\"<goal>Fix the login redirect.</goal>\",\"order\":0}]}.");
                }

                payload["tasks"] = PlanningArtifactTools.BuildTaskItems(tasks);
                break;
            case "notify":
                payload["kind"] = RequireString(item, name, "kind");
                payload["text"] = RequireString(item, name, "text");
                CopyOptionalString(payload, item, "tab");
                CopyOptionalString(payload, item, "featureSlug");
                CopyOptionalString(payload, item, "from");
                break;
        }

        return payload;
    }

    private static void CopyAddressing(JsonObject payload, JsonElement item, string op)
    {
        if (item.TryGetProperty("artifactId", out var artifactId))
        {
            if (artifactId.ValueKind != JsonValueKind.String || !Guid.TryParse(artifactId.GetString(), out _))
            {
                throw new McpException($"{op} artifactId must be a UUID string.");
            }

            payload["artifactId"] = artifactId.GetString();
        }

        if (item.TryGetProperty("alias", out var alias))
        {
            if (alias.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(alias.GetString()))
            {
                throw new McpException($"{op} alias must be a non-blank string.");
            }

            payload["alias"] = alias.GetString();
        }

        if (!payload.ContainsKey("artifactId") && !payload.ContainsKey("alias"))
        {
            throw new McpException(
                $"{op} requires the artifact to mutate: pass artifactId or alias — " +
                "ops cannot name artifacts created by earlier ops of the same batch (v1).");
        }
    }

    private static string RequireString(JsonElement item, string op, string field)
    {
        if (!item.TryGetProperty(field, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new McpException($"{op} requires a non-blank {field} string.");
        }

        return value.GetString()!;
    }

    private static void CopyOptionalString(JsonObject payload, JsonElement item, string field)
    {
        if (item.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString()))
        {
            payload[field] = value.GetString();
        }
    }

    private static JsonElement CompactBatch(JsonElement batch)
    {
        var echo = new JsonObject
        {
            ["featureSlug"] = batch.GetProperty("featureSlug").GetString(),
            ["results"] = CompactResults(batch.GetProperty("results"))
        };

        if (batch.TryGetProperty("warnings", out var warnings) &&
            warnings.ValueKind == JsonValueKind.Array &&
            warnings.GetArrayLength() > 0)
        {
            echo["warnings"] = JsonNode.Parse(warnings.GetRawText());
        }

        CarryReplayedFlag(echo, batch);
        return echo.Deserialize<JsonElement>();
    }

    private static JsonArray CompactResults(JsonElement results)
    {
        var rows = new JsonArray();
        foreach (var row in results.EnumerateArray())
        {
            rows.Add(CompactRow(row));
        }

        return rows;
    }

    private static JsonObject CompactRow(JsonElement row)
    {
        var name = row.GetProperty("op").GetString() ?? string.Empty;
        var compact = new JsonObject
        {
            ["op"] = name,
            ["index"] = row.GetProperty("index").GetInt32()
        };

        if (row.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
        {
            CompactPayload(compact, name, payload);
        }

        if (row.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
        {
            compact["error"] = error.GetString();
        }

        return compact;
    }

    private static void CompactPayload(JsonObject row, string op, JsonElement payload)
    {
        switch (op)
        {
            case "patch":
                row["id"] = payload.GetProperty("artifactId").GetString();
                row["section"] = payload.GetProperty("section").GetString();
                row["version"] = payload.GetProperty("version").GetInt32();
                row["status"] = payload.GetProperty("status").GetString();
                row["bodyChars"] = payload.GetProperty("bodyChars").GetInt32();
                row["lastLogLine"] = payload.GetProperty("lastLogLine").GetString();
                CopyWhenPresent(row, payload, "statusChanged", JsonValueKind.String);
                CopyWhenPresent(row, payload, "featureStatusChanged", JsonValueKind.String);
                CopyWhenPresent(row, payload, "tickedIndex", JsonValueKind.Number);
                CopyWhenPresent(row, payload, "tickedText", JsonValueKind.String);
                CopyWhenPresent(row, payload, "noChange", JsonValueKind.True);
                CopyWhenPresent(row, payload, "validationProofVoided", JsonValueKind.String);
                break;
            case "append_log":
            case "set_status":
                row["id"] = payload.GetProperty("id").GetString();
                row["version"] = payload.GetProperty("version").GetInt32();
                row["status"] = payload.GetProperty("status").GetString();
                row["lastLogLine"] = payload.GetProperty("lastLogLine").GetString();
                CopyWhenPresent(row, payload, "featureStatusChanged", JsonValueKind.String);
                if (payload.TryGetProperty("warnings", out var warnings) &&
                    warnings.ValueKind == JsonValueKind.Array &&
                    warnings.GetArrayLength() > 0)
                {
                    row["warnings"] = JsonNode.Parse(warnings.GetRawText());
                }

                break;
            case "upsert_tasks":
                Merge(row, PlanningArtifactTools.CompactTaskBatch(payload));
                break;
            case "notify":
                row["id"] = payload.GetProperty("id").GetString();
                row["kind"] = payload.GetProperty("kind").GetString();
                CopyWhenPresent(row, payload, "featureSlug", JsonValueKind.String);
                CopyWhenPresent(row, payload, "url", JsonValueKind.String);
                break;
            default:
                row["payload"] = JsonNode.Parse(payload.GetRawText())!.AsObject();
                break;
        }
    }

    private static void CopyWhenPresent(JsonObject row, JsonElement payload, string field, JsonValueKind kind)
    {
        if (payload.TryGetProperty(field, out var value) && value.ValueKind == kind)
        {
            row[field] = JsonNode.Parse(value.GetRawText());
        }
    }

    private static void Merge(JsonObject row, JsonElement compact)
    {
        foreach (var property in compact.EnumerateObject())
        {
            row[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        }
    }
}
