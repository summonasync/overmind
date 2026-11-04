namespace Overmind.Infrastructure.Telegram;

public interface ITelegramCallbackHandler
{
    Task HandleAsync(TelegramCallback callback, CancellationToken ct);
}
