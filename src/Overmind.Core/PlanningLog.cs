using System.Text;

namespace Overmind.Core;

public static class PlanningLog
{
    public static string Append(string body, string note, DateTime? now = null, PlanningLogSource? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        var timestamp = (now ?? DateTime.UtcNow).ToString("yyyy-MM-dd");
        var line = source is { } src
            ? $"- {timestamp} [{src.ToString().ToLowerInvariant()}] — {note}"
            : $"- {timestamp} — {note}";

        if (!PlanningSections.TryFind("log", body, out var section))
        {
            var separator = body.Length > 0 && !body.EndsWith('\n') ? "\n\n" : (body.Length > 0 ? "\n" : "");
            return body + $"{separator}<log>\n{line}\n</log>\n";
        }

        var before = body.AsSpan(0, section.CloseIndex);
        var result = new StringBuilder(body.Length + line.Length + 4);
        result.Append(before);

        if (before.Length > 0 && before[^1] != '\n')
        {
            result.Append('\n');
        }

        result.Append(line);
        result.Append('\n');
        result.Append(body.AsSpan(section.CloseIndex));

        return result.ToString();
    }

    public static IReadOnlyList<PlanningLogLine> Parse(string body)
    {
        if (string.IsNullOrEmpty(body) || !PlanningSections.TryFind("log", body, out var section))
        {
            return [];
        }

        var result = new List<PlanningLogLine>();

        foreach (var rawLine in section.Content.Split('\n'))
        {
            var trimmed = rawLine.Trim();
            if (!trimmed.StartsWith("- "))
            {
                continue;
            }

            var textPart = trimmed[2..];
            var separatorIdx = textPart.IndexOf(" — ", StringComparison.Ordinal);

            if (separatorIdx > 0
                && separatorIdx >= 10
                && DateOnly.TryParseExact(textPart[..10], "yyyy-MM-dd", out var date))
            {
                var text = separatorIdx + 3 < textPart.Length
                    ? textPart[(separatorIdx + 3)..]
                    : string.Empty;
                result.Add(new PlanningLogLine(date, text, ParseSourceMarker(textPart, separatorIdx)));
            }
            else
            {
                result.Add(new PlanningLogLine(null, textPart));
            }
        }

        return result;
    }

    private static PlanningLogSource? ParseSourceMarker(string textPart, int separatorIdx)
    {
        var candidate = textPart.Substring(10, separatorIdx - 10).Trim();
        if (candidate.Length <= 2 || candidate[0] != '[' || candidate[^1] != ']')
        {
            return null;
        }

        var marker = candidate[1..^1];
        if (char.IsDigit(marker[0]))
        {
            return null;
        }

        return Enum.TryParse<PlanningLogSource>(marker, ignoreCase: true, out var source)
               && Enum.IsDefined(source)
            ? source
            : null;
    }
}

public sealed record PlanningLogLine(DateOnly? Date, string Text, PlanningLogSource? Source = null);
