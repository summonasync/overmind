using System.Text.Json;
using ModelContextProtocol;

namespace Overmind.Mcp;

public sealed class ProjectResolver
{
    private const string ProjectIdEnvironmentVariable = "OVERMIND_PROJECT_ID";

    private readonly OvermindApiClient _api;

    public ProjectResolver(OvermindApiClient api)
    {
        _api = api;
    }

    // create only shapes the onboard body from 126/03 on; both door kinds post the cwd-only form today.
    public async Task<Guid> ResolveAsync(Guid? projectId, bool create, string errorNoun, CancellationToken ct)
    {
        if (projectId is not null)
        {
            return projectId.Value;
        }

        var overrideId = Environment.GetEnvironmentVariable(ProjectIdEnvironmentVariable);
        if (Guid.TryParse(overrideId, out var fromEnvironment))
        {
            return fromEnvironment;
        }

        JsonElement onboard;
        try
        {
            onboard = await _api.PostAsync("/api/onboard", new { cwd = Environment.CurrentDirectory }, ct);
        }
        catch (Exception ex) when (ex is ApiNotFoundException or ApiStatusException or ApiUnreachableException)
        {
            throw new McpException(
                $"Could not resolve the project {errorNoun}: {ex.Message}. Pass projectId explicitly or call overmind_onboard first.");
        }

        if (onboard.ValueKind == JsonValueKind.Object &&
            onboard.TryGetProperty("projectId", out var value) &&
            value.ValueKind == JsonValueKind.String &&
            Guid.TryParse(value.GetString(), out var resolved))
        {
            return resolved;
        }

        throw new McpException(
            "The host returned no usable projectId for the working directory. Call overmind_onboard, then pass its projectId explicitly.");
    }
}
