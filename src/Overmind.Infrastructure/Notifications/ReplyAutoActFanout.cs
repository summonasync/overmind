using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Notifications;

public sealed class ReplyAutoActFanout(
    OvermindDbContext db,
    ReplyAutoActQueue queue,
    ILogger<ReplyAutoActFanout> logger)
{
    public async Task TryDispatchAsync(AgentNotification notification, CancellationToken ct)
    {
        try
        {
            await DispatchAsync(notification, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex, "auto-act fan-out failed for reply {NotificationId}", notification.Id);
        }
    }

    private async Task DispatchAsync(AgentNotification notification, CancellationToken ct)
    {
        if (notification.Kind != NotificationKind.Approval
            || notification.FeatureSlug is null
            || notification.ProjectId is null)
        {
            return;
        }

        var autoActEnabled = await db.Projects
            .AsNoTracking()
            .Where(p => p.Id == notification.ProjectId)
            .Select(p => p.AutoActOnReplies)
            .FirstOrDefaultAsync(ct);

        if (!autoActEnabled)
        {
            return;
        }

        if (queue.TryEnqueue(notification.Id))
        {
            return;
        }

        logger.LogError(
            "auto-act queue is full; reply {NotificationId} stays unprocessed — the reply row and the wake channel remain the recovery path",
            notification.Id);
    }
}
