namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramCallbackRouter(
    TelegramNotificationCallbackHandler notifications,
    TelegramWaveCallbackHandler waves) : ITelegramCallbackHandler
{
    public Task HandleAsync(TelegramCallback callback, CancellationToken ct) =>
        TelegramWaveCallbackData.Owns(callback.Data)
            ? waves.HandleAsync(callback, ct)
            : notifications.HandleAsync(callback, ct);
}
