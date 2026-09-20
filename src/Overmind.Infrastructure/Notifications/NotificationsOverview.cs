using Overmind.Core;

namespace Overmind.Infrastructure.Notifications;

public sealed record NotificationsOverview(
    IReadOnlyList<AgentNotification> Items,
    int UnreadCount);
