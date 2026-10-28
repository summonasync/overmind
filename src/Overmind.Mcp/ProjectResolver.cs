using System.Text.Json;
using System.Text.Json.Nodes;
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

    public async Task<Guid> ResolveAsync(Guid? projectId, bool create, string errorNoun, CancellationToken ct, string? workingDirectory = null)
    {
        var cwd = workingDirectory ?? Environment.CurrentDirectory;

        if (projectId is not null)
        {
            return projectId.Value;
        }

        var overrideId = Environment.GetEnvironmentVariable(ProjectIdEnvironmentVariable);
        if (Guid.TryParse(overrideId, out var fromEnvironment))
        {
            return fromEnvironment;
        }

        var pin = ProjectPinFile.Read(cwd);
        if (pin.ProjectId is { } pinnedId)
        {
            if (await IsRegisteredAsync(pinnedId, cwd, ct))
            {
                return pinnedId;
            }
        }
        else if (pin.IsUnparsable)
        {
            Console.Error.WriteLine($"[overmind-mcp] WARN: ignoring unparsable pin file {pin.FilePath}");
        }

        var body = new JsonObject { ["cwd"] = cwd };
        if (!create)
        {
            body["create"] = false;
        }

        JsonElement onboard;
        try
        {
            onboard = await _api.PostAsync("/api/onboard", body, ct);
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

    private async Task<bool> IsRegisteredAsync(Guid pinnedId, string cwd, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["cwd"] = cwd,
            ["projectId"] = pinnedId,
        };
        try
        {
            await _api.PostAsync("/api/onboard", body, ct);
            return true;
        }
        catch (Exception ex) when (ex is ApiNotFoundException or ApiStatusException or ApiUnreachableException)
        {
            Console.Error.WriteLine($"[overmind-mcp] WARN: ignoring .overmind/project pin {pinnedId}: {ex.Message}");
            return false;
        }
    }
}
