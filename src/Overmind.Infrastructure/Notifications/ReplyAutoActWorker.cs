using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Overmind.Infrastructure.Notifications;

public sealed class ReplyAutoActWorker(
    ReplyAutoActQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ReplyAutoActWorker> logger)
{
    private const int MaxAttempts = 3;

    private readonly Dictionary<Guid, int> _attempts = [];

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<Guid> interrupted = [];
        try
        {
            await foreach (var notificationId in queue.DequeueAllAsync(stoppingToken))
            {
                if (!await ProcessAsync(notificationId, stoppingToken))
                {
                    interrupted.Add(notificationId);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        foreach (var notificationId in interrupted)
        {
            await ProcessAsync(notificationId, CancellationToken.None);
        }

        while (queue.TryDequeue(out var pending))
        {
            await ProcessAsync(pending, CancellationToken.None);
        }
    }

    private async Task<bool> ProcessAsync(Guid notificationId, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<IReplyAutoActProcessor>();
            var result = await processor.ProcessAsync(notificationId, ct);
            if (!result.Acted)
            {
                logger.LogInformation(
                    "auto-act skipped reply {NotificationId}: {Reason}", notificationId, result.Reason);
            }

            _attempts.Remove(notificationId);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            var attempts = _attempts.GetValueOrDefault(notificationId) + 1;
            if (attempts >= MaxAttempts)
            {
                _attempts.Remove(notificationId);
                logger.LogError(
                    ex, "auto-act gave up on reply {NotificationId} after {Attempts} attempts",
                    notificationId, attempts);
                return true;
            }

            if (!queue.TryEnqueue(notificationId))
            {
                _attempts.Remove(notificationId);
                logger.LogError(
                    ex,
                    "auto-act queue is full; reply {NotificationId} dropped after {Attempts} failed attempts — nothing is persisted: replying to the notification again re-stamps RepliedAt and re-queues it, and the in-memory queue does not survive a restart",
                    notificationId, attempts);
                return true;
            }

            _attempts[notificationId] = attempts;
            logger.LogWarning(
                ex, "auto-act attempt {Attempts} for reply {NotificationId} failed — requeued",
                attempts, notificationId);
            return true;
        }
    }
}
