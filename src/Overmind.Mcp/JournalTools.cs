using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class JournalTools
{
    private const string CaptureDescription =
        "Captures the owner's journal thought through the human door POST /api/entries. projectId omitted " +
        "files it as a journal entry in the reserved Journal container (the first capture births it); a guid " +
        "files it into that project. Title caps at 500 chars. Loopback-only host, no auth.";

    private const string SearchDescription =
        "Searches entries through GET /api/search. mode=fulltext (default) or semantic; semantic answers 503 " +
        "with a fix hint when the vector store is down. limit clamps to 1..100 (default 20). " +
        "Loopback-only host, no auth.";

    private const string ReadDescription =
        "Reads one entry through GET /api/entries/{id} and returns content plus the link rails: outgoing, " +
        "incoming and unresolved [[wiki]] markers. Loopback-only host, no auth.";

    private const string ListDescription =
        "Lists a project's entries through GET /api/projects/{id}/entries. Filters: tag, project " +
        "(linked-board name), day (YYYY-MM-DD), limit (caps returned rows; the stream is newest-first). " +
        "projectId omitted resolves the reserved Journal container. Loopback-only host, no auth.";

    private const string AppendDescription =
        "Appends a dated follow-up through the human door POST /api/entries/{id}/append. text soft-caps at " +
        "2000 chars: within the grace band it stores whole with a warnings[] note, past it the text truncates " +
        "and the response previews the lost tail. Loopback-only host, no auth.";

    private static readonly JsonElement EmptyHits = JsonSerializer.Deserialize<JsonElement>("[]");

    private readonly OvermindApiClient _api;
    private readonly JournalProjectResolver _journal;

    public JournalTools(OvermindApiClient api, JournalProjectResolver journal)
    {
        _api = api;
        _journal = journal;
    }

    [McpServerTool(Name = "journal_capture", Destructive = false, Idempotent = false)]
    [Description(CaptureDescription)]
    public async Task<JsonElement> Capture(
        [Description("Entry title.")] string title,
        [Description("Markdown body.")] string content,
        [Description("Optional project id; omitted files into the reserved Journal container.")] Guid? projectId = null,
        [Description("Optional tags.")] string[]? tags = null,
        CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["type"] = "journal",
            ["title"] = title,
            ["content"] = content,
        };

        if (projectId is { } target)
        {
            body["projectId"] = target.ToString("D");
        }

        if (tags is { Length: > 0 })
        {
            var tagNodes = new JsonArray();
            foreach (var tag in tags)
            {
                tagNodes.Add(tag);
            }

            body["tags"] = tagNodes;
        }

        var created = await Guarded(() => _api.PostAsync("/api/entries", body, ct));
        return CaptureEcho(created);
    }

    [McpServerTool(Name = "journal_search", ReadOnly = true)]
    [Description(SearchDescription)]
    public async Task<JsonElement> Search(
        [Description("Search text.")] string query,
        [Description("Search mode: fulltext (default) or semantic.")] string? mode = null,
        [Description("Max hits, clamped 1..100 by the door.")] int? limit = null,
        [Description("Optional project id to restrict hits.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var effectiveMode = string.IsNullOrWhiteSpace(mode) ? "fulltext" : mode.Trim();
        var parameters = new List<string>
        {
            $"q={Uri.EscapeDataString(query)}",
            $"mode={Uri.EscapeDataString(effectiveMode)}",
        };

        if (limit is { } capped)
        {
            parameters.Add($"limit={capped}");
        }

        if (projectId is { } scope)
        {
            parameters.Add($"projectId={scope}");
        }

        var payload = await Guarded(() => _api.GetAsync("/api/search?" + string.Join('&', parameters), ct));
        return payload.TryGetProperty("hits", out var hits) && hits.ValueKind == JsonValueKind.Array
            ? hits
            : EmptyHits;
    }

    [McpServerTool(Name = "journal_read", ReadOnly = true, Idempotent = true)]
    [Description(ReadDescription)]
    public async Task<JsonElement> Read(
        [Description("Entry id.")] Guid entryId,
        CancellationToken ct = default)
    {
        var entry = await Guarded(() => _api.GetAsync($"/api/entries/{entryId}", ct));

        var view = new JsonObject
        {
            ["id"] = entry.GetProperty("id").GetString(),
            ["title"] = entry.GetProperty("title").GetString(),
            ["type"] = entry.GetProperty("type").GetString(),
            ["updatedAt"] = entry.GetProperty("updatedAt").GetString(),
            ["tags"] = Section(entry, "tags"),
            ["content"] = entry.GetProperty("content").GetString(),
            ["outgoing"] = Section(entry, "outgoing"),
            ["incoming"] = Section(entry, "incoming"),
            ["unresolved"] = Section(entry, "unresolved"),
        };

        return view.Deserialize<JsonElement>();
    }

    [McpServerTool(Name = "journal_list", ReadOnly = true, Idempotent = true)]
    [Description(ListDescription)]
    public async Task<JsonElement> List(
        [Description("Optional project id; omitted resolves the reserved Journal container.")] Guid? projectId = null,
        [Description("Optional tag filter.")] string? tag = null,
        [Description("Optional linked-project name filter.")] string? project = null,
        [Description("Optional day filter YYYY-MM-DD (that day, UTC).")] string? day = null,
        [Description("Caps the returned rows; the stream is newest-first.")] int? limit = null,
        CancellationToken ct = default)
    {
        var container = await _journal.ResolveAsync(projectId, ct);

        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(tag))
        {
            filters.Add($"tag={Uri.EscapeDataString(tag.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(project))
        {
            filters.Add($"project={Uri.EscapeDataString(project.Trim())}");
        }

        if (day is { } only)
        {
            var (since, until) = DayBounds(only);
            filters.Add($"since={Uri.EscapeDataString(since)}");
            filters.Add($"until={Uri.EscapeDataString(until)}");
        }

        var path = $"/api/projects/{container}/entries";
        if (filters.Count > 0)
        {
            path += "?" + string.Join('&', filters);
        }

        var payload = await Guarded(() => _api.GetAsync(path, ct));
        if (limit is not { } rows || payload.ValueKind != JsonValueKind.Array || payload.GetArrayLength() <= rows)
        {
            return payload;
        }

        var capped = new JsonArray();
        foreach (var row in payload.EnumerateArray().Take(rows))
        {
            capped.Add(JsonNode.Parse(row.GetRawText()));
        }

        return capped.Deserialize<JsonElement>();
    }

    [McpServerTool(Name = "journal_append", Destructive = false, Idempotent = false)]
    [Description(AppendDescription)]
    public async Task<JsonElement> Append(
        [Description("Entry id.")] Guid entryId,
        [Description("Follow-up text, soft cap 2000 chars.")] string text,
        CancellationToken ct = default)
    {
        var appended = await Guarded(() => _api.PostAsync($"/api/entries/{entryId}/append", new { text }, ct));
        return AppendEcho(appended);
    }

    internal static (string Since, string Until) DayBounds(string day)
    {
        if (!DateOnly.TryParseExact(
                day.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new McpException($"day must be YYYY-MM-DD, got '{day}'.");
        }

        return ($"{parsed:yyyy-MM-dd}T00:00:00Z", $"{parsed.AddDays(1):yyyy-MM-dd}T00:00:00Z");
    }

    private JsonElement CaptureEcho(JsonElement entry)
    {
        var echo = new JsonObject
        {
            ["id"] = entry.GetProperty("id").GetString(),
            ["projectId"] = entry.GetProperty("projectId").GetString(),
            ["title"] = entry.GetProperty("title").GetString(),
            ["updatedAt"] = entry.GetProperty("updatedAt").GetString(),
            ["bodyChars"] = entry.GetProperty("content").GetString()?.Length ?? 0,
        };

        CopyWarnings(entry, echo);
        return echo.Deserialize<JsonElement>();
    }

    private JsonElement AppendEcho(JsonElement entry)
    {
        var echo = new JsonObject
        {
            ["id"] = entry.GetProperty("id").GetString(),
            ["title"] = entry.GetProperty("title").GetString(),
            ["version"] = entry.GetProperty("currentVersion").GetInt32(),
            ["updatedAt"] = entry.GetProperty("updatedAt").GetString(),
            ["bodyChars"] = entry.GetProperty("content").GetString()?.Length ?? 0,
        };

        CopyWarnings(entry, echo);
        return echo.Deserialize<JsonElement>();
    }

    private static void CopyWarnings(JsonElement entry, JsonObject echo)
    {
        if (entry.TryGetProperty("warnings", out var warnings) &&
            warnings.ValueKind == JsonValueKind.Array &&
            warnings.GetArrayLength() > 0)
        {
            echo["warnings"] = JsonNode.Parse(warnings.GetRawText());
        }
    }

    private static JsonNode? Section(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) &&
        value.ValueKind is JsonValueKind.Array or JsonValueKind.Object
            ? JsonNode.Parse(value.GetRawText())
            : null;

    private static async Task<JsonElement> Guarded(Func<Task<JsonElement>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is ApiNotFoundException or SemanticSearchUnavailableException or ApiUnreachableException or ApiStatusException)
        {
            throw new McpException(ex.Message);
        }
    }
}
