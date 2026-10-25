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
        "note", "decision", "gate", "guard", "idea", "audit", "skill", "tombstone"
    };

    private static readonly HashSet<string> WritableEntryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "note", "decision", "gate", "guard", "idea", "audit", "tombstone"
    };

    private const string SkillManagedElsewhere =
        "Skill entries are managed via the Overmind UI / future MCP delivery and cannot be written through this tool. Supported types: note, decision, gate, guard, idea, audit, tombstone.";

    private const string SkillDeleteRefused =
        "SkillEntryDeleteRefused: skill entries are managed via the Overmind UI — they carry skill files and a kit lifecycle, so delete_entry refuses them.";

    private const string KitOwnedEntryText =
        "KitOwnedEntry: entry '{0}' is managed by a skill kit '{1}' — the kit reimport or the UI owns it.";

    private const string ProjectMismatch =
        "Entry '{0}' does not belong to the resolved project ({1}). Pass the projectId that owns the entry, or call overmind_onboard.";

    private const string ListEntriesDescription =
        "Lists entries of a project. Returns metadata only by default: id, type, title, tags, createdAt, updatedAt and contentLength in characters — no content. " +
        "Read a single body with read_entry, or pass include_content=true to receive entries with full content. " +
        "Optional filters: type (note, decision, gate, guard, skill, idea, audit, tombstone), tag and q (fulltext). " +
        "All matching entries are returned in one array, most recently updated first; there is no pagination, and a q search is capped at 100 hits.";

    private const string SearchDescription =
        "Searches entries and planning artifacts by fulltext; semantic mode searches entries only and fails with 'vector search unavailable' when the vector store is down. " +
        "Fulltext covers entries plus planning features and their spec/plan/task/result bodies. " +
        "Each hit carries its source (entry, feature or artifact) with only its id fields, a title or label, and a snippet — a short window of lines around the first query-term match; " +
        "full bodies are not returned, read a full entry with read_entry or an artifact with planning_read_artifact.";

    private const string ReadProjectManifestDescription =
        "Reads the project's manifest — the full markdown describing what the project is, its stack, build/test commands and conventions; the deep read that complements the one-line description in list_projects. " +
        "Returns the manifest markdown verbatim. When no manifest is set the tool says so instead of failing — " +
        "set one on the project config page in the Overmind UI or import it from AGENTS.md/CLAUDE.md/QWEN.md. " +
        "projectId is resolved from OVERMIND_PROJECT_ID or the working directory when omitted; " +
        "the resolution never creates a project row; an unregistered directory returns guidance instead.";

    private readonly OvermindApiClient _api;
    private readonly ProjectResolver _projects;

    public OvermindTools(OvermindApiClient api, ProjectResolver projects)
    {
        _api = api;
        _projects = projects;
    }

    [McpServerTool(Name = "list_projects", ReadOnly = true, Idempotent = true)]
    [Description("Lists all overmind projects with id, name, disk path and description.")]
    public Task<JsonElement> ListProjects(CancellationToken ct) =>
        Guarded(() => _api.GetAsync("/api/projects", ct));

    [McpServerTool(Name = "list_entries", ReadOnly = true, Idempotent = true)]
    [Description(ListEntriesDescription)]
    public async Task<JsonElement> ListEntries(
        Guid projectId,
        [Description("Optional entry type filter: note, decision, gate, guard, skill, idea, audit or tombstone.")] string? type = null,
        [Description("Optional tag filter; only entries carrying this tag are returned.")] string? tag = null,
        [Description("Optional fulltext query narrowing the listing to matching entries.")] string? q = null,
        [Description("Set true to include full entry bodies; by default only metadata is returned.")] bool include_content = false,
        CancellationToken ct = default)
    {
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(type))
        {
            filters.Add($"type={Uri.EscapeDataString(NormalizeType(type))}");
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            filters.Add($"tag={Uri.EscapeDataString(tag.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            filters.Add($"q={Uri.EscapeDataString(q.Trim())}");
        }

        var path = $"/api/projects/{projectId}/entries";
        if (filters.Count > 0)
        {
            path += "?" + string.Join('&', filters);
        }

        var payload = await Guarded(() => _api.GetAsync(path, ct));
        if (include_content)
        {
            return payload;
        }

        var listing = new JsonArray();
        foreach (var entry in payload.EnumerateArray())
        {
            listing.Add(EntryMetadata(entry));
        }

        return listing.Deserialize<JsonElement>();
    }

    [McpServerTool(Name = "read_entry", ReadOnly = true, Idempotent = true)]
    [Description("Reads a single entry by id, including content, tags and version.")]
    public Task<JsonElement> ReadEntry(
        [Description("Id of the entry to read.")] Guid entryId,
        CancellationToken ct) =>
        Guarded(() => _api.GetAsync($"/api/entries/{entryId}", ct));

    [McpServerTool(Name = "write_entry", Destructive = false, Idempotent = false)]
    [Description("Creates a new entry (note, decision, gate, guard, idea, audit or tombstone) in a project. A hand-made tombstone buries an approach without deleting anything. Skills are managed via the Overmind UI. idea is the informal lane for unformed product thinking — no statuses, no lifecycle; promotion to a board feature stays a ceremony act.")]
    public async Task<JsonElement> WriteEntry(
        [Description("Id of the project to write into.")] Guid projectId,
        [Description("Entry type: note, decision, gate, guard, idea, audit or tombstone.")] string type,
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
    [Description("Replaces the content of an existing note, decision, gate, guard, idea, audit or tombstone, keeping its title and tags. Skills are managed via the Overmind UI.")]
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

    [McpServerTool(Name = "delete_entry", Destructive = true, Idempotent = true)]
    [Description("Deletes one entry — notes, decisions, gates, guards, ideas, audits and tombstones. Deletion is permanent: the row and its version history are gone, and a tombstone entry is born in its place carrying the original title with ' (deleted)' appended, the original type, the deletion date, door+actor provenance, the optional reason from this call, and the final content preserved in a fenced block. Deleting a tombstone is a plain delete — no second tombstone. Tombstones stay discoverable: list them via list_entries with type=tombstone; they ride search like every other entry. Kit-linked entries are refused with KitOwnedEntry: 'managed by a skill kit — the kit reimport or the UI owns it'. Skill entries are refused too — the UI owns their lifecycle. projectId is resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")]
    public async Task<JsonElement> DeleteEntry(
        [Description("Id of the entry to delete.")] Guid entryId,
        [Description("Optional reason recorded on the tombstone — why the entry died. Ignored when the entry is itself a tombstone (plain delete).")] string? reason = null,
        [Description("Id of the project the entry belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolvedProjectId = await _projects.ResolveAsync(projectId, create: true, "for the entry", ct);

        var entry = await Guarded(() => _api.GetAsync($"/api/entries/{entryId}", ct));

        if (!Guid.TryParse(entry.GetProperty("projectId").GetString(), out var ownerProject) ||
            ownerProject != resolvedProjectId)
        {
            throw new McpException(string.Format(ProjectMismatch, entryId, resolvedProjectId));
        }

        if (string.Equals(entry.GetProperty("type").GetString(), "skill", StringComparison.OrdinalIgnoreCase))
        {
            throw new McpException(SkillDeleteRefused);
        }

        var kits = await Guarded(() => _api.GetAsync("/api/kits", ct));
        foreach (var kit in kits.EnumerateArray())
        {
            var owns = kit.TryGetProperty("entryIds", out var entryIds) &&
                entryIds.ValueKind == JsonValueKind.Array &&
                entryIds.EnumerateArray().Any(id =>
                    Guid.TryParse(id.GetString(), out var owned) && owned == entryId);

            if (owns)
            {
                throw new McpException(string.Format(
                    KitOwnedEntryText, entryId, kit.GetProperty("name").GetString()));
            }
        }

        var path = $"/api/entries/{entryId}";
        if (!string.IsNullOrWhiteSpace(reason))
        {
            path += $"?reason={Uri.EscapeDataString(reason.Trim())}";
        }

        await Guarded(() => _api.DeleteAsync(path, ct));

        var echo = new JsonObject
        {
            ["id"] = entryId.ToString(),
            ["type"] = entry.GetProperty("type").GetString(),
            ["deleted"] = true
        };
        return echo.Deserialize<JsonElement>();
    }

    [McpServerTool(Name = "search", ReadOnly = true)]
    [Description(SearchDescription)]
    public async Task<JsonElement> Search(
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

        var payload = await Guarded(() => _api.GetAsync("/api/search?" + string.Join('&', parameters), ct));

        var hits = new JsonArray();
        if (payload.TryGetProperty("hits", out var found) && found.ValueKind == JsonValueKind.Array)
        {
            foreach (var hit in found.EnumerateArray())
            {
                hits.Add(SearchHit(hit, query));
            }
        }

        return hits.Deserialize<JsonElement>();
    }

    [McpServerTool(Name = "read_project_manifest", ReadOnly = true, Idempotent = true)]
    [Description(ReadProjectManifestDescription)]
    public async Task<string> ReadProjectManifest(
        [Description("Id of the project whose manifest to read; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolvedProjectId = await _projects.ResolveAsync(projectId, create: false, "for the entry", ct);
        var project = await Guarded(() => _api.GetAsync($"/api/projects/{resolvedProjectId}", ct));

        var manifest = project.ValueKind == JsonValueKind.Object &&
            project.TryGetProperty("manifest", out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        return manifest ??
            $"No manifest is set for project '{resolvedProjectId}'. Set one on the project config page in the Overmind UI, or import it from AGENTS.md/CLAUDE.md/QWEN.md.";
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

    private static JsonObject SearchHit(JsonElement hit, string query)
    {
        var source = hit.TryGetProperty("source", out var rawSource) ? rawSource.GetString() : null;
        var snippet = SearchSnippet.Window(
            hit.TryGetProperty("snippet", out var raw) ? raw.GetString() ?? string.Empty : string.Empty,
            query);

        return source switch
        {
            "feature" => FeatureHit(hit, snippet),
            "artifact" => ArtifactHit(hit, snippet),
            _ => EntryHit(hit, snippet)
        };
    }

    private static JsonObject EntryHit(JsonElement hit, string snippet) =>
        new()
        {
            ["source"] = "entry",
            ["id"] = hit.GetProperty("entryId").GetString(),
            ["title"] = hit.TryGetProperty("title", out var title) ? title.GetString() : null,
            ["type"] = hit.GetProperty("type").GetString(),
            ["snippet"] = snippet
        };

    private static JsonObject FeatureHit(JsonElement hit, string snippet) =>
        new()
        {
            ["source"] = "feature",
            ["featureSlug"] = hit.GetProperty("featureSlug").GetString(),
            ["title"] = hit.TryGetProperty("title", out var title) ? title.GetString() : null,
            ["snippet"] = snippet
        };

    private static JsonObject ArtifactHit(JsonElement hit, string snippet) =>
        new()
        {
            ["source"] = "artifact",
            ["artifactId"] = hit.GetProperty("artifactId").GetString(),
            ["featureSlug"] = hit.GetProperty("featureSlug").GetString(),
            ["kind"] = hit.GetProperty("kind").GetString(),
            ["order"] = hit.GetProperty("order").GetInt32(),
            ["label"] = hit.GetProperty("label").GetString(),
            ["snippet"] = snippet
        };

    private static JsonObject EntryMetadata(JsonElement entry)
    {
        var projection = new JsonObject
        {
            ["id"] = entry.GetProperty("id").GetString(),
            ["type"] = entry.GetProperty("type").GetString(),
            ["title"] = entry.GetProperty("title").GetString(),
            ["tags"] = JsonNode.Parse(entry.GetProperty("tags").GetRawText())
        };

        if (entry.TryGetProperty("createdAt", out var createdAt))
        {
            projection["createdAt"] = createdAt.GetString();
        }

        projection["updatedAt"] = entry.GetProperty("updatedAt").GetString();
        projection["contentLength"] = entry.GetProperty("content").GetString()?.Length ?? 0;
        return projection;
    }

    private static string NormalizeType(string type)
    {
        var normalized = type.Trim();
        if (!ReadableEntryTypes.Contains(normalized))
        {
            throw new McpException(
                $"Invalid entry type '{type}'. Expected one of: note, decision, gate, guard, skill, idea, audit, tombstone.");
        }

        return normalized;
    }

    private static string NormalizeWritableType(string type)
    {
        var normalized = type.Trim();
        if (normalized.Equals("skill", StringComparison.OrdinalIgnoreCase))
        {
            throw new McpException(SkillManagedElsewhere);
        }

        if (!WritableEntryTypes.Contains(normalized))
        {
            throw new McpException(
                $"Invalid entry type '{type}'. Expected one of: note, decision, gate, guard, idea, audit, tombstone.");
        }

        return normalized;
    }
}
