using System.Text;
using System.Text.Json;

namespace Overmind.Host;

internal static class PlanningCliMutationText
{
    internal static string Claim(JsonElement echo)
    {
        var alias = PlanningCliJson.Text(echo, "claimedByAlias");
        var builder = new StringBuilder();
        builder.Append(alias.Length == 0
            ? $"{PlanningCliJson.Text(echo, "slug")} · unclaimed\n"
            : $"{PlanningCliJson.Text(echo, "slug")} · claim {alias}/{PlanningCliJson.Text(echo, "claimedByVendor")}"
                + $" ({PlanningCliJson.Text(echo, "claimState")})\n");

        var lane = PlanningCliJson.Text(echo, "lane");
        if (lane.Length > 0)
        {
            builder.Append($"lane: {lane}\n");
        }

        if (PlanningCliJson.Flag(echo, "overrideLaneCheck"))
        {
            builder.Append("lane check overridden\n");
        }

        return builder.ToString();
    }

    internal static string Mutation(JsonElement mutation)
    {
        var builder = new StringBuilder();
        var status = PlanningCliJson.Text(mutation, "status");
        var version = PlanningCliJson.Number(mutation, "version");
        if (status.Length > 0 || version > 0)
        {
            builder.Append($"[{status}] v{version}\n");
        }

        AppendLogLine(builder, mutation);
        AppendWarnings(builder, mutation);
        AppendFeatureStatusChanged(builder, mutation);
        return builder.Length == 0 ? $"{mutation}\n" : builder.ToString();
    }

    internal static string Patch(JsonElement patch)
    {
        var builder = new StringBuilder();
        builder.Append(
            $"{PlanningCliJson.Text(patch, "kind")} · {PlanningCliJson.Text(patch, "section")}"
            + $" [{PlanningCliJson.Text(patch, "status")}] v{PlanningCliJson.Number(patch, "version")}\n");

        if (patch.TryGetProperty("tickedIndex", out var ticked) && ticked.ValueKind == JsonValueKind.Number)
        {
            builder.Append($"acceptance item {ticked.GetInt32()}: {PlanningCliJson.Text(patch, "tickedText")}\n");
        }

        if (patch.TryGetProperty("noChange", out var noChange) && noChange.ValueKind == JsonValueKind.True)
        {
            builder.Append("already in the requested state — no change\n");
        }

        AppendLogLine(builder, patch);
        AppendFeatureStatusChanged(builder, patch);
        return builder.ToString();
    }

    internal static string TasksBatch(JsonElement batch)
    {
        var builder = new StringBuilder();
        builder.Append(
            $"tasks created {PlanningCliJson.Number(batch, "created")}"
            + $", updated {PlanningCliJson.Number(batch, "updated")}\n");

        foreach (var artifact in PlanningCliJson.Items(batch, "artifacts"))
        {
            builder.Append(
                $"  {PlanningCliJson.Text(artifact, "kind")} #{PlanningCliJson.Number(artifact, "order"):00}"
                + $" [{PlanningCliJson.Text(artifact, "status")}]"
                + $" {BriefOf(artifact)}\n");
        }

        return builder.ToString();
    }

    internal static string FeatureUpdate(JsonElement update)
    {
        var builder = new StringBuilder();
        builder.Append(
            $"{PlanningCliJson.Text(update, "slug")} [{PlanningCliJson.Text(update, "status")}]"
            + $" v{PlanningCliJson.Number(update, "version")}"
            + $" · body {PlanningCliJson.Number(update, "bodyChars")} chars\n");
        return builder.ToString();
    }

    internal static string DirectiveAck(JsonElement ack)
    {
        var builder = new StringBuilder();
        builder.Append(PlanningCliJson.Flag(ack, "ack")
            ? $"directive {PlanningCliJson.Text(ack, "id")} acknowledged"
            : $"directive {PlanningCliJson.Text(ack, "id")} NOT acknowledged");

        var alias = PlanningCliJson.Text(ack, "ackByAlias");
        if (alias.Length > 0)
        {
            builder.Append($" by {alias}/{PlanningCliJson.Text(ack, "ackVendor")}");
        }

        var at = PlanningCliJson.Text(ack, "ackAt");
        if (at.Length > 0)
        {
            builder.Append($" at {at}");
        }

        builder.Append('\n');
        return builder.ToString();
    }

    private static void AppendLogLine(StringBuilder builder, JsonElement mutation)
    {
        var logLine = PlanningCliJson.Text(mutation, "lastLogLine");
        if (logLine.Length > 0)
        {
            builder.Append($"{logLine}\n");
        }
    }

    private static void AppendWarnings(StringBuilder builder, JsonElement mutation)
    {
        var warnings = PlanningCliJson.Items(mutation, "warnings")
            .Select(w => w.ValueKind == JsonValueKind.String ? w.GetString() : null)
            .Where(w => !string.IsNullOrWhiteSpace(w));
        foreach (var warning in warnings)
        {
            builder.Append($"warning: {warning}\n");
        }
    }

    private static void AppendFeatureStatusChanged(StringBuilder builder, JsonElement mutation)
    {
        var changed = PlanningCliJson.Text(mutation, "featureStatusChanged");
        if (changed.Length > 0)
        {
            builder.Append($"feature status → {changed}\n");
        }
    }

    private static string BriefOf(JsonElement artifact)
    {
        var brief = PlanningCliJson.Text(artifact, "brief");
        return brief.Length == 0 ? string.Empty : $" — {brief}";
    }
}
