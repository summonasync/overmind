using System.Text.Json.Serialization;
using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record CreateNotificationRequest(
    string Kind,
    string Text,
    Guid? ProjectId = null,
    string? FeatureSlug = null,
    string? Tab = null,
    string? From = null);

public sealed record ReplyNotificationRequest(string Text);

public sealed record AgentNotificationResponse(
    Guid Id,
    Guid? ProjectId,
    NotificationKind Kind,
    string? FeatureSlug,
    string? Tab,
    string Text,
    string Agent,
    DateTime CreatedAt,
    DateTime? ReadAt,
    string? ReplyText,
    DateTime? RepliedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Url);

public sealed record NotificationResponse(
    Guid Id,
    Guid? ProjectId,
    NotificationKind Kind,
    string? FeatureSlug,
    string? Tab,
    string Text,
    string Agent,
    DateTime CreatedAt,
    DateTime? ReadAt,
    string? ReplyText,
    DateTime? RepliedAt);

public sealed record NotificationsResponse(NotificationResponse[] Items, int UnreadCount);

public sealed record RepliesResponse(NotificationResponse[] Items);

public sealed record ReadAllNotificationsResponse(int Marked);

public sealed record ClearReadNotificationsResponse(int Deleted);
