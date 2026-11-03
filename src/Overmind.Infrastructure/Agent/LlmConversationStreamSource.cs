using System.Runtime.CompilerServices;
using Overmind.Core;

namespace Overmind.Infrastructure.Agent;

public sealed class LlmConversationStreamSource(AgentLoop loop) : IConversationStreamSource
{
    public const string ProviderMissingNotice =
        "No LLM provider configured - set the agent base URL, API key and model in settings to enable replies.";
    public const string ModelStreamCode = "modelStreamFailed";

    public async IAsyncEnumerable<ConversationStreamEvent> StreamAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        [EnumeratorCancellation] CancellationToken ct = default,
        RoutingMissContext? missContext = null)
    {
        await foreach (var streamEvent in loop.RunAsync(
            conversation, history, userText, ct, AgentRole.Worker, missContext))
        {
            yield return streamEvent;
        }
    }
}
