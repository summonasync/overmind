using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramWaveCallbackHandler(
    TelegramTransportOptionsProvider optionsProvider,
    TelegramBotClientCache clientCache,
    ILogger<TelegramWaveCallbackHandler> logger) : ITelegramCallbackHandler
{
    public const string NotWiredYetAnswer = "The flow behind this button is not wired yet.";

    public async Task HandleAsync(TelegramCallback callback, CancellationToken ct)
    {
        try
        {
            await ProcessAsync(callback, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Telegram wave callback {CallbackQueryId} from chat {ChatId} failed; answering with the failure notice",
                callback.CallbackQueryId, callback.ChatId);
            await AnswerAsync(callback, TelegramNotificationCallbackHandler.FailureAnswer, ct);
        }
    }

    private async Task ProcessAsync(TelegramCallback callback, CancellationToken ct)
    {
        var options = await optionsProvider.GetAsync(ct);
        if (!options.IsRunnable)
        {
            return;
        }

        if (!options.AllowedChatIds.Contains(callback.ChatId))
        {
            logger.LogDebug(
                "Telegram wave callback from chat {ChatId} is not allowlisted; denied", callback.ChatId);
            await AnswerAsync(callback, TelegramNotificationCallbackHandler.NotAllowedAnswer, ct);
            return;
        }

        if (!TelegramWaveCallbackData.TryParse(callback.Data, out var data))
        {
            await AnswerAsync(callback, TelegramNotificationCallbackHandler.InvalidButtonAnswer, ct);
            return;
        }

        logger.LogInformation(
            "Telegram wave callback {Action} from chat {ChatId} arrived before its flow was wired",
            data.Action, callback.ChatId);
        await AnswerAsync(callback, NotWiredYetAnswer, ct);
    }

    private async Task AnswerAsync(TelegramCallback callback, string text, CancellationToken ct)
    {
        try
        {
            var options = await optionsProvider.GetAsync(ct);
            if (!options.IsRunnable)
            {
                return;
            }

            await clientCache.GetOrCreate(options)
                .Client.AnswerCallbackQuery(callback.CallbackQueryId, text, cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Answering telegram wave callback {CallbackQueryId} failed", callback.CallbackQueryId);
        }
    }
}
