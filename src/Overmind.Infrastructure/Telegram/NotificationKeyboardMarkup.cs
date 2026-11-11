using Telegram.Bot.Types.ReplyMarkups;

namespace Overmind.Infrastructure.Telegram;

public static class NotificationKeyboardMarkup
{
    private const string ApproveAction = "approve";
    private const string DeclineAction = "decline";
    private const string ApprovedOutcome = "approved";
    private const string DeclinedOutcome = "declined";

    public static InlineKeyboardMarkup Pending(Guid notificationId) => new(
        InlineKeyboardButton.WithCallbackData("✅ Approve", ActionData(notificationId, ApproveAction)),
        InlineKeyboardButton.WithCallbackData("❌ Decline", ActionData(notificationId, DeclineAction)));

    public static InlineKeyboardMarkup Decided(Guid notificationId, string? replyText)
    {
        var outcome = OutcomeOf(replyText);
        var label = outcome is null ? "already decided" : $"already decided: {outcome}";
        return new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData(label, ActionData(notificationId, ActionOf(outcome))));
    }

    private static string? OutcomeOf(string? replyText)
    {
        if (string.IsNullOrWhiteSpace(replyText))
        {
            return null;
        }

        var trimmed = replyText.Trim();
        if (trimmed.StartsWith(ApprovedOutcome, StringComparison.OrdinalIgnoreCase))
        {
            return ApprovedOutcome;
        }

        return trimmed.StartsWith(DeclinedOutcome, StringComparison.OrdinalIgnoreCase) ? DeclinedOutcome : null;
    }

    private static string ActionOf(string? outcome) =>
        outcome == DeclinedOutcome ? DeclineAction : ApproveAction;

    private static string ActionData(Guid notificationId, string action) =>
        $"{NotificationCallbackData.Prefix}:{notificationId:D}:{action}";
}
