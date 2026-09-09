using System.Text.RegularExpressions;

namespace Overmind.Core;

public static partial class PlanningLog
{
    [GeneratedRegex(@"(?m)^[ \t]*<log>[ \t]*$", RegexOptions.None, 2000)]
    private static partial Regex LogOpenPattern();

    [GeneratedRegex(@"(?m)^[ \t]*</log>[ \t]*$", RegexOptions.None, 2000)]
    private static partial Regex LogClosePattern();

    public static string Append(string body, string note, DateTime? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        var timestamp = (now ?? DateTime.UtcNow).ToString("yyyy-MM-dd");
        var line = $"- {timestamp} \u2014 {note}";

        var openMatches = LogOpenPattern().Matches(body);
        var closeMatches = LogClosePattern().Matches(body);

        var lastOpen = FindLastAnchoredPair(openMatches, closeMatches);

        if (lastOpen is null)
        {
            var separator = body.Length > 0 && !body.EndsWith('\n') ? "\n\n" : (body.Length > 0 ? "\n" : "");
            return body + $"{separator}<log>\n{line}\n</log>\n";
        }

        var (openIndex, openLength, closeIndex) = lastOpen.Value;
        var before = body.AsSpan(0, closeIndex);
        var after = body.AsSpan(closeIndex);

        var result = new System.Text.StringBuilder(body.Length + line.Length + 4);
        result.Append(before);

        if (before.Length > 0 && before[^1] != '\n')
        {
            result.Append('\n');
        }

        result.Append(line);
        result.Append('\n');
        result.Append(after);

        return result.ToString();
    }

    public static IReadOnlyList<PlanningLogLine> Parse(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return [];
        }

        var openMatches = LogOpenPattern().Matches(body);
        var closeMatches = LogClosePattern().Matches(body);

        var lastOpen = FindLastAnchoredPair(openMatches, closeMatches);

        if (lastOpen is null)
        {
            return [];
        }

        var (openIndex, openLength, closeIndex) = lastOpen.Value;
        var content = body.Substring(openIndex + openLength, closeIndex - openIndex - openLength);
        var lines = content.Split('\n');
        var result = new List<PlanningLogLine>();

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.Trim();
            if (!trimmed.StartsWith("- "))
            {
                continue;
            }

            var textPart = trimmed[2..];
            var separatorIdx = textPart.IndexOf(" \u2014 ", StringComparison.Ordinal);

            if (separatorIdx > 0
                && separatorIdx >= 10
                && DateOnly.TryParseExact(textPart[..10], "yyyy-MM-dd", out var date))
            {
                var text = separatorIdx + 3 < textPart.Length
                    ? textPart[(separatorIdx + 3)..]
                    : string.Empty;
                result.Add(new PlanningLogLine(date, text));
            }
            else
            {
                result.Add(new PlanningLogLine(null, textPart));
            }
        }

        return result;
    }

    private static (int OpenIndex, int OpenLength, int CloseIndex)? FindLastAnchoredPair(
        MatchCollection openMatches, MatchCollection closeMatches)
    {
        for (var i = openMatches.Count - 1; i >= 0; i--)
        {
            var openMatch = openMatches[i];
            for (var j = closeMatches.Count - 1; j >= 0; j--)
            {
                var closeMatch = closeMatches[j];
                if (closeMatch.Index > openMatch.Index)
                {
                    return (openMatch.Index, openMatch.Length, closeMatch.Index);
                }
            }
        }

        return null;
    }
}

public sealed record PlanningLogLine(DateOnly? Date, string Text);
