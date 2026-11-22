using System.Text.Json;
using ModelContextProtocol;

namespace Overmind.Mcp;

public sealed class JournalProjectResolver
{
    public const string ContainerName = "journal";

    public const string MissingContainerMessage =
        "No journal container exists yet — capture one thought with journal_capture (projectId omitted births it). (JournalProjectMissing)";

    private readonly OvermindApiClient _api;

    public JournalProjectResolver(OvermindApiClient api)
    {
        _api = api;
    }

    public async Task<Guid> ResolveAsync(Guid? projectId, CancellationToken ct)
    {
        if (projectId is { } named)
        {
            return named;
        }

        JsonElement projects;
        try
        {
            projects = await _api.GetAsync("/api/projects", ct);
        }
        catch (Exception ex) when (ex is ApiNotFoundException or ApiStatusException or ApiUnreachableException)
        {
            throw new McpException($"Could not resolve the journal container: {ex.Message}");
        }

        foreach (var project in projects.EnumerateArray())
        {
            if (!project.TryGetProperty("name", out var name) ||
                !string.Equals(name.GetString()?.Trim(), ContainerName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return project.GetProperty("id").GetGuid();
        }

        throw new McpException(MissingContainerMessage);
    }
}
