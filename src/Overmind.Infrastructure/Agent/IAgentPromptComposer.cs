using Microsoft.Extensions.AI;
using Overmind.Core;

namespace Overmind.Infrastructure.Agent;

public interface IAgentPromptComposer
{
    Task<IReadOnlyList<ChatMessage>> ComposeAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        CancellationToken ct);
}
