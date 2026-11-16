namespace Overmind.Infrastructure.Telegram;

public interface ITelegramWaveCommandService
{
    Task HandleAsync(TelegramWaveCommand command, TelegramIncomingMessage message, CancellationToken ct);
}
