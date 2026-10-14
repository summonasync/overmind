using System.ComponentModel;
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

    private const string BriefingDescription =
        "ONE call returns the start-of-work snapshot of a project — the guardrail picture an agent absorbs before any work: " +
        "guards and gates with full bodies, decisions and the latest notes as ~200-character snippets (read_entry for depth), " +
        "and the tombstone list of buried entries. " +
        "One consolidated tool by design: four specialized getguards/getgates/getnotes/gettombstones lookups would multiply " +
        "the discovery surface for a single moment-in-time need; list_entries stays the exploration tool, this one is the start ritual. " +
        "notesLimit defaults to 10; includeTombstones=false trims the graves section; categories without entries come back as " +
        "empty arrays, never omitted keys; entries arrive most-recently-updated first. " +
        "Read-only: projectId is resolved from OVERMIND_PROJECT_ID or the working directory when omitted.";

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
        [Description("Id of the project to brief on; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")]
        Guid? projectId = null,
        [Description("How many of the latest notes to include; default 10.")]
        int? notesLimit = null,
        [Description("Set false to trim the tombstone section from the payload; default true.")]
        bool? includeTombstones = null,
        CancellationToken ct = default)
    {
        var limit = notesLimit ?? DefaultNotesLimit;
        if (limit < 0)
        {
            throw new McpException("notesLimit must be zero or a positive count of notes.");
        }

        var resolvedProjectId = await _projects.ResolveAsync(projectId, create: false, "for the briefing", ct);

        var guards = await Guarded(() => FetchEntriesAsync(resolvedProjectId, "guard", ct));
        var gates = await Guarded(() => FetchEntriesAsync(resolvedProjectId, "gate", ct));
        var decisions = await Guarded(() => FetchEntriesAsync(resolvedProjectId, "decision", ct));
        var notes = await Guarded(() => FetchEntriesAsync(resolvedProjectId, "note", ct));

        var briefing = new JsonObject
        {
            ["projectId"] = resolvedProjectId.ToString(),
            ["guards"] = FullRows(guards),
            ["gates"] = FullRows(gates),
            ["decisions"] = SnippetRows(decisions, int.MaxValue),
            ["notes"] = SnippetRows(notes, limit)
        };

        if (includeTombstones ?? true)
        {
            var tombstones = await Guarded(() => FetchEntriesAsync(resolvedProjectId, "tombstone", ct));
            briefing["tombstones"] = GraveRows(tombstones);
        }

        return briefing.Deserialize<JsonElement>();
    }

    private async Task<JsonElement> FetchEntriesAsync(Guid projectId, string type, CancellationToken ct) =>
        await _api.GetAsync($"/api/projects/{projectId}/entries?type={type}", ct);

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

    private static JsonArray FullRows(JsonElement entries)
    {
        var rows = new JsonArray();
        foreach (var entry in entries.EnumerateArray())
        {
            rows.Add(new JsonObject
            {
                ["id"] = entry.GetProperty("id").GetString(),
                ["title"] = entry.GetProperty("title").GetString(),
                ["content"] = entry.GetProperty("content").GetString(),
                ["updatedAt"] = entry.GetProperty("updatedAt").GetString()
            });
        }

        return rows;
    }

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
}
