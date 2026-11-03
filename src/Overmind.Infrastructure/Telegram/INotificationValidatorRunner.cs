using Overmind.Core;

namespace Overmind.Infrastructure.Telegram;

public interface INotificationValidatorRunner
{
    Task RunAsync(long chatId, AgentNotification notification, string ownerReply, CancellationToken ct);
}
