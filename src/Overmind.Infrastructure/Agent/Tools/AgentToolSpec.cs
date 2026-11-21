using System.Text.Json;

namespace Overmind.Infrastructure.Agent.Tools;

public delegate Task<JsonElement> AgentToolHandler(JsonElement arguments, CancellationToken ct);

public delegate Task<JsonElement> AgentDelegatedToolHandler(
    JsonElement arguments, Guid instructionMessageId, CancellationToken ct);

public delegate Task<JsonElement> AgentConversationToolHandler(
    JsonElement arguments, Guid conversationId, CancellationToken ct);

public delegate Task<JsonElement> AgentScopedToolHandler(
    JsonElement arguments, Guid? callerProjectId, CancellationToken ct);

public delegate Task<JsonElement> AgentDelegatedScopedToolHandler(
    JsonElement arguments, Guid instructionMessageId, Guid? callerProjectId, CancellationToken ct);

public sealed record AgentToolSpec(
    string Name,
    string Description,
    string ParametersJson,
    AgentToolHandler Invoke,
    AgentToolCategory Category = AgentToolCategory.Standard,
    AgentDelegatedToolHandler? DelegatedInvoke = null,
    AgentConversationToolHandler? ConversationInvoke = null,
    AgentToolScope Scope = AgentToolScope.Enforce,
    AgentScopedToolHandler? ScopedInvoke = null,
    AgentDelegatedScopedToolHandler? DelegatedScopedInvoke = null);
