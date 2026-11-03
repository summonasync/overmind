namespace Overmind.Infrastructure.Telegram;

public sealed record NotificationCallbackData(Guid NotificationId, NotificationDecision Decision)
{
    public const string Prefix = "notif";

    public static bool TryParse(string? data, out NotificationCallbackData parsed)
    {
        parsed = new NotificationCallbackData(Guid.Empty, NotificationDecision.Approve);
        if (string.IsNullOrEmpty(data))
        {
            return false;
        }

        var segments = data.Split(':');
        if (segments.Length != 3
            || segments[0] != Prefix
            || segments[1].Length != 36
            || !Guid.TryParseExact(segments[1], "D", out var notificationId))
        {
            return false;
        }

        var decision = segments[2] switch
        {
            "approve" => NotificationDecision.Approve,
            "decline" => NotificationDecision.Decline,
            _ => (NotificationDecision?)null
        };

        if (decision is not { } resolved)
        {
            return false;
        }

        parsed = new NotificationCallbackData(notificationId, resolved);
        return true;
    }
}
