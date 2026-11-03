using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramNotificationDeliveryStore(OvermindDbContext db)
{
    public async Task SaveAsync(long chatId, long messageId, Guid notificationId, CancellationToken ct = default)
    {
        db.TelegramNotificationDeliveries.Add(
            TelegramNotificationDelivery.Create(chatId, messageId, notificationId));
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid?> FindNotificationIdAsync(long chatId, long messageId, CancellationToken ct = default) =>
        await db.TelegramNotificationDeliveries
            .AsNoTracking()
            .Where(d => d.ChatId == chatId && d.MessageId == messageId)
            .Select(d => (Guid?)d.NotificationId)
            .FirstOrDefaultAsync(ct);

    public async Task<AgentNotification?> FindNotificationAsync(long chatId, long messageId, CancellationToken ct = default)
    {
        var notification = await db.TelegramNotificationDeliveries
            .AsNoTracking()
            .Where(d => d.ChatId == chatId && d.MessageId == messageId)
            .Join(
                db.AgentNotifications.AsNoTracking(),
                delivery => delivery.NotificationId,
                notification => notification.Id,
                (_, notification) => notification)
            .FirstOrDefaultAsync(ct);
        return notification;
    }
}
