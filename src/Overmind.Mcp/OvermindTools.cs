using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class OvermindTools
{
    private static readonly HashSet<string> ReadableEntryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "note", "decision", "gate", "guard", "skill"
    };

    private static readonly HashSet<string> WritableEntryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "note", "decision", "gate", "guard"
    };

    private const string SkillManagedElsewhere =
        "Skill entries are managed via the Overmind UI / future MCP delivery and cannot be written through this tool. Supported types: note, decision, gate, guard.";

    private readonly OvermindApiClient _api;

    public OvermindTools(OvermindApiClient api)
    {
        _api = api;
    }

    [McpServerTool(Name = "list_projects", ReadOnly = true, Idempotent = true)]
    [Description("Lists all overmind projects with id, name, disk path and description.")]
    public Task<JsonElement> ListProjects(CancellationToken ct) =>
        Guarded(() => _api.GetAsync("/api/projects", ct));

    [McpServerTool(Name = "list_entries", ReadOnly = true, Idempotent = true)]
    [Description("Lists entries of a project, optionally filtered by type (note, decision, gate, guard, skill).")]
    public async Task<JsonElement> ListEntries(
        Guid projectId,
        [Description("Optional entry type filter: note, decision, gate, guard or skill.")] string? type = null,
        CancellationToken ct = default)
    {
        var path = $"/api/projects/{projectId}/entries";
        if (!string.IsNullOrWhiteSpace(type))
        {
            path += $"?type={Uri.EscapeDataString(NormalizeType(type))}";
        }

        return await Guarded(() => _api.GetAsync(path, ct));
    }

    [McpServerTool(Name = "read_entry", ReadOnly = true, Idempotent = true)]
    [Description("Reads a single entry by id, including content, tags and version.")]
    public Task<JsonElement> ReadEntry(
        [Description("Id of the entry to read.")] Guid entryId,
        CancellationToken ct) =>
        Guarded(() => _api.GetAsync($"/api/entries/{entryId}", ct));

    [McpServerTool(Name = "write_entry", Destructive = false, Idempotent = false)]
    [Description("Creates a new entry (note, decision, gate or guard) in a project. Skills are managed via the Overmind UI.")]
    public async Task<JsonElement> WriteEntry(
        [Description("Id of the project to write into.")] Guid projectId,
        [Description("Entry type: note, decision, gate or guard.")] string type,
        [Description("Entry title.")] string title,
        [Description("Markdown body of the entry.")] string content,
        [Description("Optional tags attached to the entry.")] string[]? tags = null,
        CancellationToken ct = default)
    {
        var body = new
        {
            projectId,
            type = NormalizeWritableType(type),
            title,
            content,
            tags
        };

        var entry = await Guarded(() => _api.PostAsync("/api/entries", body, ct));
        return CompactEntry(entry);
    }

    [McpServerTool(Name = "update_entry", Destructive = false, Idempotent = false)]
    [Description("Replaces the content of an existing note, decision, gate or guard, keeping its title and tags. Skills are managed via the Overmind UI.")]
    public async Task<JsonElement> UpdateEntry(
        [Description("Id of the entry to update.")] Guid entryId,
        [Description("New markdown body of the entry.")] string content,
        CancellationToken ct = default)
    {
        var existing = await Guarded(() => _api.GetAsync($"/api/entries/{entryId}", ct));

        var entryType = existing.GetProperty("type").GetString();
        if (string.Equals(entryType, "skill", StringComparison.OrdinalIgnoreCase))
        {
            throw new McpException(SkillManagedElsewhere);
        }

        var title = existing.GetProperty("title").GetString() ?? string.Empty;

        var entry = await Guarded(() => _api.PutAsync($"/api/entries/{entryId}", new { title, content }, ct));
        return CompactEntry(entry);
    }

    [McpServerTool(Name = "search", ReadOnly = true)]
    [Description("Searches entries by fulltext or semantic mode. Semantic mode fails with 'vector search unavailable' when the vector store is down.")]
    public Task<JsonElement> Search(
        [Description("Search query text.")] string query,
        [Description("Optional project id to restrict the search to.")] Guid? projectId = null,
        [Description("Search mode: fulltext (default) or semantic.")] string? mode = null,
        CancellationToken ct = default)
    {
        var parameters = new List<string> { $"q={Uri.EscapeDataString(query)}" };
        if (projectId.HasValue)
        {
            parameters.Add($"projectId={projectId.Value}");
        }

        var effectiveMode = string.IsNullOrWhiteSpace(mode) ? "fulltext" : mode.Trim();
        parameters.Add("limit=20");
        parameters.Add($"mode={Uri.EscapeDataString(effectiveMode)}");

        return Guarded(() => _api.GetAsync("/api/search?" + string.Join('&', parameters), ct));
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

    private static JsonElement CompactEntry(JsonElement entry)
    {
        var compact = new JsonObject
        {
            ["id"] = entry.GetProperty("id").GetString(),
            ["type"] = entry.GetProperty("type").GetString(),
            ["version"] = entry.GetProperty("currentVersion").GetInt32(),
            ["updatedAt"] = entry.GetProperty("updatedAt").GetString(),
            ["bodyChars"] = entry.GetProperty("content").GetString()?.Length ?? 0
        };
        return compact.Deserialize<JsonElement>();
    }

    private static string NormalizeType(string type)
    {
        var normalized = type.Trim().ToLowerInvariant();
        if (!ReadableEntryTypes.Contains(normalized))
        {
            throw new McpException(
                $"Invalid entry type '{type}'. Expected one of: note, decision, gate, guard, skill.");
        }

        return normalized;
    }

    private static string NormalizeWritableType(string type)
    {
        var normalized = type.Trim().ToLowerInvariant();
        if (normalized == "skill")
        {
            throw new McpException(SkillManagedElsewhere);
        }

        if (!WritableEntryTypes.Contains(normalized))
        {
            throw new McpException(
                $"Invalid entry type '{type}'. Expected one of: note, decision, gate, guard.");
        }

        return normalized;
    }
}
