using System.Runtime.CompilerServices;
using Overmind.Core;
using Overmind.Infrastructure.Llm;

namespace Overmind.Infrastructure.Agent;

public sealed class PlaceholderConversationStreamSource : IConversationStreamSource
{
    public const string LoopPendingNotice = "The agent loop is not wired yet; your message is stored in this conversation.";
    public const string ProviderReadyNotice = "Replies will stream here once the loop lands.";
    public const string ProviderMissingNotice = "No LLM provider configured — set the agent base URL, API key and model in settings to enable replies.";

    private readonly ILlmChatClientFactory _chatFactory;

    public PlaceholderConversationStreamSource(ILlmChatClientFactory chatFactory)
    {
        _chatFactory = chatFactory;
    }

    public async IAsyncEnumerable<ConversationStreamEvent> StreamAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return ConversationStreamEvent.Delta(LoopPendingNotice);
        yield return ConversationStreamEvent.Delta(
            await _chatFactory.IsConfiguredAsync(ct) ? ProviderReadyNotice : ProviderMissingNotice);
        yield return ConversationStreamEvent.Completed();
    }
}
