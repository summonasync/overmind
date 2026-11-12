using System.Text;

namespace Overmind.Host;

internal static class PlanningCliWatchText
{
    internal static string Signal(PlanningCliWatchSignal signal) =>
        $"[{signal.Kind}] from {signal.From} in {signal.RoomId ?? "-"} "
        + $"at {signal.CreatedAt:O}: {signal.Excerpt}\n";

    internal static string NoSignals(TimeSpan timeout) =>
        $"no signals within {Format(timeout)}\n";

    internal static string Format(TimeSpan duration)
    {
        var builder = new StringBuilder();
        Append(builder, duration.Days, "d");
        Append(builder, duration.Hours, "h");
        Append(builder, duration.Minutes, "m");
        Append(builder, duration.Seconds, "s");
        return builder.Length == 0 ? "0s" : builder.ToString();
    }

    private static void Append(StringBuilder builder, int amount, string unit)
    {
        if (amount == 0)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(amount).Append(unit);
    }
}
