using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerResourceType]
public sealed class OvermindPlanningResources
{
    private readonly OvermindApiClient _api;

    public OvermindPlanningResources(OvermindApiClient api)
    {
        _api = api;
    }

    [McpServerResource(UriTemplate = "overmind://projects/{id}/planning/features", Name = "project-planning-features", Title = "Project planning board", MimeType = "text/markdown")]
    [Description("The planning board of a project: one markdown line per feature with status, task progress and spec/plan validation.")]
    public async Task<string> GetBoard(Guid id, CancellationToken ct)
    {
        var board = await Guarded(() => _api.GetAsync($"/api/agent/projects/{id}/planning/features", ct));
        return RenderBoard(board);
    }

    [McpServerResource(UriTemplate = "overmind://projects/{id}/planning/features/{slug}", Name = "project-planning-feature", Title = "Project planning feature", MimeType = "text/markdown")]
    [Description("One planning feature rendered as markdown: feature body, spec, plan, tasks with status lines, result.")]
    public async Task<string> GetFeature(Guid id, string slug, CancellationToken ct)
    {
        var feature = await Guarded(() => _api.GetAsync(
            $"/api/agent/projects/{id}/planning/features/{Uri.EscapeDataString(slug)}", ct));
        return RenderFeature(feature);
    }

    [McpServerResource(UriTemplate = "overmind://projects/{id}/planning/features/{slug}/current", Name = "project-planning-current", Title = "Current planning task", MimeType = "text/markdown")]
    [Description("The current task of a feature prefixed with 'Current task:' (soft spec/plan warnings listed beneath), or a STOP / HELD / NOTHING TO DO first line when there is nothing the agent may do.")]
    public async Task<string> GetCurrent(Guid id, string slug, CancellationToken ct)
    {
        var current = await Guarded(() => _api.GetAsync(
            $"/api/agent/projects/{id}/planning/features/{Uri.EscapeDataString(slug)}/current", ct));
        return RenderCurrent(current);
    }

    private static string RenderBoard(JsonElement board)
    {
        var builder = new StringBuilder();
        if (board.ValueKind != JsonValueKind.Array)
        {
            return builder.ToString();
        }

        foreach (var item in board.EnumerateArray())
        {
            builder.Append($"- {StringOf(item, "slug")} [{StringOf(item, "status")}] — {StringOf(item, "title")}");
            builder.Append($" · tasks {IntOf(item, "taskDone")}/{IntOf(item, "taskTotal")}");
            builder.Append($" · spec {StatusOf(item, "specStatus")}");
            builder.Append($" · plan {StatusOf(item, "planStatus")}\n");
        }

        return builder.ToString();
    }

    private static string RenderFeature(JsonElement feature)
    {
        var builder = new StringBuilder();
        builder.Append($"# {StringOf(feature, "title")}\n\n{StringOf(feature, "body")}\n");

        var artifacts = feature.TryGetProperty("artifacts", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().ToArray()
            : [];

        AppendDocumentSection(builder, "## Spec", artifacts, "spec");
        AppendDocumentSection(builder, "## Plan", artifacts, "plan");
        AppendTasksSection(builder, artifacts);
        AppendDocumentSection(builder, "## Result", artifacts, "result");

        return builder.ToString();
    }

    private static void AppendDocumentSection(StringBuilder builder, string heading, JsonElement[] artifacts, string kind)
    {
        var artifact = artifacts.FirstOrDefault(a => KindOf(a) == kind && a.ValueKind == JsonValueKind.Object);
        if (artifact.ValueKind == JsonValueKind.Object)
        {
            builder.Append($"\n{heading}\n\n{StringOf(artifact, "body")}\n");
        }
    }

    private static void AppendTasksSection(StringBuilder builder, JsonElement[] artifacts)
    {
        var tasks = artifacts
            .Where(a => KindOf(a) == "task" && a.ValueKind == JsonValueKind.Object)
            .OrderBy(t => IntOf(t, "order"))
            .ToArray();
        if (tasks.Length == 0)
        {
            return;
        }

        builder.Append("\n## Tasks\n");
        foreach (var task in tasks)
        {
            var order = IntOf(task, "order");
            builder.Append($"\n### {order:00}. {TaskHeading(task, order)}\n\n");
            builder.Append($"Status: {StringOf(task, "status")}");
            var review = StringOf(task, "review");
            if (review.Length > 0)
            {
                builder.Append($" · Review: {review}");
            }

            builder.Append($"\n\n{StringOf(task, "body")}\n");
        }
    }

    private static string TaskHeading(JsonElement task, int order)
    {
        foreach (var line in StringOf(task, "body").Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                return trimmed[2..].Trim();
            }
        }

        return $"task-{order:00}";
    }

    private static string RenderCurrent(JsonElement current)
    {
        var state = StringOf(current, "state");
        var reason = StringOf(current, "reason");

        if (state == "stop")
        {
            return $"{reason}\n";
        }

        if (state == "empty")
        {
            return $"NOTHING TO DO: {reason}\n";
        }

        if (state == "held")
        {
            return $"HELD: {reason}\n";
        }

        if (current.TryGetProperty("artifact", out var artifact) && artifact.ValueKind == JsonValueKind.Object)
        {
            current.TryGetProperty("feature", out var feature);
            var builder = new StringBuilder();
            builder.Append(
                $"Current task: {StringOf(feature, "slug")} #{IntOf(artifact, "order"):00} [{StringOf(artifact, "status")}]\n");
            if (reason.Length > 0)
            {
                builder.Append($"\n{reason}\n");
            }

            if (current.TryGetProperty("warnings", out var warnings) && warnings.ValueKind == JsonValueKind.Array)
            {
                var listed = warnings.EnumerateArray()
                    .Select(w => w.GetString())
                    .Where(w => !string.IsNullOrWhiteSpace(w))
                    .ToArray();
                if (listed.Length > 0)
                {
                    builder.Append($"\nWarnings: {string.Join(", ", listed)}\n");
                }
            }

            builder.Append($"\n{StringOf(artifact, "body")}\n");
            return builder.ToString();
        }

        return reason.Length > 0 ? $"{reason}\n" : string.Empty;
    }

    private static string KindOf(JsonElement artifact) =>
        StringOf(artifact, "kind");

    private static string StatusOf(JsonElement element, string property)
    {
        var status = StringOf(element, property);
        return status.Length == 0 ? "none" : status;
    }

    private static string StringOf(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int IntOf(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var parsed)
            ? parsed
            : 0;

    private static async Task<JsonElement> Guarded(Func<Task<JsonElement>> action)
    {
        try
        {
            return await action();
        }
        catch (ApiNotFoundException ex)
        {
            throw new McpProtocolException(ex.Message, McpErrorCode.ResourceNotFound);
        }
        catch (Exception ex) when (ex is ApiUnreachableException or ApiStatusException)
        {
            throw new McpException(ex.Message);
        }
    }
}
