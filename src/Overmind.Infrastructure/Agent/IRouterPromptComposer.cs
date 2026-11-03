using Microsoft.Extensions.AI;
using Overmind.Core;
using Overmind.Infrastructure.Agent.Routes;

namespace Overmind.Infrastructure.Agent;

public interface IRouterPromptComposer
{
    Task<IReadOnlyList<ChatMessage>> ComposeClassificationAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        IReadOnlyList<AgentRouteRecord> hotHead,
        string userText,
        CancellationToken ct);

    Task<IReadOnlyList<ChatMessage>> ComposeChatAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        IReadOnlyList<AgentRouteRecord> hotHead,
        string userText,
        CancellationToken ct);
}
