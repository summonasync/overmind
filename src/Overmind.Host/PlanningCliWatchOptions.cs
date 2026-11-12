using System.Globalization;
using System.Text.RegularExpressions;

namespace Overmind.Host;

internal sealed record PlanningCliWatchOptions(
    string? Alias,
    string? KindFilter,
    TimeSpan Timeout,
    TimeSpan Interval)
{
    public const string MentionKind = "wave-mention";
    public const string ReplyKind = "notification-reply";
    public const string DefaultTimeout = "30m";
    public const string DefaultInterval = "10s";

    private static readonly Regex DurationPattern =
        new(@"^(?<amount>\d+(?:\.\d+)?)(?<unit>ms|s|m|h|d)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static PlanningCliWatchOptions From(PlanningCliArgs args)
    {
        var alias = args.Flag("--alias");
        var mentions = args.Has("--mentions");
        var replies = args.Has("--replies");
        string? kind = null;
        if (mentions && replies)
        {
            args.Problems.Add(
                "--mentions and --replies are exclusive — filter one signal kind or run without either.");
        }
        else if (mentions)
        {
            kind = MentionKind;
        }
        else if (replies)
        {
            kind = ReplyKind;
        }

        return new PlanningCliWatchOptions(
            alias,
            kind,
            Duration(args, "--timeout", DefaultTimeout),
            Duration(args, "--interval", DefaultInterval));
    }

    internal static bool TryParseDuration(string raw, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        var match = DurationPattern.Match(raw);
        if (match.Success)
        {
            var amount = double.Parse(
                match.Groups["amount"].Value, CultureInfo.InvariantCulture);
            value = match.Groups["unit"].Value.ToLowerInvariant() switch
            {
                "ms" => TimeSpan.FromMilliseconds(amount),
                "s" => TimeSpan.FromSeconds(amount),
                "m" => TimeSpan.FromMinutes(amount),
                "h" => TimeSpan.FromHours(amount),
                _ => TimeSpan.FromDays(amount),
            };
            return true;
        }

        return raw.Contains(':') && TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out value);
    }

    private static TimeSpan Duration(PlanningCliArgs args, string flag, string fallback)
    {
        var raw = args.Flag(flag) ?? fallback;
        if (TryParseDuration(raw, out var value) && value > TimeSpan.Zero)
        {
            return value;
        }

        args.Problems.Add(
            $"{flag} expects a duration above zero — like 90s, 30m, 2h, 1d or hh:mm:ss.");
        return TimeSpan.Zero;
    }
}
