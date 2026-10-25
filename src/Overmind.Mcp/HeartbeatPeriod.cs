namespace Overmind.Mcp;

public static class HeartbeatPeriod
{
    public const string EnvironmentVariable = "OVERMIND_HEARTBEAT_MINUTES";
    public const int DefaultMinutes = 5;
    public const int MinMinutes = 1;
    public const int MaxMinutes = 30;

    public static TimeSpan Resolve()
    {
        var raw = Environment.GetEnvironmentVariable(EnvironmentVariable);
        return int.TryParse(raw, out var minutes)
            ? FromMinutes(minutes)
            : FromMinutes(DefaultMinutes);
    }

    internal static TimeSpan FromMinutes(int minutes)
    {
        if (minutes < MinMinutes)
        {
            return TimeSpan.FromMinutes(MinMinutes);
        }

        return minutes > MaxMinutes ? TimeSpan.FromMinutes(MaxMinutes) : TimeSpan.FromMinutes(minutes);
    }
}
