namespace Overmind.Infrastructure.Notifications;

public sealed record ReplyAutoActResult(bool Acted, string Reason)
{
    public static ReplyAutoActResult Skipped(string reason) => new(false, reason);

    public static ReplyAutoActResult Performed(string reason) => new(true, reason);
}
