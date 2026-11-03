namespace Overmind.Core;

public sealed class TelegramChatMapping
{
    private TelegramChatMapping()
    {
    }

    public long ChatId { get; private set; }
    public Guid ConversationId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static TelegramChatMapping Create(long chatId, Guid conversationId)
    {
        return new TelegramChatMapping
        {
            ChatId = chatId,
            ConversationId = conversationId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
