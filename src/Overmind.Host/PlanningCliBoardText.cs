using System.Text;
using System.Text.Json;

namespace Overmind.Host;

internal static class PlanningCliBoardText
{
    internal static string Projects(JsonElement projects)
    {
        var rows = projects.ValueKind == JsonValueKind.Array ? projects.EnumerateArray().ToArray() : [];
        if (rows.Length == 0)
        {
            return "no projects registered\n";
        }

        var builder = new StringBuilder();
        foreach (var project in rows)
        {
            builder.Append(
                $"- {PlanningCliJson.Text(project, "name")} [{PlanningCliJson.Text(project, "id")}] "
                + $"· open features {PlanningCliJson.Number(project, "openFeatureCount")}\n");
            var diskPath = PlanningCliJson.Text(project, "diskPath");
            if (diskPath.Length > 0)
            {
                builder.Append($"    {diskPath}\n");
            }
        }

        return builder.ToString();
    }

    internal static string Board(JsonElement board)
    {
        var rows = board.ValueKind == JsonValueKind.Array ? board.EnumerateArray().ToArray() : [];
        if (rows.Length == 0)
        {
            return "no features on the board\n";
        }

        var builder = new StringBuilder();
        foreach (var feature in rows)
        {
            builder.Append(
                $"- {PlanningCliJson.Text(feature, "slug")} [{PlanningCliJson.Text(feature, "status")}]"
                + $" · {PlanningCliJson.Text(feature, "priority")}"
                + $" — {PlanningCliJson.Text(feature, "title")}"
                + $" · tasks {PlanningCliJson.Number(feature, "taskDone")}/{PlanningCliJson.Number(feature, "taskTotal")}"
                + $" · spec {Status(feature, "specStatus")}"
                + $" · plan {Status(feature, "planStatus")}\n");

            AppendClaim(builder, feature);
            if (PlanningCliJson.Flag(feature, "isBlocked"))
            {
                builder.Append("    BLOCKED\n");
            }
        }

        return builder.ToString();
    }

    internal static string Queue(JsonElement queue)
    {
        var rows = PlanningCliJson.Items(queue, "items");
        if (rows.Length == 0)
        {
            return "queue is empty\n";
        }

        var builder = new StringBuilder();
        foreach (var item in rows)
        {
            builder.Append(
                $"{PlanningCliJson.Number(item, "queuePosition"):00}. {PlanningCliJson.Text(item, "slug")}"
                + $" [{PlanningCliJson.Text(item, "priority")}] {PlanningCliJson.Text(item, "status")}"
                + $" — {PlanningCliJson.Text(item, "title")}"
                + $" · tasks {PlanningCliJson.Number(item, "taskDone")}/{PlanningCliJson.Number(item, "taskTotal")}"
                + $" · spec {Status(item, "specStatus")}"
                + $" · plan {Status(item, "planStatus")}"
                + $" · {PlanningCliJson.Text(item, "phase")}\n");

            var blockedBy = PlanningCliJson.Items(item, "blockedBy")
                .Select(b => b.ValueKind == JsonValueKind.String ? b.GetString() : null)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .ToArray();
            if (blockedBy.Length > 0)
            {
                builder.Append($"    blocked by: {string.Join(", ", blockedBy)}\n");
            }

            AppendClaim(builder, item);
        }

        return builder.ToString();
    }

    internal static string Lanes(JsonElement lanes)
    {
        var rows = PlanningCliJson.Items(lanes, "lanes");
        if (rows.Length == 0)
        {
            return "no lanes on the roster\n";
        }

        var builder = new StringBuilder();
        foreach (var lane in rows)
        {
            builder.Append($"- {PlanningCliJson.Text(lane, "name")}");
            var serialOrder = PlanningCliJson.Number(lane, "serialOrder");
            if (serialOrder > 0)
            {
                builder.Append($" · serial {serialOrder}");
            }

            builder.Append($" · open features {PlanningCliJson.Number(lane, "openFeatureCount")}");

            var holder = PlanningCliJson.Object(lane, "holder");
            builder.Append(holder.ValueKind == JsonValueKind.Object
                ? $" · holder {PlanningCliJson.Text(holder, "alias")}/{PlanningCliJson.Text(holder, "vendor")}"
                    + $" ({PlanningCliJson.Text(holder, "claimState")})\n"
                : " · holder none\n");

            foreach (var feature in PlanningCliJson.Items(lane, "features"))
            {
                builder.Append(
                    $"    {PlanningCliJson.Text(feature, "slug")} [{PlanningCliJson.Text(feature, "status")}]\n");
            }
        }

        return builder.ToString();
    }

    internal static string Materialization(JsonElement status)
    {
        var builder = new StringBuilder();
        builder.Append($"strategy: {PlanningCliJson.Text(status, "strategy")}\n");
        builder.Append($"targetDir: {PlanningCliJson.Text(status, "targetDir")}\n");
        builder.Append($"lastSuccess: {Stamp(status, "lastSuccess")}\n");
        builder.Append($"lastAttempt: {Stamp(status, "lastAttempt")}\n");

        var reason = PlanningCliJson.Text(status, "lastAttemptReason");
        if (reason.Length > 0)
        {
            builder.Append($"lastAttemptReason: {reason}\n");
        }

        builder.Append($"trackedFiles: {PlanningCliJson.Number(status, "trackedFiles")}\n");
        var stale = PlanningCliJson.Items(status, "stalePaths");
        builder.Append($"stalePaths: {stale.Length}\n");
        foreach (var path in stale)
        {
            builder.Append($"    {path}\n");
        }

        return builder.ToString();
    }

    private static void AppendClaim(StringBuilder builder, JsonElement row)
    {
        var alias = PlanningCliJson.Text(row, "claimedByAlias");
        if (alias.Length == 0)
        {
            return;
        }

        builder.Append(
            $"    claim {alias}/{PlanningCliJson.Text(row, "claimedByVendor")}"
            + $" ({PlanningCliJson.Text(row, "claimState")})\n");
    }

    private static string Status(JsonElement element, string property)
    {
        var status = PlanningCliJson.Text(element, property);
        return status.Length == 0 ? "none" : status;
    }

    private static string Stamp(JsonElement element, string property)
    {
        var stamp = PlanningCliJson.Text(element, property);
        return stamp.Length == 0 ? "none" : stamp;
    }
}
