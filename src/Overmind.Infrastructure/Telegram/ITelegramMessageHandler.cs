namespace Overmind.Infrastructure.Telegram;

public interface ITelegramMessageHandler
{
    Task HandleAsync(TelegramIncomingMessage message, CancellationToken ct);
}
