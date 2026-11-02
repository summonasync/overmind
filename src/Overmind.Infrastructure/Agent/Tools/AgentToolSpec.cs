using System.Text.Json;

namespace Overmind.Infrastructure.Agent.Tools;

public delegate Task<JsonElement> AgentToolHandler(JsonElement arguments, CancellationToken ct);

public sealed record AgentToolSpec(
    string Name,
    string Description,
    string ParametersJson,
    AgentToolHandler Invoke,
    AgentToolCategory Category = AgentToolCategory.Standard);
