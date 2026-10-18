using System.Text;
using System.Text.Json;

namespace Overmind.Host;

internal static class PlanningCliDocumentText
{
    internal static string Feature(JsonElement feature)
    {
        var builder = new StringBuilder();
        builder.Append($"# {PlanningCliJson.Text(feature, "title")}\n\n{PlanningCliJson.Text(feature, "body")}\n");

        var artifacts = PlanningCliJson.Items(feature, "artifacts");

        AppendDocument(builder, artifacts, "spec", "## Spec");
        AppendDocument(builder, artifacts, "plan", "## Plan");

        var tasks = artifacts
            .Where(a => PlanningCliJson.Text(a, "kind") == "task")
            .OrderBy(a => PlanningCliJson.Number(a, "order"))
            .ToArray();
        if (tasks.Length > 0)
        {
            builder.Append("\n## Tasks\n");
            foreach (var task in tasks)
            {
                var order = PlanningCliJson.Number(task, "order");
                builder.Append($"\n### {order:00}. {TaskHeading(task, order)}\n\n");
                builder.Append($"Status: {PlanningCliJson.Text(task, "status")}");
                var review = PlanningCliJson.Text(task, "review");
                if (review.Length > 0)
                {
                    builder.Append($" · Review: {review}");
                }

                builder.Append($"\n\n{PlanningCliJson.Text(task, "body")}\n");
            }
        }

        AppendDocument(builder, artifacts, "result", "## Result");
        return builder.ToString();
    }

    internal static string Current(JsonElement current)
    {
        var state = PlanningCliJson.Text(current, "state");
        var reason = PlanningCliJson.Text(current, "reason");

        if (state == "stop")
        {
            return $"{reason}\n";
        }

        if (state == "held")
        {
            return $"HELD: {reason}\n";
        }

        if (state == "empty")
        {
            return $"NOTHING TO DO: {reason}\n";
        }

        var artifact = PlanningCliJson.Object(current, "artifact");
        if (artifact.ValueKind != JsonValueKind.Object)
        {
            return reason.Length > 0 ? $"{reason}\n" : string.Empty;
        }

        var feature = PlanningCliJson.Object(current, "feature");
        var builder = new StringBuilder();
        builder.Append(
            $"Current task: {PlanningCliJson.Text(feature, "slug")} "
            + $"#{PlanningCliJson.Number(artifact, "order"):00} [{PlanningCliJson.Text(artifact, "status")}]\n");
        if (reason.Length > 0)
        {
            builder.Append($"\n{reason}\n");
        }

        var warnings = PlanningCliJson.Items(current, "warnings")
            .Select(w => w.ValueKind == JsonValueKind.String ? w.GetString() : null)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToArray();
        if (warnings.Length > 0)
        {
            builder.Append($"\nWarnings: {string.Join(", ", warnings)}\n");
        }

        builder.Append($"\n{PlanningCliJson.Text(artifact, "body")}\n");
        return builder.ToString();
    }

    internal static string Artifact(JsonElement artifact)
    {
        var builder = new StringBuilder();
        var kind = PlanningCliJson.Text(artifact, "kind");
        builder.Append(
            $"{kind} #{PlanningCliJson.Number(artifact, "order"):00} "
            + $"[{PlanningCliJson.Text(artifact, "status")}] v{PlanningCliJson.Number(artifact, "version")}\n");

        var brief = PlanningCliJson.Text(artifact, "brief");
        if (brief.Length > 0)
        {
            builder.Append($"Brief: {brief}\n");
        }

        builder.Append($"\n{PlanningCliJson.Text(artifact, "body")}\n");
        return builder.ToString();
    }

    internal static string Diff(JsonElement diff)
    {
        var builder = new StringBuilder();
        builder.Append(
            $"diff v{PlanningCliJson.Number(diff, "fromVersion")} → v{PlanningCliJson.Number(diff, "toVersion")}\n\n");
        builder.Append($"{PlanningCliJson.Text(diff, "diff")}\n");
        return builder.ToString();
    }

    private static void AppendDocument(StringBuilder builder, JsonElement[] artifacts, string kind, string heading)
    {
        var document = artifacts.FirstOrDefault(a =>
            a.ValueKind == JsonValueKind.Object && PlanningCliJson.Text(a, "kind") == kind);
        if (document.ValueKind == JsonValueKind.Object)
        {
            builder.Append($"\n{heading}\n\n{PlanningCliJson.Text(document, "body")}\n");
        }
    }

    private static string TaskHeading(JsonElement task, int order)
    {
        foreach (var line in PlanningCliJson.Text(task, "body").Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                return trimmed[2..].Trim();
            }
        }

        return $"task-{order:00}";
    }
}
