using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Notifications;

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
            var text = Format(notification, link);

            foreach (var chatId in options.AllowedChatIds)
            {
                foreach (var messageId in await sender.SendAsync(chatId, [text], ct))
                {
                    await deliveries.SaveAsync(chatId, messageId, notification.Id, ct);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Telegram delivery failed for notification {NotificationId}", notification.Id);
        }
    }

    private static string Format(AgentNotification notification, string? link)
    {
        var text = $"[{notification.Kind.ToString().ToLowerInvariant()}] {notification.Text}";
        return link is null ? text : $"{text}\n\n{link}";
    }
}
