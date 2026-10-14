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
            var inlineClose = FindLastInlineLogClose(body);
            if (inlineClose >= 0)
            {
                return body[..inlineClose] + "\n" + line + "\n" + body[inlineClose..];
            }

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

    public static string ReplaceNewest(string body, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(body);

        if (!PlanningSections.TryFind("log", body, out var section))
        {
            throw new InvalidOperationException("The body carries no log section.");
        }

        var contentStart = section.OpenIndex + section.OpenLength;
        if (contentStart < body.Length && body[contentStart] == '\n')
        {
            contentStart++;
        }

        var contentEnd = section.CloseIndex;
        if (contentEnd >= 2 && body[contentEnd - 1] == '\n')
        {
            contentEnd -= body[contentEnd - 2] == '\r' ? 2 : 1;
        }

        var lineStart = -1;
        var lineLength = 0;

        if (contentEnd >= contentStart)
        {
            var offset = 0;
            foreach (var raw in body[contentStart..contentEnd].Split('\n'))
            {
                if (raw.Trim().StartsWith("- ", StringComparison.Ordinal))
                {
                    lineStart = contentStart + offset;
                    lineLength = raw.Length;
                }

                offset += raw.Length + 1;
            }
        }

        if (lineStart < 0)
        {
            throw new InvalidOperationException("The log section holds no line to edit.");
        }

        var line = body.Substring(lineStart, lineLength);
        var separator = line.IndexOf(" — ", StringComparison.Ordinal);
        if (separator < 0)
        {
            throw new InvalidOperationException("The newest log line is not a dated entry; its text cannot be replaced.");
        }

        var carriageReturn = line.EndsWith('\r') ? "\r" : string.Empty;
        var edited = string.Concat(line.AsSpan(0, separator + 3), text, carriageReturn);

        return string.Concat(body.AsSpan(0, lineStart), edited, body.AsSpan(lineStart + lineLength));
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

    // Bodies seeded with a one-line log wrapper carry no anchored pair, yet appending a block
    // wrapper next to it splits the journal in two on render; the line joins that wrapper
    // instead. Prose mentions never qualify: the open must start a line, the close must end one.
    private static int FindLastInlineLogClose(string body)
    {
        var candidate = -1;
        var cursor = 0;

        while (cursor < body.Length)
        {
            var open = IndexOfLineStartToken(body, "<log>", cursor);
            if (open < 0)
            {
                break;
            }

            var close = IndexOfLineEndToken(body, "</log>", open + "<log>".Length);
            if (close < 0)
            {
                cursor = open + "<log>".Length;
                continue;
            }

            candidate = close;
            cursor = close + "</log>".Length;
        }

        return candidate;
    }

    private static int IndexOfLineStartToken(string body, string token, int start)
    {
        var index = start;
        while ((index = body.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            if (index == 0 || body[index - 1] == '\n')
            {
                return index;
            }

            index += token.Length;
        }

        return -1;
    }

    private static int IndexOfLineEndToken(string body, string token, int start)
    {
        var index = start;
        while ((index = body.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            var end = index + token.Length;
            if (end == body.Length || body[end] == '\n' ||
                (body[end] == '\r' && end + 1 < body.Length && body[end + 1] == '\n'))
            {
                return index;
            }

            index = end;
        }

        return -1;
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
