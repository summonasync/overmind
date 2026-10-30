namespace Overmind.Infrastructure.Notifications;

public interface IReplyAutoActProcessor
{
    Task<ReplyAutoActResult> ProcessAsync(Guid notificationId, CancellationToken ct);
}
