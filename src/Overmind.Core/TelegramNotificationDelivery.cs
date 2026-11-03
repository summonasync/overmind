namespace Overmind.Core;

public sealed class TelegramNotificationDelivery
{
    private TelegramNotificationDelivery()
    {
    }

    public long ChatId { get; private set; }
    public long MessageId { get; private set; }
    public Guid NotificationId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static TelegramNotificationDelivery Create(long chatId, long messageId, Guid notificationId)
    {
        return new TelegramNotificationDelivery
        {
            ChatId = chatId,
            MessageId = messageId,
            NotificationId = notificationId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
