using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramWaveCallbackHandler(
    TelegramTransportOptionsProvider optionsProvider,
    TelegramBotClientCache clientCache,
    ITelegramWaveCommandService commands,
    ITelegramWaveSnapshotService snapshot,
    ILogger<TelegramWaveCallbackHandler> logger) : ITelegramCallbackHandler
{
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
            logger.LogInformation(
                "Telegram wave callback from chat {ChatId} is not allowlisted; denied", callback.ChatId);
            await AnswerAsync(callback, TelegramNotificationCallbackHandler.NotAllowedAnswer, ct);
            return;
        }

        if (!TelegramWaveCallbackData.TryParse(callback.Data, out var data))
        {
            await AnswerAsync(callback, TelegramNotificationCallbackHandler.InvalidButtonAnswer, ct);
            return;
        }

        var answer = data.Action switch
        {
            WaveCallbackAction.Project when data.ProjectId is { } projectId
                => await commands.PickProjectAsync(callback.ChatId, projectId, ct),
            WaveCallbackAction.Room when data.DraftToken is { } token && data.RoomIndex is { } roomIndex
                => await commands.PickRoomAsync(callback.ChatId, token, roomIndex, ct),
            WaveCallbackAction.SnapshotProject when data.ProjectId is { } snapshotProjectId
                => await snapshot.PickProjectAsync(callback.ChatId, snapshotProjectId, ct),
            WaveCallbackAction.SnapshotCancel => ITelegramWaveSnapshotService.DismissAnswer,
            WaveCallbackAction.Cancel => commands.Cancel(callback.ChatId),
            _ => TelegramNotificationCallbackHandler.InvalidButtonAnswer
        };

        await AnswerAsync(callback, answer, ct);
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
                .Client.AnswerCallbackQuery(
                    callback.CallbackQueryId, WithinAnswerBudget(text), cancellationToken: ct);
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

    private static string WithinAnswerBudget(string text) =>
        text.Length <= TelegramNotificationCallbackHandler.AnswerTextLimit
            ? text
            : string.Concat(
                text.AsSpan(0, TelegramNotificationCallbackHandler.AnswerTextLimit - 1), "…");
}
