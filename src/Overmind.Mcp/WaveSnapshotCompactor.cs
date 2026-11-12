using System.Text.Json;
using System.Text.Json.Nodes;

namespace Overmind.Mcp;

internal static class WaveSnapshotCompactor
{
    public static JsonElement Compact(JsonElement snapshot)
    {
        if (snapshot.ValueKind != JsonValueKind.Object)
        {
            return snapshot;
        }

        var payload = JsonNode.Parse(snapshot.GetRawText())!.AsObject();
        CompactSection(payload, "queue", CompactQueueRow);
        CompactSection(payload, "claims", CompactClaimRow);
        CompactSection(payload, "entries", CompactEntryRow);
        CompactSection(payload, "discovery", CompactDiscoveryRow);
        return payload.Deserialize<JsonElement>();
    }

    private static void CompactSection(
        JsonObject payload, string section, Func<JsonObject, JsonObject> project)
    {
        if (payload.TryGetPropertyValue(section, out var rows) && rows is JsonArray array)
        {
            payload[section] = new JsonArray(array
                .Select(row => row as JsonObject is { } item ? project(item) : row?.DeepClone())
                .ToArray());
        }
    }

    internal static JsonObject CompactQueueRow(JsonObject row)
    {
        var compact = new JsonObject
        {
            ["slug"] = row["slug"]?.DeepClone(),
            ["number"] = row["number"]?.DeepClone(),
            ["title"] = row["title"]?.DeepClone(),
            ["priority"] = row["priority"]?.DeepClone(),
            ["status"] = row["status"]?.DeepClone(),
            ["queuePosition"] = row["queuePosition"]?.DeepClone(),
            ["phase"] = row["phase"]?.DeepClone()
        };

        if (Int32Of(row, "readiness") > 0)
        {
            compact["readiness"] = row["readiness"]!.DeepClone();
        }

        if (Int32Of(row, "taskTotal") > 0)
        {
            compact["taskDone"] = row["taskDone"]?.DeepClone();
            compact["taskTotal"] = row["taskTotal"]!.DeepClone();
        }

        if (BooleanOf(row, "isBlocked"))
        {
            compact["isBlocked"] = row["isBlocked"]!.DeepClone();
        }

        if (row["blockedBy"] is JsonArray blockedBy && blockedBy.Count > 0)
        {
            compact["blockedBy"] = blockedBy.DeepClone();
        }

        if (row["claimedByAlias"] is not null)
        {
            compact["claimedByAlias"] = row["claimedByAlias"]!.DeepClone();
            compact["claimedByVendor"] = row["claimedByVendor"]?.DeepClone();
            compact["claimedByHuman"] =
                BooleanOf(row, "claimedByHuman") ? row["claimedByHuman"]!.DeepClone() : null;
            compact["claimedAt"] = row["claimedAt"]?.DeepClone();
            compact["claimState"] = row["claimState"]?.DeepClone();
        }

        if (row["lane"] is not null)
        {
            compact["lane"] = row["lane"]!.DeepClone();
            compact["laneBoundAlias"] = row["laneBoundAlias"]?.DeepClone();
            compact["laneHolder"] = row["laneHolder"]?.DeepClone();
        }

        if (row["potentialOverlaps"] is JsonArray overlaps && overlaps.Count > 0)
        {
            compact["potentialOverlaps"] = overlaps.DeepClone();
        }

        if (Int32Of(row, "openDirectiveCount") > 0)
        {
            compact["openDirectiveCount"] = row["openDirectiveCount"]!.DeepClone();
        }

        if (BooleanOf(row, "stale"))
        {
            compact["stale"] = row["stale"]!.DeepClone();
        }

        return DropNulls(compact);
    }

    private static JsonObject CompactClaimRow(JsonObject row) => DropNulls(new JsonObject
    {
        ["slug"] = row["slug"]?.DeepClone(),
        ["claimedByAlias"] = row["claimedByAlias"]?.DeepClone(),
        ["claimedByVendor"] = row["claimedByVendor"]?.DeepClone(),
        ["claimedByHuman"] =
            BooleanOf(row, "claimedByHuman") ? row["claimedByHuman"]!.DeepClone() : null,
        ["claimedAt"] = row["claimedAt"]?.DeepClone(),
        ["claimState"] = row["claimState"]?.DeepClone(),
        ["queuePosition"] = row["queuePosition"]?.DeepClone(),
        ["priority"] = row["priority"]?.DeepClone(),
        ["status"] = row["status"]?.DeepClone()
    });

    private static JsonObject CompactEntryRow(JsonObject row) => DropNulls(new JsonObject
    {
        ["id"] = row["id"]?.DeepClone(),
        ["type"] = row["type"]?.DeepClone(),
        ["title"] = row["title"]?.DeepClone(),
        ["tags"] = row["tags"] is JsonArray tags && tags.Count > 0 ? tags.DeepClone() : null,
        ["updatedAt"] = row["updatedAt"]?.DeepClone()
    });

    private static JsonObject CompactDiscoveryRow(JsonObject row)
    {
        var clone = (JsonObject)row.DeepClone();
        clone.Remove("workspace");
        return clone;
    }

    private static JsonObject DropNulls(JsonObject row)
    {
        foreach (var key in row.Where(pair => pair.Value is null).Select(pair => pair.Key).ToArray())
        {
            row.Remove(key);
        }

        return row;
    }

    private static int Int32Of(JsonObject row, string key) =>
        row[key] is JsonValue value && value.TryGetValue(out int number) ? number : 0;

    private static bool BooleanOf(JsonObject row, string key) =>
        row[key] is JsonValue value && value.TryGetValue(out bool flag) && flag;
}
