using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wake;

namespace Overmind.Infrastructure.Notifications;

public sealed class NotificationsService(
    OvermindDbContext db,
    ReplyAutoActFanout autoAct,
    WakeDropFanout wakeDrop,
    IEnumerable<INotificationDeliveryChannel> deliveryChannels)
{
    public const int ListCapacity = 50;

    public async Task<AgentNotification?> CreateAsync(
        NotificationKind kind,
        string text,
        Guid? projectId,
        string? featureSlug,
        string? tab,
        string? agent,
        CancellationToken ct)
    {
        if (projectId is { } scoped && !await db.Projects.AnyAsync(p => p.Id == scoped, ct))
        {
            return null;
        }

        var notification = AgentNotification.Create(kind, text, projectId, featureSlug, tab, agent);
        db.AgentNotifications.Add(notification);
        await db.SaveChangesAsync(ct);

        foreach (var channel in deliveryChannels)
        {
            _ = channel.TryDeliverAsync(notification, CancellationToken.None);
        }

        return notification;
    }

    public async Task<NotificationsOverview> GetOverviewAsync(
        bool includeHidden, bool awaiting, int take, CancellationToken ct)
    {
        var notifications = db.AgentNotifications.AsNoTracking();
        if (awaiting)
        {
            notifications = notifications.Where(n =>
                n.ReplyText == null
                && n.HiddenAt == null
                && (n.Kind == NotificationKind.Approval || n.Kind == NotificationKind.Question));
        }
        else if (!includeHidden)
        {
            notifications = notifications.Where(n => n.HiddenAt == null);
        }

        var items = await notifications
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        var unreadCount = await db.AgentNotifications
            .AsNoTracking()
            .CountAsync(n => n.HiddenAt == null && n.ReadAt == null, ct);

        return new NotificationsOverview(items, unreadCount);
    }

    public async Task<IReadOnlyList<AgentNotification>> ListRepliesAsync(
        Guid? projectId,
        bool includeRead,
        DateTime? since,
        NotificationKind? kind,
        string? featureSlug,
        CancellationToken ct)
    {
        var replied = db.AgentNotifications
            .AsNoTracking()
            .Where(n => n.ReplyText != null);

        if (!includeRead)
        {
            replied = replied.Where(n => n.ReadAt == null);
        }

        if (projectId is { } scoped)
        {
            replied = replied.Where(n => n.ProjectId == scoped);
        }

        if (since is { } after)
        {
            replied = replied.Where(n => n.RepliedAt > after);
        }

        if (kind is { } wanted)
        {
            replied = replied.Where(n => n.Kind == wanted);
        }

        if (featureSlug is { } slug)
        {
            replied = replied.Where(n => n.FeatureSlug == slug);
        }

        return await replied
            .OrderByDescending(n => n.RepliedAt)
            .Take(ListCapacity)
            .ToListAsync(ct);
    }

    public async Task<AgentNotification?> MarkReadAsync(Guid id, CancellationToken ct)
    {
        var notification = await db.AgentNotifications.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (notification is null)
        {
            return null;
        }

        notification.MarkRead();
        await db.SaveChangesAsync(ct);

        return notification;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var deleted = await db.AgentNotifications
            .Where(n => n.Id == id)
            .ExecuteDeleteAsync(ct);

        return deleted > 0;
    }

    public async Task<int> MarkAllReadAsync(CancellationToken ct)
    {
        return await db.AgentNotifications
            .Where(n => n.ReadAt == null)
            .ExecuteUpdateAsync(
                set => set.SetProperty(n => n.ReadAt, DateTime.UtcNow),
                ct);
    }

    public async Task<int> ClearReadAsync(CancellationToken ct)
    {
        return await db.AgentNotifications
            .Where(n => n.ReadAt != null && n.ReplyText == null)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<int> ClearConversationsAsync(CancellationToken ct)
    {
        return await db.AgentNotifications
            .Where(n => n.ReplyText != null && n.HiddenAt == null)
            .ExecuteUpdateAsync(
                set => set.SetProperty(n => n.HiddenAt, DateTime.UtcNow),
                ct);
    }

    public async Task<AgentNotification?> ReplyAsync(Guid id, string text, CancellationToken ct)
    {
        var notification = await db.AgentNotifications.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (notification is null)
        {
            return null;
        }

        notification.Reply(text);
        await db.SaveChangesAsync(ct);
        await autoAct.TryDispatchAsync(notification, ct);
        _ = wakeDrop.WakeAsync(notification.Agent);

        return notification;
    }
}
