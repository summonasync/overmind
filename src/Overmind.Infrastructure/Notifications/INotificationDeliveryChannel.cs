using Overmind.Core;

namespace Overmind.Infrastructure.Notifications;

public interface INotificationDeliveryChannel
{
    Task TryDeliverAsync(AgentNotification notification, CancellationToken ct);
}
