using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Overmind.Infrastructure.Agent;

public interface IAgentToolInvoker
{
    IReadOnlyList<AITool> CreateTools(bool includeHumanGated);

    Task<JsonElement> InvokeAsync(string toolName, JsonElement arguments, AgentToolInvocation invocation, CancellationToken ct);
}
