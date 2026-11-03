using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Agent;

namespace Overmind.Infrastructure.Telegram;

public sealed class NotificationValidatorRunner(
    TelegramValidatorConversationStore conversations,
    ConversationMessageStore messages,
    [FromKeyedServices(NotificationValidatorSessionRegistrar.Alias)] AgentLoop loop,
    ConversationTurnGate turns,
    TelegramReplySender sender,
    ILogger<NotificationValidatorRunner> logger) : INotificationValidatorRunner
{
    public async Task RunAsync(
        long chatId, AgentNotification notification, string ownerReply, CancellationToken ct)
    {
        try
        {
            var conversation = await conversations.GetOrCreateConversationAsync(chatId, notification.ProjectId, ct);
            if (!turns.TryEnter(conversation.Id))
            {
                logger.LogWarning(
                    "Validator turn for conversation {ConversationId} is still running; the instruction for notification {NotificationId} was not enqueued",
                    conversation.Id, notification.Id);
                return;
            }

            try
            {
                await ValidateAsync(conversation, chatId, notification, ownerReply, ct);
            }
            finally
            {
                turns.Exit(conversation.Id);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Validator turn for notification {NotificationId} failed; the reply proof stays recorded",
                notification.Id);
        }
    }

    private async Task ValidateAsync(
        Conversation conversation,
        long chatId,
        AgentNotification notification,
        string ownerReply,
        CancellationToken ct)
    {
        var instruction = ComposeInstruction(notification, ownerReply);
        await messages.AppendAsync(conversation.Id, ConversationMessage.UserRole, instruction, ct);
        var history = await messages.ListPageAsync(conversation.Id, ct: ct);

        var reply = new StringBuilder();
        await foreach (var frame in loop.RunAsync(conversation, history, instruction, ct, AgentRole.Validator))
        {
            if (frame.Kind == ConversationStreamEventKind.Delta)
            {
                reply.Append(frame.Text);
            }
        }

        var outcome = reply.ToString().Trim();
        if (outcome.Length == 0)
        {
            logger.LogWarning(
                "Validator turn for notification {NotificationId} produced no outcome", notification.Id);
            return;
        }

        await messages.AppendAsync(conversation.Id, ConversationMessage.AssistantRole, outcome, ct);
        try
        {
            await sender.SendAsync(
                chatId, TelegramReplySplitter.Split(TelegramMarkdownStripper.Strip(outcome)), ct: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Validator outcome for chat {ChatId} was not delivered", chatId);
        }
    }

    private static string ComposeInstruction(AgentNotification notification, string ownerReply)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Notification {notification.Id} ({notification.Kind}).");
        if (notification.ProjectId is { } projectId)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Project: {projectId}.");
        }

        if (!string.IsNullOrWhiteSpace(notification.FeatureSlug))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Feature: {notification.FeatureSlug}.");
        }

        if (!string.IsNullOrWhiteSpace(notification.Tab))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Gate: {notification.Tab}.");
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"Notification text: {notification.Text}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Owner reply: {ownerReply.Trim()}");
        return builder.ToString().TrimEnd();
    }
}
