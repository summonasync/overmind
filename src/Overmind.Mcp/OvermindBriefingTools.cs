using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class OvermindBriefingTools
{
    private const int DefaultNotesLimit = 10;
    private const int SnippetCap = 200;
    private const int RetiredDays = 14;
    private const int RetiredRowCap = 5;

    private const string ActiveState = "active";
    private const string LiftedState = "lifted";
    private const string SupersededState = "superseded";

    private const string MemoryLayerNote =
        "Memory layer (Grimoire) — durable crew facts live in the local Grimoire store " +
        "(memory_brief / memory_recall on the grimoire MCP server); this section is teaching, not state.";

    private const string BriefingDescription =
        "ONE call returns the start-of-work snapshot of a project — guards and gates with full bodies, decisions " +
        "and the latest notes as snippets, tombstones, and the lifted-recently tail. The read ritual before any " +
        "work; list_entries stays the exploration tool, briefing the snapshot. Read-only.";

    private readonly OvermindApiClient _api;
    private readonly ProjectResolver _projects;

    public OvermindBriefingTools(OvermindApiClient api, ProjectResolver projects)
    {
        _api = api;
        _projects = projects;
    }

    [McpServerTool(Name = "overmind_briefing", ReadOnly = true, Idempotent = true)]
    [Description(BriefingDescription)]
    public async Task<JsonElement> Briefing(
        [Description("Optional project id.")]
        Guid? projectId = null,
        [Description("Count of latest notes; default 10.")]
        int? notesLimit = null,
        [Description("False trims the tombstone section; default true.")]
        bool? includeTombstones = null,
        CancellationToken ct = default)
    {
        var limit = notesLimit ?? DefaultNotesLimit;
        if (limit < 0)
        {
            throw new McpException("notesLimit must be zero or a positive count of notes.");
        }

        var resolvedProjectId = await _projects.ResolveAsync(projectId, create: false, "for the briefing", ct);

        var guards = await Guarded(() => FetchEntriesAsync(resolvedProjectId, "guard", ActiveState, ct));
        var gates = await Guarded(() => FetchEntriesAsync(resolvedProjectId, "gate", ActiveState, ct));
        var decisions = await Guarded(() => FetchEntriesAsync(resolvedProjectId, "decision", ActiveState, ct));
        var notes = await Guarded(() => FetchEntriesAsync(resolvedProjectId, "note", null, ct));
        var lanes = await Guarded(() => FetchLanesAsync(resolvedProjectId, ct));
        var lifecycle = await Guarded(() => FetchAllEntriesAsync(resolvedProjectId, ct));

        var briefing = new JsonObject
        {
            ["projectId"] = resolvedProjectId.ToString(),
            ["guards"] = GuardRows(guards, decisions),
            ["gates"] = FullRows(gates),
            ["decisions"] = SnippetRows(decisions, int.MaxValue),
            ["notes"] = SnippetRows(notes, limit),
            ["liftedRecently"] = LiftedRows(lifecycle, DateTimeOffset.UtcNow),
            ["memoryLayer"] = new JsonObject
            {
                ["note"] = MemoryLayerNote,
                ["boardToMemory"] =
                    "entries and artifacts carry 'pointer: grimoire://memory/<id>' lines toward the memory fact",
                ["memoryToBoard"] =
                    "grimoire memory records carry an 'overmind://entry/<uuid>' tag back to the entry"
            }
        };

        if (includeTombstones ?? true)
        {
            var tombstones = await Guarded(() => FetchEntriesAsync(resolvedProjectId, "tombstone", null, ct));
            briefing["tombstones"] = GraveRows(tombstones);
        }

        var liveSurfaces = LiveSurfaceRows(lanes);
        if (liveSurfaces is { Count: > 0 })
        {
            briefing["liveSurfaces"] = liveSurfaces;
        }

        return briefing.Deserialize<JsonElement>();
    }

    private async Task<JsonElement> FetchEntriesAsync(Guid projectId, string type, string? state, CancellationToken ct)
    {
        var path = $"/api/projects/{projectId}/entries?type={type}";
        if (state is not null)
        {
            path += $"&state={state}";
        }

        return await _api.GetAsync(path, ct);
    }

    private async Task<JsonElement> FetchAllEntriesAsync(Guid projectId, CancellationToken ct) =>
        await _api.GetAsync($"/api/projects/{projectId}/entries", ct);

    private async Task<JsonElement> FetchLanesAsync(Guid projectId, CancellationToken ct) =>
        await _api.GetAsync($"/api/agent/projects/{projectId}/planning/lanes", ct);

    private static JsonArray? LiveSurfaceRows(JsonElement roster)
    {
        if (roster.ValueKind != JsonValueKind.Object || !roster.TryGetProperty("lanes", out var lanes))
        {
            return null;
        }

        var rows = new JsonArray();
        foreach (var lane in lanes.EnumerateArray())
        {
            if (lane.ValueKind != JsonValueKind.Object ||
                !lane.TryGetProperty("liveSurfaces", out var surfaces) ||
                surfaces.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var laneName = lane.TryGetProperty("name", out var name) ? name.GetString() : null;
            foreach (var surface in surfaces.EnumerateArray())
            {
                rows.Add(new JsonObject
                {
                    ["lane"] = laneName,
                    ["glob"] = surface.TryGetProperty("glob", out var glob) ? glob.GetString() : null,
                    ["owner"] = surface.TryGetProperty("owner", out var owner) ? owner.GetString() : null
                });
            }
        }

        return rows;
    }

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

    private static JsonArray GuardRows(JsonElement guards, JsonElement decisions)
    {
        var rows = FullRows(guards);
        foreach (var decision in decisions.EnumerateArray())
        {
            if (!decision.TryGetProperty("enforces", out var enforces) || enforces.ValueKind != JsonValueKind.True)
            {
                continue;
            }

            var row = FullRow(decision);
            row["kind"] = "decision·enforces";
            rows.Add(row);
        }

        return rows;
    }

    private static JsonArray FullRows(JsonElement entries)
    {
        var rows = new JsonArray();
        foreach (var entry in entries.EnumerateArray())
        {
            rows.Add(FullRow(entry));
        }

        return rows;
    }

    private static JsonObject FullRow(JsonElement entry) =>
        new()
        {
            ["id"] = entry.GetProperty("id").GetString(),
            ["title"] = entry.GetProperty("title").GetString(),
            ["content"] = entry.GetProperty("content").GetString(),
            ["updatedAt"] = entry.GetProperty("updatedAt").GetString()
        };

    private static JsonArray SnippetRows(JsonElement entries, int limit)
    {
        var rows = new JsonArray();
        foreach (var entry in entries.EnumerateArray().Take(limit))
        {
            rows.Add(new JsonObject
            {
                ["id"] = entry.GetProperty("id").GetString(),
                ["title"] = entry.GetProperty("title").GetString(),
                ["snippet"] = BriefingSnippet.Clip(entry.GetProperty("content").GetString() ?? string.Empty, SnippetCap),
                ["updatedAt"] = entry.GetProperty("updatedAt").GetString()
            });
        }

        return rows;
    }

    private static JsonArray GraveRows(JsonElement entries)
    {
        var rows = new JsonArray();
        foreach (var entry in entries.EnumerateArray())
        {
            rows.Add(new JsonObject
            {
                ["id"] = entry.GetProperty("id").GetString(),
                ["title"] = entry.GetProperty("title").GetString(),
                ["type"] = entry.GetProperty("type").GetString(),
                ["deletedAt"] = entry.GetProperty("createdAt").GetString()
            });
        }

        return rows;
    }

    private static JsonArray LiftedRows(JsonElement entries, DateTimeOffset now)
    {
        var titles = Titles(entries);
        var retired = entries.EnumerateArray()
            .Select(entry => (Entry: entry, At: StateAt(entry)))
            .Where(row => IsRetired(row.Entry) && row.At is { } at && now - at <= TimeSpan.FromDays(RetiredDays))
            .Select(row => (row.Entry, At: row.At!.Value))
            .OrderByDescending(row => row.At)
            .Take(RetiredRowCap)
            .ToArray();

        var rows = new JsonArray();
        foreach (var (entry, _) in retired)
        {
            rows.Add(RetiredRow(entry, titles));
        }

        return rows;
    }

    private static bool IsRetired(JsonElement entry) =>
        entry.TryGetProperty("state", out var state) &&
        state.ValueKind == JsonValueKind.String &&
        state.GetString() is LiftedState or SupersededState;

    private static DateTimeOffset? StateAt(JsonElement entry)
    {
        if (!entry.TryGetProperty("stateAt", out var stateAt) || stateAt.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            stateAt.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var at)
            ? at
            : null;
    }

    private static Dictionary<string, string> Titles(JsonElement entries)
    {
        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries.EnumerateArray())
        {
            var id = entry.GetProperty("id").GetString();
            if (id is not null && !titles.ContainsKey(id))
            {
                titles[id] = entry.GetProperty("title").GetString() ?? string.Empty;
            }
        }

        return titles;
    }

    private static JsonObject RetiredRow(JsonElement entry, Dictionary<string, string> titles)
    {
        var reason = entry.TryGetProperty("stateReason", out var stateReason) &&
            stateReason.ValueKind == JsonValueKind.String
                ? stateReason.GetString() ?? string.Empty
                : string.Empty;

        var row = new JsonObject
        {
            ["id"] = entry.GetProperty("id").GetString(),
            ["title"] = entry.GetProperty("title").GetString(),
            ["state"] = entry.GetProperty("state").GetString(),
            ["stateAt"] = entry.GetProperty("stateAt").GetString(),
            ["reason"] = BriefingSnippet.Clip(reason, SnippetCap)
        };

        if (entry.TryGetProperty("supersededById", out var successorId) &&
            successorId.ValueKind == JsonValueKind.String &&
            successorId.GetString() is { } successor &&
            titles.TryGetValue(successor, out var successorTitle))
        {
            row["successor"] = successorTitle;
        }

        return row;
    }
}
