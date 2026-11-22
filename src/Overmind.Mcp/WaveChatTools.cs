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
        "Signal-only by etiquette: blocker, critical spot, handoff or broadcast; no chatter. An @alias mention " +
        "means the addressee must read this; the author is the calling session's alias.")]
    public async Task<CallToolResult> PostMessage(
        [Description("Lane room name or the shared 'all' room.")] string room,
        [Description("Signal text, soft cap 500 chars — longer text lands truncated with warnings; @alias tokens become mentions.")] string text,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var message = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/wave/rooms/{Uri.EscapeDataString(room)}/messages",
            new { text },
            ct), readback: null, ct);

        var id = message.GetProperty("id").GetString();
        var postedRoom = message.GetProperty("room").GetString();
        var authorAlias = message.GetProperty("authorAlias").GetString();
        var mentions = message.GetProperty("mentions").EnumerateArray()
            .Select(m => m.GetString())
            .ToArray();
        var mentionNote = mentions.Length > 0 ? $" Mentions: {string.Join(", ", mentions)}." : string.Empty;
        var warningNote = message.TryGetProperty("warnings", out var warnings) &&
            warnings.ValueKind == JsonValueKind.Array && warnings.GetArrayLength() > 0
            ? $"\nWarnings: {warnings.GetRawText()}"
            : string.Empty;

        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = $"Message {id} posted to '{postedRoom}' as {authorAlias}.{mentionNote}{warningNote}"
                }
            ]
        };
    }

    [McpServerTool(Name = "wave_chat_unread", Destructive = false, Idempotent = false)]
    [Description("Fetches the calling alias's unread wave chat feed and advances its watermarks — the " +
        "consuming door; for counts without advancing use wave_chat_peek, for mentions plus notification " +
        "replies use overmind_wake_peek. Delivered once, merged oldest first.")]
    public async Task<JsonElement> FetchUnread(
        [Description("Comma-separated room filter; omitted reads every active room.")] string? rooms = null,
        [Description("ISO datetime; only narrows the watermark lower bound.")] DateTime? since = null,
        [Description("Optional page cap on the newest window.")] int? limit = null,
        [Description("Project each message to id, room, authorAlias, createdAt, text≤200.")] bool compact = false,
        [Description("Re-deliver every message that ever mentioned the alias.")] bool replayMentions = false,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var selected = ParseRooms(rooms) ?? await RoomsAsync(resolved, ct);

        var merged = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var watermark = DateTime.MinValue;
        foreach (var room in selected)
        {
            var page = await ReadAsync(
                $"/api/agent/projects/{resolved}/wave/rooms/{Uri.EscapeDataString(room)}/unread{UnreadQuery(since, limit, replayMentions)}", ct);
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
    [Description("Counts the calling alias's undelivered wave chat traffic per room without advancing any " +
        "watermark — {room, count, ids} rows; read the bodies with wave_chat_unread when count > 0.")]
    public async Task<JsonElement> PeekRooms(
        [Description("Comma-separated room filter; omitted reads every active room.")] string? rooms = null,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var selected = ParseRooms(rooms) ?? await RoomsAsync(resolved, ct);

        var peeks = new JsonArray();
        foreach (var room in selected)
        {
            var peek = await ReadAsync($"/api/agent/projects/{resolved}/wave/rooms/{Uri.EscapeDataString(room)}/peek", ct);
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
