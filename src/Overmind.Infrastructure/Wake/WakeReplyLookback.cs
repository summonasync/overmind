namespace Overmind.Infrastructure.Wake;

public static class WakeReplyLookback
{
    public const string EnvironmentVariable = "OVERMIND_WAKE_REPLY_LOOKBACK_DAYS";
    public const int DefaultDays = 7;
    public const int MinDays = 1;

    public static TimeSpan Resolve() => TimeSpan.FromDays(ResolveDays());

    internal static int ResolveDays()
    {
        var raw = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!int.TryParse(raw, out var days))
        {
            return DefaultDays;
        }

        return Math.Max(days, MinDays);
    }
}
