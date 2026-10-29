namespace Overmind.Mcp;

public static class WakePeriods
{
    public const string IdleEnvironmentVariable = "OVERMIND_WAKE_IDLE_SECONDS";
    public const string GateEnvironmentVariable = "OVERMIND_WAKE_GATE_SECONDS";
    public const int DefaultIdleSeconds = 60;
    public const int MinIdleSeconds = 5;
    public const int MaxIdleSeconds = 600;
    public const int DefaultGateSeconds = 5;
    public const int MinGateSeconds = 1;
    public const int MaxGateSeconds = 60;

    public static TimeSpan ResolveIdle() =>
        FromSeconds(IdleEnvironmentVariable, DefaultIdleSeconds, MinIdleSeconds, MaxIdleSeconds);

    public static TimeSpan ResolveGate() =>
        FromSeconds(GateEnvironmentVariable, DefaultGateSeconds, MinGateSeconds, MaxGateSeconds);

    internal static TimeSpan FromSeconds(string variable, int fallback, int min, int max)
    {
        var raw = Environment.GetEnvironmentVariable(variable);
        var seconds = int.TryParse(raw, out var parsed) ? parsed : fallback;
        if (seconds < min)
        {
            return TimeSpan.FromSeconds(min);
        }

        return seconds > max ? TimeSpan.FromSeconds(max) : TimeSpan.FromSeconds(seconds);
    }
}
