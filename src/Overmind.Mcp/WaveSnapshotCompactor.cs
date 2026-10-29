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

    private static JsonObject CompactQueueRow(JsonObject row) => new()
    {
        ["slug"] = row["slug"]?.DeepClone(),
        ["number"] = row["number"]?.DeepClone(),
        ["title"] = row["title"]?.DeepClone(),
        ["priority"] = row["priority"]?.DeepClone(),
        ["status"] = row["status"]?.DeepClone(),
        ["queuePosition"] = row["queuePosition"]?.DeepClone(),
        ["phase"] = row["phase"]?.DeepClone(),
        ["readiness"] = row["readiness"]?.DeepClone(),
        ["taskDone"] = row["taskDone"]?.DeepClone(),
        ["taskTotal"] = row["taskTotal"]?.DeepClone(),
        ["isBlocked"] = row["isBlocked"]?.DeepClone(),
        ["blockedBy"] = row["blockedBy"]?.DeepClone(),
        ["claimedByAlias"] = row["claimedByAlias"]?.DeepClone(),
        ["claimedByVendor"] = row["claimedByVendor"]?.DeepClone(),
        ["claimedByHuman"] = row["claimedByHuman"]?.DeepClone(),
        ["claimedAt"] = row["claimedAt"]?.DeepClone(),
        ["claimState"] = row["claimState"]?.DeepClone(),
        ["lane"] = row["lane"]?.DeepClone(),
        ["laneBoundAlias"] = row["laneBoundAlias"]?.DeepClone(),
        ["laneHolder"] = row["laneHolder"]?.DeepClone(),
        ["potentialOverlaps"] = row["potentialOverlaps"]?.DeepClone(),
        ["openDirectiveCount"] = row["openDirectiveCount"]?.DeepClone(),
        ["stale"] = row["stale"]?.DeepClone()
    };

    private static JsonObject CompactClaimRow(JsonObject row) => new()
    {
        ["slug"] = row["slug"]?.DeepClone(),
        ["claimedByAlias"] = row["claimedByAlias"]?.DeepClone(),
        ["claimedByVendor"] = row["claimedByVendor"]?.DeepClone(),
        ["claimedByHuman"] = row["claimedByHuman"]?.DeepClone(),
        ["claimedAt"] = row["claimedAt"]?.DeepClone(),
        ["claimState"] = row["claimState"]?.DeepClone(),
        ["queuePosition"] = row["queuePosition"]?.DeepClone(),
        ["priority"] = row["priority"]?.DeepClone(),
        ["status"] = row["status"]?.DeepClone()
    };

    private static JsonObject CompactEntryRow(JsonObject row) => new()
    {
        ["id"] = row["id"]?.DeepClone(),
        ["type"] = row["type"]?.DeepClone(),
        ["title"] = row["title"]?.DeepClone(),
        ["tags"] = row["tags"]?.DeepClone(),
        ["updatedAt"] = row["updatedAt"]?.DeepClone()
    };
}
