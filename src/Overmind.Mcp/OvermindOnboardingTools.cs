using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class OvermindOnboardingTools
{
    private const string ProjectIdEnvironmentVariable = "OVERMIND_PROJECT_ID";

    private readonly OvermindApiClient _api;

    public OvermindOnboardingTools(OvermindApiClient api)
    {
        _api = api;
    }

    [McpServerTool(Name = "overmind_onboard", Destructive = false, Idempotent = false)]
    [Description("Call this first before any other overmind tool. Reports the adapter's working directory to the host, which resolves (creating if needed) the matching project, and returns the project id, board summary and onboarding quickstart. The payload also carries a materialization block (strategy, targetDir, trigger, lastRun) and a validation block (humanGate). Set OVERMIND_PROJECT_ID to a valid project guid to skip cwd matching — use this for symlinked or worktree layouts where the resolved cwd differs from the registered project path.")]
    public async Task<string> Onboard(CancellationToken ct)
    {
        var payload = await Guarded(() => _api.PostAsync("/api/onboard", BuildRequest(), ct));
        return Format(payload);
    }

    private static object BuildRequest()
    {
        var overrideId = Environment.GetEnvironmentVariable(ProjectIdEnvironmentVariable);
        return Guid.TryParse(overrideId, out var projectId)
            ? new { projectId }
            : new { cwd = Environment.CurrentDirectory };
    }

    private static async Task<JsonElement> Guarded(Func<Task<JsonElement>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is ApiStatusException or ApiNotFoundException or ApiUnreachableException)
        {
            throw new McpException(ex.Message);
        }
    }

    private static string Format(JsonElement payload)
    {
        var projectName = payload.GetProperty("projectName").GetString() ?? "unknown";
        var projectId = payload.GetProperty("projectId").GetString() ?? string.Empty;

        var output = new StringBuilder();
        output.AppendLine($"Project: {projectName} ({projectId})");
        if (payload.TryGetProperty("created", out var created) &&
            created.ValueKind == JsonValueKind.True &&
            created.GetBoolean())
        {
            output.AppendLine($"Created project '{projectName}'.");
        }

        var board = payload.GetProperty("board");
        if (board.TryGetProperty("countsByStatus", out var counts) &&
            counts.ValueKind == JsonValueKind.Object &&
            counts.EnumerateObject().Any())
        {
            output.AppendLine($"Board: {string.Join(", ", counts.EnumerateObject().Select(p => $"{p.Name}: {p.Value}"))}");
        }
        else
        {
            output.AppendLine("Board: no features yet.");
        }

        if (board.TryGetProperty("currentSlug", out var current) &&
            current.ValueKind == JsonValueKind.String)
        {
            output.AppendLine($"Current feature: {current.GetString()}");
        }

        output.AppendLine();
        output.Append(payload.GetProperty("quickstart").GetString());
        return output.ToString();
    }
}
