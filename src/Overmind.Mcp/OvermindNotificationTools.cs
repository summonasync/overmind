using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class OvermindNotificationTools
{
    private static readonly HashSet<string> NotificationKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "info", "question", "approval"
    };

    private readonly OvermindApiClient _api;
    private readonly ProjectResolver _projects;

    public OvermindNotificationTools(OvermindApiClient api, ProjectResolver projects)
    {
        _api = api;
        _projects = projects;
    }

    [McpServerTool(Name = "overmind_notify", Destructive = false, Idempotent = false)]
    [Description("Tell the human something: posts a notification to the center the human watches in the Overmind web UI. Kind is info, question or approval; featureSlug and tab turn it into a deep link onto the feature page. Use question or approval when a gate needs the human's call, then pick the answer up with overmind_replies. The response may carry url (set when featureSlug is) — quote the url in chat when asking for an approval or reporting a question so the human can click through.")]
    public async Task<JsonElement> Notify(
        [Description("Notification kind: info, question or approval.")] string kind,
        [Description("Message body shown to the human, max 2000 chars.")] string text,
        [Description("Id of the project the notification targets; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        [Description("Slug of the feature the deep link opens, as shown on the planning board.")] string? featureSlug = null,
        [Description("Feature page tab the deep link opens: spec, plan, tasks or result.")] string? tab = null,
        [Description("Sender name shown next to the notification in the center; defaults to 'agent'.")] string? from = null,
        CancellationToken ct = default)
    {
        var payload = new JsonObject
        {
            ["kind"] = NormalizeKind(kind),
            ["text"] = text
        };

        payload["projectId"] = await _projects.ResolveAsync(projectId, create: true, "for the notification", ct);
        if (featureSlug is not null)
        {
            payload["featureSlug"] = featureSlug;
        }

        if (tab is not null)
        {
            payload["tab"] = tab;
        }

        if (from is not null)
        {
            payload["from"] = from;
        }

        return await Guarded(() => _api.PostAsync("/api/agent/notifications", payload, ct));
    }

    [McpServerTool(Name = "overmind_replies", ReadOnly = true, Idempotent = true)]
    [Description("Poll for the human's answers: notifications you sent via overmind_notify that carry a reply, newest first. Poll it after asking a question or requesting approval — each item's replyText is the human's decision. Optional combinable filters cut the backlog: since (ISO-8601 timestamp — only replies newer than it, e.g. the artifact's last update when hunting a validation proof; the zone offset is optional, offset-less values are read as UTC the way artifact timestamps are emitted), kind (info/question/approval), featureSlug. An over-narrow filter returns an empty array, not an error. Project resolution never creates a project row; an unregistered directory returns guidance instead.")]
    public async Task<JsonElement> Replies(
        [Description("Id of the project whose replies to read; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        [Description("A reply marks its notification read, so false matches nothing — poll with the default true.")] bool? includeRead = null,
        [Description("ISO-8601 timestamp; only notifications the human replied to after it, e.g. the artifact's last update before you requested a proof. The zone offset is optional: an offset-less value is read as UTC, matching how artifact timestamps are emitted.")] string? since = null,
        [Description("Notification kind: info, question or approval.")] string? kind = null,
        [Description("Feature slug to narrow to, as shown on the planning board.")] string? featureSlug = null,
        CancellationToken ct = default)
    {
        var kindFilter = kind is null ? null : NormalizeKind(kind);
        var sinceFilter = string.IsNullOrWhiteSpace(since) ? null : NormalizeSince(since);
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the notification", ct);
        var query = new List<string> { $"projectId={resolved}" };
        if (includeRead is { } read)
        {
            query.Add($"includeRead={(read ? "true" : "false")}");
        }

        if (sinceFilter is { } from)
        {
            query.Add($"since={Uri.EscapeDataString(from)}");
        }

        if (kindFilter is { } narrowed)
        {
            query.Add($"kind={narrowed}");
        }

        if (!string.IsNullOrWhiteSpace(featureSlug))
        {
            query.Add($"featureSlug={featureSlug.Trim()}");
        }

        return await Guarded(() => _api.GetAsync($"/api/agent/notifications/replies?{string.Join("&", query)}", ct));
    }

    private static async Task<JsonElement> Guarded(Func<Task<JsonElement>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is ApiStatusException or ApiNotFoundException or ApiUnreachableException)
        {
            throw new McpException(ex.Message);
        }
    }

    private static string NormalizeKind(string kind)
    {
        var trimmed = kind.Trim();
        if (!NotificationKinds.Contains(trimmed))
        {
            throw new McpException($"Invalid notification kind '{kind}'. Expected one of: info, question, approval.");
        }

        return trimmed.ToLowerInvariant();
    }

    private static string NormalizeSince(string since)
    {
        var trimmed = since.Trim();
        if (!DateTime.TryParse(trimmed, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            throw new McpException(
                $"Invalid since timestamp '{since}'. Expected an ISO-8601 timestamp, e.g. 2026-10-23T13:26:17Z or the artifact's updatedAt as emitted.");
        }

        return parsed.ToString("O", CultureInfo.InvariantCulture);
    }
}
