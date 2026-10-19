namespace Overmind.Infrastructure.Grimoire;

public static class GrimoireOutcomeParser
{
    private const string GrimoireVerbPrefix = "grimoire ";
    private const string SkippedVerb = "skipped (not detected)";
    private const string DetailSeparator = " — ";

    public static IReadOnlyList<GrimoireAgentOutcome> Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var outcomes = new List<GrimoireAgentOutcome>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (ParseLine(line) is { } outcome)
            {
                outcomes.Add(outcome);
            }
        }

        return outcomes;
    }

    private static GrimoireAgentOutcome? ParseLine(string line)
    {
        var nameSeparator = line.IndexOf(':');
        if (nameSeparator <= 0)
        {
            return null;
        }

        var agent = line[..nameSeparator].Trim();
        var tail = line[(nameSeparator + 1)..].Trim();
        if (tail.StartsWith(SkippedVerb, StringComparison.Ordinal))
        {
            SplitDetail(tail[SkippedVerb.Length..], out var skippedPath);
            return new GrimoireAgentOutcome(agent, "skipped", ExtractPath(skippedPath), null);
        }

        if (!tail.StartsWith(GrimoireVerbPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var detail = tail[GrimoireVerbPrefix.Length..].Trim();
        var verb = SplitDetail(detail, out var remainder);
        return new GrimoireAgentOutcome(agent, verb, ExtractPath(remainder), ExtractError(remainder));
    }

    private static string SplitDetail(string detail, out string remainder)
    {
        var separator = detail.IndexOf(DetailSeparator, StringComparison.Ordinal);
        if (separator < 0)
        {
            remainder = string.Empty;
            return detail;
        }

        remainder = detail[(separator + DetailSeparator.Length)..].Trim();
        return detail[..separator].Trim();
    }

    private static string? ExtractPath(string remainder)
    {
        if (remainder.Length == 0)
        {
            return null;
        }

        return SplitError(remainder, out var path) ? path : remainder;
    }

    private static string? ExtractError(string remainder) =>
        SplitError(remainder, out var path) ? remainder[(path.Length + 2)..^1].Trim() : null;

    private static bool SplitError(string remainder, out string path)
    {
        var start = remainder.LastIndexOf(" (", StringComparison.Ordinal);
        if (start > 0 && remainder.EndsWith(')'))
        {
            path = remainder[..start].Trim();
            return true;
        }

        path = remainder;
        return false;
    }
}
