using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Overmind.Infrastructure.Agent;

public interface IAgentToolInvoker
{
    IReadOnlyList<AITool> CreateTools();

    Task<JsonElement> InvokeAsync(string toolName, JsonElement arguments, CancellationToken ct);
}
