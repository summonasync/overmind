using System.Text;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Notifications;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramAgentChatHandler(
    TelegramChatStore chats,
    ConversationMessageStore messages,
    IConversationStreamSource streamSource,
    ConversationTurnGate turns,
    TelegramReplySender sender,
    TelegramNotificationDeliveryStore deliveries,
    NotificationsService notifications,
    INotificationValidatorRunner validator,
    ITelegramWaveCommandService waveCommands,
    ILogger<TelegramAgentChatHandler> logger) : ITelegramMessageHandler
{
    public const int MaxAttempts = 3;
    public const string BusyReplyText =
        "Another turn is still running in this conversation; send the message again once it finishes.";
    public const string ReplyRegisteredAck = "Reply registered.";
    public const string ReplyFailedNotice =
        "The reply could not be registered after 3 attempts; the failure was logged. Try again later.";

    public async Task HandleAsync(TelegramIncomingMessage message, CancellationToken ct)
    {
        if (message.RepliedToMessageId is { } repliedTo)
        {
            var notificationId = await deliveries.FindNotificationIdAsync(message.ChatId, repliedTo, ct);
            if (notificationId is { } id)
            {
                await RegisterNotificationReplyAsync(id, message, ct);
                return;
            }
        }

        if (TelegramWaveCommandParser.TryMatch(message.Text, out var command))
        {
            await waveCommands.HandleAsync(command, message, ct);
            return;
        }

        var conversation = await chats.GetOrCreateConversationAsync(message.ChatId, ct);
        if (!turns.TryEnter(conversation.Id))
        {
            await SendNoticeQuietlyAsync(message.ChatId, BusyReplyText, ct);
            return;
        }

        try
        {
            await RunTurnAsync(conversation, message, ct);
        }
        finally
        {
            turns.Exit(conversation.Id);
        }
    }

    private async Task RegisterNotificationReplyAsync(
        Guid notificationId, TelegramIncomingMessage message, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var notification = await notifications.ReplyAsync(notificationId, message.Text, ct);
                await SendNoticeQuietlyAsync(message.ChatId, ReplyRegisteredAck, ct);
                if (notification is not null)
                {
                    await validator.RunAsync(message.ChatId, notification, message.Text, ct);
                }

                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= MaxAttempts)
                {
                    logger.LogError(
                        ex,
                        "Telegram reply from chat {ChatId} dead-lettered: registering the reply to notification {NotificationId} failed {Attempts} times",
                        message.ChatId, notificationId, attempt);
                    await SendNoticeQuietlyAsync(message.ChatId, ReplyFailedNotice, ct);
                    return;
                }

                logger.LogWarning(
                    ex,
                    "Registering the telegram reply to notification {NotificationId} failed on attempt {Attempt}; retrying",
                    notificationId, attempt);
            }
        }
    }

    private async Task RunTurnAsync(
        Conversation conversation, TelegramIncomingMessage message, CancellationToken ct)
    {
        IReadOnlyList<ConversationMessage>? history = null;
        var userAppended = false;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                history ??= await messages.ListPageAsync(conversation.Id, ct: ct);
                if (!userAppended)
                {
                    await messages.AppendAsync(conversation.Id, ConversationMessage.UserRole, message.Text, ct);
                    userAppended = true;
                }

                var outcome = await CollectReplyAsync(conversation, history, message.Text, ct);
                if (outcome.Refusal is { } refusal)
                {
                    await sender.SendAsync(message.ChatId, TelegramReplySplitter.Split(refusal), ct: ct);
                    return;
                }

                await DeliverReplyAsync(conversation.Id, message.ChatId, outcome.Reply, ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (TelegramReplyDeliveryException ex) when (ex.DeliveredChunks > 0)
            {
                logger.LogError(
                    ex,
                    "Telegram reply to chat {ChatId} was partially delivered ({DeliveredChunks} chunks); the turn of conversation {ConversationId} is not retried",
                    message.ChatId, ex.DeliveredChunks, conversation.Id);
                return;
            }
            catch (Exception ex)
            {
                if (attempt >= MaxAttempts)
                {
                    logger.LogError(
                        ex,
                        "Telegram message from chat {ChatId} dead-lettered: the turn of conversation {ConversationId} failed {Attempts} times",
                        message.ChatId, conversation.Id, attempt);
                    await SendNoticeQuietlyAsync(
                        message.ChatId,
                        $"The agent turn failed {MaxAttempts} times in a row; the failure was logged. Try again later.",
                        ct);
                    return;
                }

                logger.LogWarning(
                    ex,
                    "Telegram turn attempt {Attempt} for chat {ChatId} failed; retrying",
                    attempt, message.ChatId);
            }
        }
    }

    private async Task<TurnOutcome> CollectReplyAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        CancellationToken ct)
    {
        var reply = new StringBuilder();
        string? refusal = null;
        string? trace = null;
        await foreach (var streamEvent in streamSource.StreamAsync(conversation, history, userText, ct))
        {
            switch (streamEvent.Kind)
            {
                case ConversationStreamEventKind.Delta:
                    reply.Append(streamEvent.Text);
                    break;
                case ConversationStreamEventKind.Routed:
                    trace ??= RoutingTrace(streamEvent);
                    break;
                case ConversationStreamEventKind.Failed:
                    refusal = streamEvent.Text;
                    break;
            }
        }

        if (trace is not null && reply.Length > 0)
        {
            reply.Insert(0, trace + "\n\n");
        }

        return new TurnOutcome(reply.ToString(), refusal);
    }

    private static string RoutingTrace(ConversationStreamEvent streamEvent) =>
        streamEvent.RouteSource == RoutingSource.Learned
            ? $"→ {streamEvent.RouteTarget} · learned"
            : $"→ {streamEvent.RouteTarget}";

    private async Task DeliverReplyAsync(Guid conversationId, long chatId, string reply, CancellationToken ct)
    {
        if (reply.Length == 0)
        {
            return;
        }

        var plain = TelegramMarkdownStripper.Strip(reply);
        if (plain.Length == 0)
        {
            return;
        }

        try
        {
            await sender.SendAsync(chatId, TelegramReplySplitter.Split(plain), ct: ct);
        }
        catch (TelegramReplyDeliveryException ex) when (ex.DeliveredChunks > 0)
        {
            await messages.AppendAsync(conversationId, ConversationMessage.AssistantRole, reply, ct);
            throw;
        }

        await messages.AppendAsync(conversationId, ConversationMessage.AssistantRole, reply, ct);
    }

    private async Task SendNoticeQuietlyAsync(long chatId, string text, CancellationToken ct)
    {
        try
        {
            await sender.SendAsync(chatId, TelegramReplySplitter.Split(text), ct: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Telegram notice to chat {ChatId} could not be delivered", chatId);
        }
    }

    private sealed record TurnOutcome(string Reply, string? Refusal);
}
