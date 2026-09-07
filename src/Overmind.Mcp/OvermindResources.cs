using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerResourceType]
public sealed class OvermindResources
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly OvermindApiClient _api;

    public OvermindResources(OvermindApiClient api)
    {
        _api = api;
    }

    [McpServerResource(UriTemplate = "overmind://projects", Name = "projects", Title = "Overmind projects", MimeType = "application/json")]
    [Description("Lists all overmind projects.")]
    public async Task<string> ListProjects(CancellationToken ct)
    {
        var projects = await Guarded(() => _api.GetAsync("/api/projects", ct));
        return projects.ToString();
    }

    [McpServerResource(UriTemplate = "overmind://projects/{id}", Name = "project", Title = "Project meta", MimeType = "application/json")]
    [Description("Metadata of a single overmind project.")]
    public async Task<string> GetProject(Guid id, CancellationToken ct)
    {
        var project = await Guarded(() => _api.GetAsync($"/api/projects/{id}", ct));
        return project.ToString();
    }

    [McpServerResource(UriTemplate = "overmind://projects/{id}/gates", Name = "project-gates", Title = "Project gates", MimeType = "text/markdown")]
    [Description("All gate entries of a project, concatenated as markdown with title headers.")]
    public Task<string> GetGates(Guid id, CancellationToken ct) =>
        Guarded(() => GetEntriesMarkdown(id, "gate", ct));

    [McpServerResource(UriTemplate = "overmind://projects/{id}/guards", Name = "project-guards", Title = "Project guards", MimeType = "text/markdown")]
    [Description("All guard entries of a project, concatenated as markdown with title headers.")]
    public Task<string> GetGuards(Guid id, CancellationToken ct) =>
        Guarded(() => GetEntriesMarkdown(id, "guard", ct));

    [McpServerResource(UriTemplate = "overmind://projects/{id}/notes", Name = "project-notes", Title = "Project notes", MimeType = "text/markdown")]
    [Description("All note entries of a project, concatenated as markdown with title headers.")]
    public Task<string> GetNotes(Guid id, CancellationToken ct) =>
        Guarded(() => GetEntriesMarkdown(id, "note", ct));

    [McpServerResource(UriTemplate = "overmind://projects/{id}/decisions", Name = "project-decisions", Title = "Project decisions", MimeType = "text/markdown")]
    [Description("All decision entries of a project, concatenated as markdown with title headers.")]
    public Task<string> GetDecisions(Guid id, CancellationToken ct) =>
        Guarded(() => GetEntriesMarkdown(id, "decision", ct));

    [McpServerResource(UriTemplate = "overmind://projects/{id}/skills", Name = "project-skills", Title = "Project skills", MimeType = "text/markdown")]
    [Description("All skill entries of a project, concatenated as markdown with title headers.")]
    public Task<string> GetSkills(Guid id, CancellationToken ct) =>
        Guarded(() => GetEntriesMarkdown(id, "skill", ct));

    [McpServerResource(UriTemplate = "overmind://entries/{id}", Name = "entry", Title = "Context entry", MimeType = "text/markdown")]
    [Description("A single context entry rendered as markdown with a title header.")]
    public async Task<string> GetEntry(Guid id, CancellationToken ct)
    {
        var entry = await Guarded(() => _api.GetAsync($"/api/entries/{id}", ct));
        return RenderEntry(entry);
    }

    private async Task<string> GetEntriesMarkdown(Guid projectId, string type, CancellationToken ct)
    {
        var entries = await _api.GetAsync($"/api/projects/{projectId}/entries?type={type}", ct);

        var builder = new StringBuilder();
        foreach (var entry in entries.EnumerateArray())
        {
            builder.Append(RenderEntry(entry)).Append('\n');
        }

        return builder.ToString();
    }

    private static string RenderEntry(JsonElement entry)
    {
        var title = entry.TryGetProperty("title", out var t) ? t.GetString() : string.Empty;
        var content = entry.TryGetProperty("content", out var c) ? c.GetString() : string.Empty;
        return $"# {title}\n\n{content}\n";
    }

    private static async Task<T> Guarded<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (ApiNotFoundException ex)
        {
            throw new McpProtocolException(ex.Message, McpErrorCode.ResourceNotFound);
        }
        catch (Exception ex) when (ex is SemanticSearchUnavailableException or ApiUnreachableException or ApiStatusException)
        {
            throw new McpException(ex.Message);
        }
    }
}
