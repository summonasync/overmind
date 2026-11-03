namespace Overmind.Core;

public sealed class TelegramValidatorMapping
{
    private TelegramValidatorMapping()
    {
    }

    public Guid Id { get; private set; }
    public long ChatId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public Guid ConversationId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static TelegramValidatorMapping Create(long chatId, Guid? projectId, Guid conversationId)
    {
        return new TelegramValidatorMapping
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            ProjectId = projectId,
            ConversationId = conversationId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
