using System.Text.Json;

namespace Overmind.Infrastructure.Agent.Tools;

public delegate Task<JsonElement> AgentToolHandler(JsonElement arguments, CancellationToken ct);

public delegate Task<JsonElement> AgentDelegatedToolHandler(
    JsonElement arguments, Guid instructionMessageId, CancellationToken ct);

public delegate Task<JsonElement> AgentConversationToolHandler(
    JsonElement arguments, Guid conversationId, CancellationToken ct);

public sealed record AgentToolSpec(
    string Name,
    string Description,
    string ParametersJson,
    AgentToolHandler Invoke,
    AgentToolCategory Category = AgentToolCategory.Standard,
    AgentDelegatedToolHandler? DelegatedInvoke = null,
    AgentConversationToolHandler? ConversationInvoke = null);
