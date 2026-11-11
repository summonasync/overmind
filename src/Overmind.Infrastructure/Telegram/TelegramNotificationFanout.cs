using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramNotificationFanout(
    IServiceScopeFactory scopes,
    Func<Uri?> origin,
    ILogger<TelegramNotificationFanout> logger) : INotificationDeliveryChannel
{
    public async Task TryDeliverAsync(AgentNotification notification, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var provider = scope.ServiceProvider;

            var options = await provider.GetRequiredService<TelegramTransportOptionsProvider>().GetAsync(ct);
            if (!options.IsRunnable || options.AllowedChatIds.Count == 0)
            {
                return;
            }

            var link = NotificationLinks.Dashboard(origin(), notification.ProjectId, notification.FeatureSlug, notification.Tab);
            var sender = provider.GetRequiredService<TelegramReplySender>();
            var deliveries = provider.GetRequiredService<TelegramNotificationDeliveryStore>();
            var projectName = await ProjectNameAsync(provider, notification.ProjectId, ct);
            var (text, entities) = Format(notification, link, projectName);
            var keyboard = KeyboardFor(notification);

            foreach (var chatId in options.AllowedChatIds)
            {
                try
                {
                    foreach (var messageId in await sender.SendAsync(chatId, [text], keyboard, entities, ct))
                    {
                        await deliveries.SaveAsync(chatId, messageId, notification.Id, ct);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Telegram delivery to chat {ChatId} failed for notification {NotificationId}; remaining chats continue",
                        chatId, notification.Id);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Telegram delivery failed for notification {NotificationId}", notification.Id);
        }
    }

    private static async Task<string?> ProjectNameAsync(
        IServiceProvider provider, Guid? projectId, CancellationToken ct)
    {
        if (projectId is not { } id)
        {
            return null;
        }

        var db = provider.GetRequiredService<OvermindDbContext>();
        return await db.Projects
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct);
    }

    private static (string Text, IReadOnlyList<MessageEntity>? Entities) Format(
        AgentNotification notification, string? link, string? projectName)
    {
        var text = $"[{notification.Kind.ToString().ToLowerInvariant()}] {notification.Text}";
        var body = link is null ? text : $"{text}\n\n{link}";
        if (projectName is not { } name)
        {
            return (body, null);
        }

        return (
            $"{name}\n\n{body}",
            [new MessageEntity { Type = MessageEntityType.Bold, Offset = 0, Length = name.Length }]);
    }

    private static InlineKeyboardMarkup? KeyboardFor(AgentNotification notification)
    {
        if (notification.Kind is not (NotificationKind.Approval or NotificationKind.Question))
        {
            return null;
        }

        return NotificationKeyboardMarkup.Pending(notification.Id);
    }
}
