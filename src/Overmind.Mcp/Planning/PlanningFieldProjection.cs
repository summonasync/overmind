using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using Overmind.Mcp;

namespace Overmind.Mcp.Planning;

internal static class PlanningFieldProjection
{
    internal const string IdKey = "slug";
    internal const string QueueRowsProperty = "items";

    internal const string BothKnobsError =
        "fields and compact are mutually exclusive projections: pass fields for a custom {slug, ...requested} slice " +
        "or compact for the predefined shape — never both.";

    internal const string EmptyFieldsError =
        "fields names no keys — a projection of nothing is a caller bug: list at least one allowed field or omit the knob.";

    internal static readonly string[] BoardFields =
    [
        "slug", "number", "title", "priority", "status", "queuePosition", "specStatus", "planStatus",
        "taskDone", "taskTotal", "taskCounts", "readiness", "blockedBy", "isBlocked", "phase", "briefExcerpt",
        "claimedByAlias", "claimedByVendor", "claimedAt", "claimState", "lane", "createdAt", "updatedAt"
    ];

    internal static readonly string[] QueueFields =
    [
        .. BoardFields,
        "claimedByHuman", "openDirectiveCount", "laneHolder", "laneBoundAlias", "potentialOverlaps",
        "pendingMutations", "lastAttemptAt", "lastSuccessAt", "stale"
    ];

    internal static readonly string[] FeatureReadFields =
    [
        "slug", "title", "body", "status", "priority", "version", "createdAt", "updatedAt", "artifacts",
        "isBlocked", "blockedBy", "blocks", "claimedByAlias", "claimedByVendor", "claimedAt", "claimState",
        "claimedByHuman", "pinnedBranch", "pinnedWorktreePath", "lane", "codeSurfaces", "potentialOverlaps",
        "openDirectives", "openDirectiveCount", "missingSections"
    ];

    private static readonly string[] CarriedArtifactFields = ["brief", "acTicked", "acTotal", "blocked", "blockedBy"];

    private static readonly string[] CarriedFeatureFields =
    [
        "blockedBy", "blocks", "claimedByAlias", "claimedByVendor", "claimedAt", "claimState",
        "pinnedBranch", "pinnedWorktreePath", "lane", "openDirectiveCount", "missingSections"
    ];

    internal static string[] ResolveRequested(string? fields, bool compact, string[] allowed)
    {
        var requested = ParseFields(fields);
        if (requested.Length > 0 && compact)
        {
            throw new McpException(BothKnobsError);
        }

        if (fields is not null && requested.Length == 0)
        {
            throw new McpException(EmptyFieldsError);
        }

        var unknown = requested.Where(field => !allowed.Contains(field)).ToArray();
        if (unknown.Length > 0)
        {
            throw new McpException(
                $"Unknown field(s) '{string.Join(", ", unknown)}' in fields. Allowed fields: {string.Join(", ", allowed)}.");
        }

        return requested;
    }

    internal static JsonElement ProjectRows(JsonElement payload, string? rowsProperty, string[] requested) =>
        MapRows(payload, rowsProperty, row => ProjectRow(row, requested));

    internal static JsonElement CompactRows(JsonElement payload, string? rowsProperty) =>
        MapRows(payload, rowsProperty, WaveSnapshotCompactor.CompactQueueRow);

    internal static JsonElement ProjectObject(JsonElement payload, string[] requested) =>
        payload.ValueKind == JsonValueKind.Object
            ? ProjectRow(JsonObjectOf(payload), requested).Deserialize<JsonElement>()
            : payload;

    internal static JsonElement CompactFeatureRead(JsonElement feature)
    {
        var compact = new JsonObject
        {
            ["slug"] = CopyOf(feature, "slug"),
            ["title"] = CopyOf(feature, "title"),
            ["status"] = CopyOf(feature, "status"),
            ["priority"] = CopyOf(feature, "priority"),
            ["version"] = CopyOf(feature, "version"),
            ["createdAt"] = CopyOf(feature, "createdAt"),
            ["updatedAt"] = CopyOf(feature, "updatedAt"),
            ["bodyChars"] = BodyCharsOf(feature),
            ["isBlocked"] = CopyOf(feature, "isBlocked")
        };

        if (feature.TryGetProperty("artifacts", out var artifacts) && artifacts.ValueKind == JsonValueKind.Array)
        {
            compact["artifacts"] = new JsonArray(artifacts.EnumerateArray()
                .Where(artifact => artifact.ValueKind == JsonValueKind.Object)
                .Select(CompactArtifactRow)
                .ToArray());
        }

        foreach (var field in CarriedFeatureFields)
        {
            if (feature.TryGetProperty(field, out var value))
            {
                compact[field] = JsonNode.Parse(value.GetRawText());
            }
        }

        return compact.Deserialize<JsonElement>();
    }

    private static string[] ParseFields(string? fields)
    {
        if (string.IsNullOrWhiteSpace(fields))
        {
            return [];
        }

        var requested = new List<string>();
        foreach (var token in fields.Split([',', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!requested.Contains(token))
            {
                requested.Add(token);
            }
        }

        return [.. requested];
    }

    private static JsonObject ProjectRow(JsonObject row, string[] requested)
    {
        var projected = new JsonObject();
        if (row.TryGetPropertyValue(IdKey, out var id))
        {
            projected[IdKey] = id?.DeepClone();
        }

        foreach (var field in requested.Where(field => field != IdKey))
        {
            if (row.TryGetPropertyValue(field, out var value))
            {
                projected[field] = value?.DeepClone();
            }
        }

        return projected;
    }

    private static JsonObject CompactArtifactRow(JsonElement artifact)
    {
        var row = new JsonObject
        {
            ["kind"] = CopyOf(artifact, "kind"),
            ["order"] = CopyOf(artifact, "order"),
            ["status"] = CopyOf(artifact, "status")
        };

        foreach (var field in CarriedArtifactFields)
        {
            if (artifact.TryGetProperty(field, out var value))
            {
                row[field] = JsonNode.Parse(value.GetRawText());
            }
        }

        row["lastLogLine"] = PlanningToolResponses.LastLogLineOf(artifact);
        return row;
    }

    private static int BodyCharsOf(JsonElement feature) =>
        feature.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
            ? body.GetString()?.Length ?? 0
            : 0;

    private static JsonNode? CopyOf(JsonElement parent, string field) =>
        parent.TryGetProperty(field, out var value) ? JsonNode.Parse(value.GetRawText()) : null;

    private static JsonElement MapRows(JsonElement payload, string? rowsProperty, Func<JsonObject, JsonObject> map)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            return new JsonArray(payload.EnumerateArray()
                    .Select(row => row.ValueKind == JsonValueKind.Object ? map(JsonObjectOf(row)) : JsonNode.Parse(row.GetRawText()))
                    .ToArray())
                .Deserialize<JsonElement>();
        }

        if (rowsProperty is null ||
            payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(rowsProperty, out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return payload;
        }

        var envelope = JsonNode.Parse(payload.GetRawText())!.AsObject();
        envelope[rowsProperty] = new JsonArray(rows.EnumerateArray()
            .Select(row => row.ValueKind == JsonValueKind.Object ? map(JsonObjectOf(row)) : JsonNode.Parse(row.GetRawText()))
            .ToArray());
        return envelope.Deserialize<JsonElement>();
    }

    private static JsonObject JsonObjectOf(JsonElement row) => JsonNode.Parse(row.GetRawText())!.AsObject();
}
