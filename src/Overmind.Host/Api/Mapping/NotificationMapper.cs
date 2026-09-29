using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Mapping;

public static class NotificationMapper
{
    public static NotificationResponse ToResponse(AgentNotification notification)
    {
        return new NotificationResponse(
            notification.Id,
            notification.ProjectId,
            notification.Kind,
            notification.FeatureSlug,
            notification.Tab,
            notification.Text,
            notification.Agent,
            notification.CreatedAt,
            notification.ReadAt,
            notification.ReplyText,
            notification.RepliedAt);
    }

    public static AgentNotificationResponse ToAgentResponse(AgentNotification notification, string? url)
    {
        return new AgentNotificationResponse(
            notification.Id,
            notification.ProjectId,
            notification.Kind,
            notification.FeatureSlug,
            notification.Tab,
            notification.Text,
            notification.Agent,
            notification.CreatedAt,
            notification.ReadAt,
            notification.ReplyText,
            notification.RepliedAt,
            url);
    }
}
