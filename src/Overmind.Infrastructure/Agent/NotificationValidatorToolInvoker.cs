using System.Text.Json;
using Microsoft.Extensions.AI;
using Overmind.Infrastructure.Agent.Tools;

namespace Overmind.Infrastructure.Agent;

public sealed class NotificationValidatorToolInvoker(IAgentToolInvoker full) : IAgentToolInvoker
{
    public static readonly IReadOnlySet<string> AllowedTools =
        new HashSet<string>(
            ["get_board_context", "planning_validate", "planning_set_feature_status"],
            StringComparer.Ordinal);

    public const string RefusalTemplate = "'{0}' is outside the notification validator's toolset.";

    public IReadOnlyList<AITool> CreateTools(bool includeHumanGated) =>
        full.CreateTools(includeHumanGated)
            .Where(tool => AllowedTools.Contains(tool.Name))
            .ToList();

    public async Task<JsonElement> InvokeAsync(
        string toolName, JsonElement arguments, AgentToolInvocation invocation, CancellationToken ct)
    {
        if (!AllowedTools.Contains(toolName.Trim()))
        {
            return Error(string.Format(RefusalTemplate, toolName));
        }

        return await full.InvokeAsync(toolName, arguments, invocation, ct);
    }

    private static JsonElement Error(string message) =>
        JsonDocument.Parse(JsonSerializer.Serialize(new { error = message })).RootElement.Clone();
}
