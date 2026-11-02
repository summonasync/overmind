namespace Overmind.Core;

public interface IConversationStreamSource
{
    IAsyncEnumerable<ConversationStreamEvent> StreamAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        CancellationToken ct);
}
