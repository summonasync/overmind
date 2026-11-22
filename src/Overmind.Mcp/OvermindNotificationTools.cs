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
    [Description("Tell the human something: posts a notification to the center watched in the Overmind " +
        "web UI. Use kind question or approval when a gate needs the human's call, then pick the answer up " +
        "with overmind_replies; featureSlug and tab make it a deep link.")]
    public async Task<JsonElement> Notify(
        [Description("Notification kind: info, question or approval.")] string kind,
        [Description("Message body shown to the human, soft cap 2000 chars — longer text lands truncated with warnings.")] string text,
        [Description("Optional project id.")] Guid? projectId = null,
        [Description("Feature slug for the deep link.")] string? featureSlug = null,
        [Description("Deep-link tab: spec, plan, tasks or result.")] string? tab = null,
        [Description("Sender name in the center; default 'agent'.")] string? from = null,
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

        var response = await Guarded(() => _api.PostAsync("/api/agent/notifications", payload, ct));
        if (NormalizeKind(kind) == "approval" && featureSlug is not null)
        {
            // The proof recipe: one reply validates spec and plan together, and covers never-edited
            // first drafts — telling the agent up front is what stops defensive double-tapping.
            var echo = JsonNode.Parse(response.GetRawText()) as JsonObject;
            if (echo is not null)
            {
                echo["hint"] = "One reply validates the spec AND the plan of this feature — draft both " +
                    "gated artifacts before tapping if you want both pre-seen. A tap also covers " +
                    "never-edited first drafts; any gated edit after the tap voids it.";
                return JsonSerializer.SerializeToElement(echo);
            }
        }

        return response;
    }

    [McpServerTool(Name = "overmind_replies", ReadOnly = true, Idempotent = true)]
    [Description("Polls the human's replies to your overmind_notify messages, newest first — each item's " +
        "replyText is the human's decision. A complete read advances this session's reply cursor, " +
        "retiring those replies from overmind_wake_peek's default window. Filters combine; an " +
        "over-narrow filter returns an empty array, not an error. Rows arrive projected (notification " +
        "text excerpted to 200 chars) unless full=true.")]
    public async Task<JsonElement> Replies(
        [Description("Optional project id.")] Guid? projectId = null,
        [Description("False narrows to replies the human has not read yet; keep the default.")] bool? includeRead = null,
        [Description("Only replies after this ISO-8601 instant (offset-less reads as UTC).")] string? since = null,
        [Description("Notification kind: info, question or approval.")] string? kind = null,
        [Description("Feature slug to narrow to.")] string? featureSlug = null,
        [Description("True for the stored rows untouched, notification text included.")] bool? full = null,
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

        var payload = await Guarded(() => _api.GetAsync(
            $"/api/agent/notifications/replies?{string.Join("&", query)}", ct));

        // Row 8: the whole reply backlog in one line. Measured (NotificationsApi.Replies →
        // NotificationsService.ListRepliesAsync): no `since` means NO lower bound at all, so the
        // 79KB payload is every reply the project ever had, each carrying its stored notification
        // text — the text is what the caller wrote, never what it needs back. The rows are
        // projected by default; the set of replies returned is byte-unchanged, and full=true is
        // the untouched path.
        return full == true ? payload : ReplyProjection.Compact(payload);
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
