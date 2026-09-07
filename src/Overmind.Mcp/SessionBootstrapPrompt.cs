using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerPromptType]
public sealed class SessionBootstrapPrompt
{
    private const string FallbackText =
        "At session start, read ctx://projects to list available projects, then read " +
        "ctx://projects/{id}/gates and /guards for the active project and follow them. " +
        "Set OVERMIND_PROJECT_ID to make the active project explicit.";

    private readonly OvermindApiClient _api;

    public SessionBootstrapPrompt(OvermindApiClient api)
    {
        _api = api;
    }

    [McpServerPrompt(Name = "session_bootstrap", Title = "Overmind session bootstrap")]
    [Description("Instruction text telling the agent which gates and guards to read at session start.")]
    public async Task<GetPromptResult> GetBootstrap(CancellationToken ct)
    {
        var activeProjectId = Environment.GetEnvironmentVariable("OVERMIND_PROJECT_ID");

        if (string.IsNullOrWhiteSpace(activeProjectId))
        {
            JsonElement projects;
            try
            {
                projects = await _api.GetAsync("/api/projects", ct);
            }
            catch (Exception ex) when (ex is ApiUnreachableException or ApiStatusException)
            {
                throw new McpException(ex.Message);
            }

            var list = projects.EnumerateArray().ToArray();
            if (list.Length == 1)
            {
                activeProjectId = list[0].GetProperty("id").GetString();
            }
        }

        var text = string.IsNullOrWhiteSpace(activeProjectId)
            ? FallbackText
            : $"At session start, read ctx://projects/{activeProjectId}/gates and /guards and follow them.";

        return new GetPromptResult
        {
            Description = "Overmind session bootstrap instructions",
            Messages =
            [
                new PromptMessage
                {
                    Role = Role.User,
                    Content = new TextContentBlock { Text = text }
                }
            ]
        };
    }
}
