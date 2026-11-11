using Microsoft.Extensions.Logging;
using Overmind.Infrastructure.Notifications;
using Telegram.Bot;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramNotificationCallbackHandler(
    TelegramTransportOptionsProvider optionsProvider,
    TelegramBotClientCache clientCache,
    TelegramNotificationDeliveryStore deliveries,
    NotificationsService notifications,
    ILogger<TelegramNotificationCallbackHandler> logger) : ITelegramCallbackHandler
{
    public const int AnswerTextLimit = 200;
    public const string InvalidButtonAnswer = "This button is no longer valid.";
    public const string NotAllowedAnswer = "Not allowed.";
    public const string AlreadyDecidedAnswer = "Already decided.";
    public const string AlreadyDecidedPrefix = "Already decided: ";
    public const string RecordedAnswer = "Decision recorded.";
    public const string FailureAnswer = "The decision could not be recorded; try again.";

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
                "Telegram callback {CallbackQueryId} from chat {ChatId} failed; answering with the failure notice",
                callback.CallbackQueryId, callback.ChatId);
            await AnswerAsync(callback, FailureAnswer, ct);
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
            logger.LogDebug("Telegram callback from chat {ChatId} is not allowlisted; denied", callback.ChatId);
            await AnswerAsync(callback, NotAllowedAnswer, ct);
            return;
        }

        if (!NotificationCallbackData.TryParse(callback.Data, out var data))
        {
            await AnswerAsync(callback, InvalidButtonAnswer, ct);
            return;
        }

        var notification = await deliveries.FindNotificationAsync(callback.ChatId, callback.MessageId, ct);
        if (notification is null || notification.Id != data.NotificationId)
        {
            await AnswerAsync(callback, InvalidButtonAnswer, ct);
            return;
        }

        if (notification.RepliedAt is not null)
        {
            await AnswerAsync(callback, AlreadyDecidedText(notification.ReplyText), ct);
            await SwapToDecidedMarkupAsync(options, callback, data.NotificationId, notification.ReplyText, ct);
            return;
        }

        var decision = data.Decision == NotificationDecision.Approve ? "approved" : "declined";
        await notifications.ReplyAsync(notification.Id, $"{decision} via telegram button by {callback.From}", ct);
        await AnswerAsync(callback, RecordedAnswer, ct);
        await EditDecisionLineAsync(options, callback, decision, ct);
    }

    private static string AlreadyDecidedText(string? replyText)
    {
        if (string.IsNullOrWhiteSpace(replyText))
        {
            return AlreadyDecidedAnswer;
        }

        var text = $"{AlreadyDecidedPrefix}{replyText.Trim()}";
        return text.Length <= AnswerTextLimit
            ? text
            : string.Concat(text.AsSpan(0, AnswerTextLimit - 1), "…");
    }

    private async Task SwapToDecidedMarkupAsync(
        TelegramTransportOptions options,
        TelegramCallback callback,
        Guid notificationId,
        string? replyText,
        CancellationToken ct)
    {
        var markup = NotificationKeyboardMarkup.Decided(notificationId, replyText);
        try
        {
            await clientCache.GetOrCreate(options).Client.EditMessageReplyMarkup(
                callback.ChatId, callback.MessageId, markup, cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Swapping telegram message {MessageId} in chat {ChatId} to the decided markup failed; the decision stays recorded",
                callback.MessageId, callback.ChatId);
        }
    }

    private async Task EditDecisionLineAsync(
        TelegramTransportOptions options, TelegramCallback callback, string decision, CancellationToken ct)
    {
        var marker = decision == "approved" ? "✅" : "❌";
        var line = $"{marker} {decision} by {callback.From}";
        var text = string.IsNullOrEmpty(callback.MessageText) ? line : $"{callback.MessageText}\n\n{line}";
        try
        {
            await clientCache.GetOrCreate(options).Client.EditMessageText(
                callback.ChatId, callback.MessageId, text, cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Editing telegram message {MessageId} in chat {ChatId} with the decision line failed; the decision stays recorded",
                callback.MessageId, callback.ChatId);
        }
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
            logger.LogWarning(ex, "Answering telegram callback {CallbackQueryId} failed", callback.CallbackQueryId);
        }
    }
}
