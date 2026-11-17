using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Overmind.Mcp.Planning;

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

    private const string SkillAppendRefused =
        "SkillEntryAppendRefused: skill entries are managed via the Overmind UI — they carry skill files and a kit lifecycle, so append_entry refuses them.";

    private const string KitOwnedEntryText =
        "KitOwnedEntry: entry '{0}' is managed by a skill kit '{1}' — the kit reimport or the UI owns it.";

    private const string ProjectMismatch =
        "Entry '{0}' does not belong to the resolved project ({1}). Pass the projectId that owns the entry, or call overmind_onboard.";

    private const string DecisionMemoryHint =
        "Durable decision — consider mirroring it into the crew memory (grimoire memory_remember) " +
        "and referencing it here with 'pointer: grimoire://memory/<id>'.";

    private const string ListEntriesDescription =
        "Lists a project's entries as metadata rows — the exploration door (bodies via include_content, one " +
        "full entry via read_entry, the start-of-work snapshot via overmind_briefing). Newest-updated first; " +
        "no pagination; q caps at 100 hits.";

    private static readonly string[] EntryMetadataFields =
        ["id", "type", "title", "tags", "createdAt", "updatedAt", "contentLength"];

    private const string SinceDescription =
        "Inclusive createdAt floor (ISO-8601 or yyyy-MM-dd, local midnight).";

    private const string UntilDescription =
        "Exclusive createdAt ceiling (ISO-8601 or yyyy-MM-dd, local midnight).";

    private const string AuthorDescription =
        "Author alias filter, case-insensitive; UI entries never match.";

    private const int FollowUpTextCap = 2000;

    private const string SearchDescription =
        "Fulltext search over entries plus planning features and artifact bodies — the cross-surface door " +
        "(list_entries explores entries only). Hits carry ids and a snippet, never full bodies. " +
        "since/until/author narrow to entries; semantic mode covers entries only.";

    private const string ReadProjectManifestDescription =
        "Reads the project's manifest markdown — stack, build/test commands, conventions — the deep read " +
        "behind list_projects' one-liner. Says so instead of failing when unset.";

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
        [Description("Type filter: note, decision, gate, guard, skill, idea, audit, tombstone.")] string? type = null,
        [Description("Lifecycle filter: all | active | lifted | superseded.")] string? state = null,
        [Description("Optional tag filter.")] string? tag = null,
        [Description("Optional fulltext query.")] string? q = null,
        [Description(SinceDescription)] string? since = null,
        [Description(UntilDescription)] string? until = null,
        [Description(AuthorDescription)] string? author = null,
        [Description("True to return full bodies instead of metadata rows.")] bool include_content = false,
        [Description("Row projection keys, e.g. title,updatedAt; id leads.")] string? fields = null,
        CancellationToken ct = default)
    {
        var requested = PlanningFieldProjection.ResolveRequested(
            fields,
            compact: false,
            include_content ? [.. EntryMetadataFields, "content"] : EntryMetadataFields);
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(type))
        {
            filters.Add($"type={Uri.EscapeDataString(NormalizeType(type))}");
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            filters.Add($"state={Uri.EscapeDataString(state.Trim())}");
        }

        AddRangeFilter(filters, "since", since);
        AddRangeFilter(filters, "until", until);
        AddRangeFilter(filters, "author", author);

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
            return requested.Length == 0
                ? payload
                : PlanningFieldProjection.ProjectRows(payload, null, requested, "id");
        }

        var listing = new JsonArray();
        foreach (var entry in payload.EnumerateArray())
        {
            listing.Add(EntryMetadata(entry));
        }

        return requested.Length == 0
            ? listing.Deserialize<JsonElement>()
            : PlanningFieldProjection.ProjectRows(listing.Deserialize<JsonElement>(), null, requested, "id");
    }

    [McpServerTool(Name = "read_entry", ReadOnly = true, Idempotent = true)]
    [Description("Reads a single entry by id, including content, tags and version.")]
    public Task<JsonElement> ReadEntry(
        [Description("Entry id.")] Guid entryId,
        CancellationToken ct) =>
        Guarded(() => _api.GetAsync($"/api/entries/{entryId}", ct));

    [McpServerTool(Name = "write_entry", Destructive = false, Idempotent = false)]
    [Description("Creates a note, decision, gate, guard, idea, audit or tombstone entry — the durable board " +
        "write; casual notes belong to grimoire. A hand-made tombstone buries an approach; idea is the " +
        "informal lane. Skills are UI-managed.")]
    public async Task<JsonElement> WriteEntry(
        [Description("Entry type: note, decision, gate, guard, idea, audit, tombstone.")] string type,
        [Description("Entry title.")] string title,
        [Description("Markdown body of the entry.")] string content,
        [Description("Optional tags attached to the entry.")] string[]? tags = null,
        [Description("Decision-only standing-rule flag; refused true on other types.")] bool? enforces = null,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolvedProjectId = await _projects.ResolveAsync(projectId, create: false, "for the entry", ct);

        var body = new
        {
            projectId = resolvedProjectId,
            type = NormalizeWritableType(type),
            title,
            content,
            tags,
            enforces
        };

        var entry = await Guarded(() => _api.PostAsync("/api/entries", body, ct));
        return CompactEntry(entry);
    }

    [McpServerTool(Name = "update_entry", Destructive = false, Idempotent = false)]
    [Description("Replaces an existing entry's content, keeping its title, tags and type; skills are " +
        "UI-managed. For new information without a rewrite, append_entry is the cheaper door.")]
    public async Task<JsonElement> UpdateEntry(
        [Description("Entry id.")] Guid entryId,
        [Description("New markdown body.")] string content,
        [Description("Decision-only standing-rule flag; explicit sets it, omit keeps.")] bool? enforces = null,
        CancellationToken ct = default)
    {
        var existing = await Guarded(() => _api.GetAsync($"/api/entries/{entryId}", ct));

        var entryType = existing.GetProperty("type").GetString();
        if (string.Equals(entryType, "skill", StringComparison.OrdinalIgnoreCase))
        {
            throw new McpException(SkillManagedElsewhere);
        }

        var title = existing.GetProperty("title").GetString() ?? string.Empty;

        var entry = await Guarded(() => _api.PutAsync($"/api/entries/{entryId}", new { title, content, enforces }, ct));
        return CompactEntry(entry);
    }

    [McpServerTool(Name = "delete_entry", Destructive = true, Idempotent = true)]
    [Description("Hard-deletes an entry — row and version history gone — and births a tombstone in its " +
        "place carrying title, type, date, provenance and final content. Destructive; tombstones stay " +
        "listed and searchable. Kit- and skill-owned entries are refused; to retire a standing rule use " +
        "lift_entry.")]
    public async Task<JsonElement> DeleteEntry(
        [Description("Entry id.")] Guid entryId,
        [Description("Optional reason for the tombstone; ignored for tombstones.")] string? reason = null,
        [Description("Optional project id.")] Guid? projectId = null,
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

    [McpServerTool(Name = "lift_entry", Destructive = false, Idempotent = true)]
    [Description("Retires one standing rule — guard, gate or decision that no longer applies — without " +
        "deleting: state moves to lifted, or superseded when supersededById names the replacement; body and " +
        "title stay untouched. Skills and tombstones refuse.")]
    public async Task<JsonElement> LiftEntry(
        [Description("Entry id.")] Guid entryId,
        [Description("Why the rule no longer applies.")] string reason,
        [Description("Optional opaque proof — notification or reply id.")] string? proof = null,
        [Description("Optional replacement id — marks the lift superseded.")] Guid? supersededById = null,
        CancellationToken ct = default)
    {
        var lifted = await Guarded(() => _api.PostAsync(
            $"/api/entries/{entryId}/lift",
            new { reason, proof, supersededById },
            ct));

        return LifecycleEcho(lifted);
    }

    [McpServerTool(Name = "append_entry", Destructive = false, Idempotent = false)]
    [Description("Appends a dated '## Follow-up' block to an entry's tail through the version machinery — " +
        "the cheap door for new information on a note, decision, gate or guard without an update_entry " +
        "rewrite; the previous version stays readable. text is capped at 2000 chars here.")]
    public async Task<JsonElement> AppendEntry(
        [Description("Entry id.")] Guid entryId,
        [Description("Follow-up text, max 2000 chars.")] string text,
        CancellationToken ct = default)
    {
        TextCapLint.Cap(text, FollowUpTextCap);

        var existing = await Guarded(() => _api.GetAsync($"/api/entries/{entryId}", ct));
        if (string.Equals(existing.GetProperty("type").GetString(), "skill", StringComparison.OrdinalIgnoreCase))
        {
            throw new McpException(SkillAppendRefused);
        }

        var appended = await Guarded(() => _api.PostAsync(
            $"/api/entries/{entryId}/append",
            new { text },
            ct));

        return LifecycleEcho(appended);
    }

    [McpServerTool(Name = "search", ReadOnly = true)]
    [Description(SearchDescription)]
    public async Task<JsonElement> Search(
        [Description("Search text.")] string query,
        [Description("Optional project id to restrict hits.")] Guid? projectId = null,
        [Description("Search mode: fulltext (default) or semantic.")] string? mode = null,
        [Description(SinceDescription)] string? since = null,
        [Description(UntilDescription)] string? until = null,
        [Description(AuthorDescription)] string? author = null,
        CancellationToken ct = default)
    {
        var parameters = new List<string> { $"q={Uri.EscapeDataString(query)}" };
        if (projectId.HasValue)
        {
            parameters.Add($"projectId={projectId.Value}");
        }

        AddRangeFilter(parameters, "since", since);
        AddRangeFilter(parameters, "until", until);
        AddRangeFilter(parameters, "author", author);

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
        [Description("Optional project id.")] Guid? projectId = null,
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
        var type = entry.GetProperty("type").GetString();
        var compact = new JsonObject
        {
            ["id"] = entry.GetProperty("id").GetString(),
            ["type"] = type,
            ["version"] = entry.GetProperty("currentVersion").GetInt32(),
            ["updatedAt"] = entry.GetProperty("updatedAt").GetString(),
            ["bodyChars"] = entry.GetProperty("content").GetString()?.Length ?? 0
        };

        if (string.Equals(type, "decision", StringComparison.OrdinalIgnoreCase))
        {
            compact["hint"] = DecisionMemoryHint;
        }

        return compact.Deserialize<JsonElement>();
    }

    private static JsonElement LifecycleEcho(JsonElement entry)
    {
        var echo = new JsonObject
        {
            ["id"] = entry.GetProperty("id").GetString(),
            ["type"] = entry.GetProperty("type").GetString(),
            ["version"] = entry.GetProperty("currentVersion").GetInt32(),
            ["state"] = entry.GetProperty("state").GetString(),
            ["updatedAt"] = entry.GetProperty("updatedAt").GetString(),
            ["bodyChars"] = entry.GetProperty("content").GetString()?.Length ?? 0
        };

        var supersededById = entry.GetProperty("supersededById").GetString();
        if (supersededById is not null)
        {
            echo["supersededById"] = supersededById;
        }

        return echo.Deserialize<JsonElement>();
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

    private static void AddRangeFilter(ICollection<string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
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
