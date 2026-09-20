using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Notifications;

public sealed class NotificationsService(OvermindDbContext db)
{
    private const int ListCapacity = 50;

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

        return notification;
    }

    public async Task<NotificationsOverview> GetOverviewAsync(CancellationToken ct)
    {
        var items = await db.AgentNotifications
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .Take(ListCapacity)
            .ToListAsync(ct);

        var unreadCount = await db.AgentNotifications
            .AsNoTracking()
            .CountAsync(n => n.ReadAt == null, ct);

        return new NotificationsOverview(items, unreadCount);
    }

    public async Task<IReadOnlyList<AgentNotification>> ListRepliesAsync(
        Guid? projectId, bool includeRead, CancellationToken ct)
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

    public async Task<AgentNotification?> ReplyAsync(Guid id, string text, CancellationToken ct)
    {
        var notification = await db.AgentNotifications.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (notification is null)
        {
            return null;
        }

        notification.Reply(text);
        await db.SaveChangesAsync(ct);

        return notification;
    }
}
