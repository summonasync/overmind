using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class WaveChatTools
{
    private const int CompactTextLength = 200;

    private readonly OvermindApiClient _api;
    private readonly MutationGuard _mutations;
    private readonly ProjectResolver _projects;

    public WaveChatTools(OvermindApiClient api, MutationGuard mutations, ProjectResolver projects)
    {
        _api = api;
        _mutations = mutations;
        _projects = projects;
    }

    [McpServerTool(Name = "wave_chat_post", Destructive = false, Idempotent = false)]
    [Description("Posts one signal to a wave chat room — a lane room by roster name or the shared 'all' room. " +
        "Signal-only by etiquette: blocker, critical spot, handoff or broadcast; an @alias mention means the " +
        "addressee must read this; no chatter. Text is trimmed, capped at 500 characters, and the author is the " +
        "calling session's registered alias. An unknown room fails with the named MissingWaveRoom error, " +
        "overlong text with WaveTextTooLong.")]
    public async Task<CallToolResult> PostMessage(
        [Description("Room to post into: a lane name from the project roster or the shared 'all' room.")] string room,
        [Description("Signal text, max 500 characters; @alias tokens inside it become mentions.")] string text,
        [Description("Id of the project the room belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var message = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/wave/rooms/{room}/messages",
            new { text },
            ct), readback: null, ct);

        var id = message.GetProperty("id").GetString();
        var postedRoom = message.GetProperty("room").GetString();
        var authorAlias = message.GetProperty("authorAlias").GetString();
        var mentions = message.GetProperty("mentions").EnumerateArray()
            .Select(m => m.GetString())
            .ToArray();
        var mentionNote = mentions.Length > 0 ? $" Mentions: {string.Join(", ", mentions)}." : string.Empty;

        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = $"Message {id} posted to '{postedRoom}' as {authorAlias}.{mentionNote}"
                }
            ]
        };
    }

    [McpServerTool(Name = "wave_chat_unread", Destructive = false, Idempotent = false)]
    [Description("Fetches the calling alias's unread wave chat feed and advances its watermarks: each room read " +
        "delivers messages newer than the alias's watermark, and a message that mentions the alias is delivered " +
        "once by that same read — the mention clause is scoped by the watermark, so nothing re-delivers on later " +
        "reads. Without rooms the tool reads every room that has activity, oldest last-activity first, so one " +
        "room's watermark cannot blind a quieter room; rooms is a comma-separated filter (lane names and/or 'all'). " +
        "The merged feed is deduplicated by message id and ordered by createdAt. since (ISO datetime) narrows the " +
        "effective lower bound to max(watermark, since) and never widens delivery; limit caps the page to the " +
        "NEWEST window — replayMentions=true re-delivers the mention slice (every message that ever mentioned the " +
        "alias) without the watermark bound, not older traffic in general; compact projects each message to " +
        "{id, room, authorAlias, createdAt, text} " +
        "with text truncated to ~200 characters. An unknown filtered room fails with the named MissingWaveRoom error.")]
    public async Task<JsonElement> FetchUnread(
        [Description("Optional comma-separated room filter; omitted reads every room with activity, oldest activity first.")] string? rooms = null,
        [Description("Optional ISO datetime; the effective lower bound becomes max(watermark, since), so delivery only narrows.")] DateTime? since = null,
        [Description("Optional page cap delivering the NEWEST window; the mention slice outside it needs replayMentions=true.")] int? limit = null,
        [Description("Project each message to {id, room, authorAlias, createdAt, text<=200 chars} to save tokens; default false keeps the full payload.")] bool compact = false,
        [Description("Opt out of mention scoping: re-deliver every message that ever mentioned the alias, legacy behavior; default false.")] bool replayMentions = false,
        [Description("Id of the project whose rooms to read; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var selected = ParseRooms(rooms) ?? await RoomsAsync(resolved, ct);

        var merged = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var watermark = DateTime.MinValue;
        foreach (var room in selected)
        {
            var page = await ReadAsync(
                $"/api/agent/projects/{resolved}/wave/rooms/{room}/unread{UnreadQuery(since, limit, replayMentions)}", ct);
            foreach (var item in page.GetProperty("items").EnumerateArray())
            {
                var node = JsonNode.Parse(item.GetRawText())!.AsObject();
                merged.TryAdd(node["id"]!.GetValue<string>(), node);
            }

            var at = page.GetProperty("watermarkAt").GetDateTime();
            if (at > watermark)
            {
                watermark = at;
            }
        }

        var messages = new JsonArray();
        foreach (var entry in merged.Values
            .Select(m => (Message: m, At: m["createdAt"]!.GetValue<DateTime>(), Id: m["id"]!.GetValue<string>()))
            .OrderBy(e => e.At)
            .ThenBy(e => e.Id))
        {
            messages.Add(compact ? CompactMessage(entry.Message) : entry.Message);
        }

        return new JsonObject
        {
            ["projectId"] = resolved,
            ["rooms"] = RoomsEcho(selected),
            ["messages"] = messages,
            ["watermarkAt"] = watermark == DateTime.MinValue ? null : watermark
        }.Deserialize<JsonElement>();
    }

    [McpServerTool(Name = "wave_chat_peek", Destructive = false, ReadOnly = true, Idempotent = true)]
    [Description("Counts the calling alias's undelivered wave chat traffic per room without advancing any watermark: " +
        "one {room, count, ids} entry per room, where count is the number of undelivered messages and ids lists " +
        "their message ids — read the full messages with wave_chat_unread only when count > 0. Same selection as " +
        "an unread read: room traffic above the watermark plus watermark-scoped mentions. Mentions (@alias) surface " +
        "in the {room, count, ids} entry of every room they were posted to: ids can repeat across rooms, and summing " +
        "the counts overstates what a single wave_chat_unread read delivers. rooms is a comma-separated " +
        "filter (lane names and/or 'all'); omitted reads every room with activity, oldest last-activity first. " +
        "An unknown filtered room fails with the named MissingWaveRoom error.")]
    public async Task<JsonElement> PeekRooms(
        [Description("Optional comma-separated room filter; omitted reads every room with activity, oldest activity first.")] string? rooms = null,
        [Description("Id of the project whose rooms to read; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var selected = ParseRooms(rooms) ?? await RoomsAsync(resolved, ct);

        var peeks = new JsonArray();
        foreach (var room in selected)
        {
            var peek = await ReadAsync($"/api/agent/projects/{resolved}/wave/rooms/{room}/peek", ct);
            peeks.Add(JsonNode.Parse(peek.GetRawText())!.AsObject());
        }

        return new JsonObject
        {
            ["projectId"] = resolved,
            ["rooms"] = RoomsEcho(selected),
            ["peeks"] = peeks
        }.Deserialize<JsonElement>();
    }

    private static string UnreadQuery(DateTime? since, int? limit, bool replayMentions)
    {
        var query = new List<string>();
        if (since is { } from)
        {
            query.Add($"since={Uri.EscapeDataString(from.ToString("O"))}");
        }

        if (limit is { } page)
        {
            query.Add($"limit={page}");
        }

        if (replayMentions)
        {
            query.Add("replayMentions=true");
        }

        return query.Count == 0 ? string.Empty : $"?{string.Join("&", query)}";
    }

    private static JsonObject CompactMessage(JsonObject message) => new()
    {
        ["id"] = message["id"]!.GetValue<string>(),
        ["room"] = message["room"]!.GetValue<string>(),
        ["authorAlias"] = message["authorAlias"]!.GetValue<string>(),
        ["createdAt"] = message["createdAt"]!.GetValue<DateTime>(),
        ["text"] = Truncate(message["text"]!.GetValue<string>())
    };

    private static string Truncate(string text) =>
        text.Length <= CompactTextLength ? text : $"{text[..CompactTextLength]}…";

    private static JsonArray RoomsEcho(List<string> rooms)
    {
        var echo = new JsonArray();
        foreach (var room in rooms)
        {
            echo.Add(room);
        }

        return echo;
    }

    private static List<string>? ParseRooms(string? rooms)
    {
        if (rooms is null)
        {
            return null;
        }

        var parsed = rooms
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (parsed.Count == 0)
        {
            throw new McpException(
                "The rooms filter carries no room names — pass comma-separated lane names or 'all', " +
                "or omit it to read every room with activity.");
        }

        return parsed;
    }

    private async Task<List<string>> RoomsAsync(Guid projectId, CancellationToken ct)
    {
        var listing = await ReadAsync($"/api/agent/projects/{projectId}/wave/rooms", ct);
        var rooms = new List<(string Room, DateTime Activity)>();
        foreach (var item in listing.GetProperty("items").EnumerateArray())
        {
            var at = item.GetProperty("lastActivityAt");
            if (at.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            rooms.Add((item.GetProperty("room").GetString()!, at.GetDateTime()));
        }

        return [.. rooms.OrderBy(r => r.Activity).Select(r => r.Room)];
    }

    private async Task<JsonElement> ReadAsync(string url, CancellationToken ct)
    {
        try
        {
            return await _api.GetAsync(url, ct);
        }
        catch (ApiStatusException ex)
        {
            throw new McpException(MutationGuard.StopDetailOf(ex) ?? ex.Message);
        }
        catch (Exception ex) when (ex is ApiNotFoundException or ApiUnreachableException)
        {
            throw new McpException(ex.Message);
        }
    }
}
