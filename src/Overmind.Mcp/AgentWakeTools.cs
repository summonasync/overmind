using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class AgentWakeTools(OvermindApiClient api)
{
    [McpServerTool(Name = "overmind_wake_peek", Destructive = false, ReadOnly = true, Idempotent = true)]
    [Description("Wake ritual for the calling session: a cheap, side-effect-free check of pending signals — " +
        "human replies to this session's notifications (kind 'notification-reply', from 'human') and wave chat " +
        "messages mentioning this session's alias (kind 'wave-mention', from the author alias, roomId the wave " +
        "room). Advances no watermarks and marks nothing read, so it is safe to poll any time; consume the " +
        "mention traffic with wave_chat_unread. Returns {signals, cursor}: each signal is " +
        "{kind, refId, roomId, from, createdAt, excerpt}, ordered by createdAt asc, capped at 100 per page, " +
        "excerpt capped at 200 chars. since is optional: omit it for the endpoint defaults — replies with " +
        "RepliedAt within the last 7 days, mentions created after the session's LastSeenAt as it stood before " +
        "this request — or pass the cursor echoed by the previous call. Paging: when a page returns 100 signals, " +
        "feed the returned cursor back in as since, verbatim (the 'ISO|refId' composite; an empty page carries a " +
        "bare ISO cursor), and repeat until an under-cap page arrives. Session-scoped, no projectId: the adapter " +
        "stamps its registered session automatically — a headerless or unknown session fails with a named error.")]
    public async Task<JsonElement> Peek(
        [Description("Optional lower bound: an ISO-8601 timestamp, or the cursor string echoed by the previous " +
            "call ('ISO|refId'), passed through verbatim; omit for the endpoint defaults (replies: last 7 days; " +
            "mentions: after the pre-request LastSeenAt).")] string? since = null,
        CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(since)
            ? "/api/agent/wake"
            : $"/api/agent/wake?since={Uri.EscapeDataString(since.Trim())}";
        try
        {
            return await api.GetAsync(url, ct);
        }
        catch (Exception ex) when (ex is ApiStatusException or ApiNotFoundException or ApiUnreachableException)
        {
            throw new McpException(ex.Message);
        }
    }
}
