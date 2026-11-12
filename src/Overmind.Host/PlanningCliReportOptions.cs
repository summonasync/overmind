using System.Globalization;

namespace Overmind.Host;

internal sealed record PlanningCliReportOptions(DateOnly Day, string Tag)
{
    public const string DefaultTag = "daily-notes";

    public string Since => UtcInstant(Day);

    public string Until => UtcInstant(Day.AddDays(1));

    internal static PlanningCliReportOptions From(PlanningCliArgs args)
    {
        var raw = args.Flag("--date");
        if (raw is null)
        {
            args.Problems.Add("report requires --date <yyyy-MM-dd> — the local day to report on.");
            return new PlanningCliReportOptions(default, DefaultTag);
        }

        if (!DateOnly.TryParseExact(
                raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            args.Problems.Add("--date expects yyyy-MM-dd — like 2026-11-12.");
            return new PlanningCliReportOptions(default, DefaultTag);
        }

        return new PlanningCliReportOptions(day, args.Flag("--tag") ?? DefaultTag);
    }

    private static string UtcInstant(DateOnly day) =>
        DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local)
            .ToUniversalTime()
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
