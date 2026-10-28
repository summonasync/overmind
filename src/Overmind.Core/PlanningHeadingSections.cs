using System.Globalization;

namespace Overmind.Core;

public static class PlanningHeadingSections
{
    public static bool IsAddress(string section) => !PlanningSections.IsNameParseable(section);

    public static bool TryGetHeadingText(string section, out string text)
    {
        text = section.TrimStart('#').Trim();
        return text.Length > 0;
    }

    public static IReadOnlyList<PlanningHeadingBlock> Find(string headingText, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headingText);
        ArgumentNullException.ThrowIfNull(body);

        var blocks = new List<PlanningHeadingBlock>();
        var fence = default(char?);
        var lineStart = 0;
        int? openStart = null;
        var openLine = string.Empty;
        var openText = string.Empty;

        foreach (var raw in body.Split('\n'))
        {
            if (!IsFencedLine(raw, ref fence) &&
                TryParse(raw, out var level, out var text) &&
                level <= 2)
            {
                if (openStart is { } start)
                {
                    blocks.Add(new PlanningHeadingBlock(
                        openText, openLine, start, lineStart, body[lineStart - 1] == '\n'));
                }

                if (level == 2 && text == headingText)
                {
                    openStart = lineStart;
                    openLine = raw;
                    openText = text;
                }
                else
                {
                    openStart = null;
                }
            }

            lineStart += raw.Length + 1;
        }

        if (openStart is { } tailStart)
        {
            blocks.Add(new PlanningHeadingBlock(
                openText, openLine, tailStart, body.Length, body[^1] == '\n'));
        }

        return blocks;
    }

    public static IReadOnlyList<string> List(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var headings = new List<string>();
        var fence = default(char?);

        foreach (var raw in body.Split('\n'))
        {
            if (!IsFencedLine(raw, ref fence) &&
                TryParse(raw, out var level, out var text) &&
                level == 2)
            {
                headings.Add($"{new string('#', level)} {text}");
            }
        }

        return headings;
    }

    public static string Replace(PlanningHeadingBlock block, string body, string content)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(content);

        var newLine = NewLine(block.HeadingLine);
        var lines = ContentLines(content).Select(WithoutCarriageReturn).ToArray();
        var heading = WithoutCarriageReturn(block.HeadingLine);
        var replacement = lines.Length > 0
            ? string.Concat(heading, newLine, string.Join(newLine, lines), block.EndsWithNewline ? newLine : "")
            : string.Concat(heading, block.EndsWithNewline ? newLine : "");

        return string.Concat(body.AsSpan(0, block.Start), replacement, body.AsSpan(block.End));
    }

    public static string Insert(string headingText, string body, string? content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headingText);
        ArgumentNullException.ThrowIfNull(body);

        IReadOnlyList<string> lines = string.IsNullOrWhiteSpace(content)
            ? []
            : ContentLines(content);
        var payload = lines.Count == 0 ? PlanningSections.EmptyMarker : string.Join("\n", lines);
        var section = string.Concat("## ", headingText, "\n", payload, "\n");

        return body.Length == 0 || body.EndsWith('\n')
            ? string.Concat(body, section)
            : string.Concat(body, "\n", section);
    }

    public static string Remove(PlanningHeadingBlock block, string body, string markerLine)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(markerLine);

        var replacement = block.EndsWithNewline
            ? string.Concat(markerLine, NewLine(block.HeadingLine))
            : markerLine;
        return string.Concat(body.AsSpan(0, block.Start), replacement, body.AsSpan(block.End));
    }

    public static string TombstoneLine(string reason) =>
        string.Concat(
            "<removed ",
            DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            " — ",
            reason.Trim(),
            ">");

    private static IReadOnlyList<string> ContentLines(string content)
    {
        var lines = content.Split('\n');
        var first = 0;
        var last = lines.Length - 1;

        while (first <= last && lines[first].Trim().Length == 0)
        {
            first++;
        }

        while (last >= first && lines[last].Trim().Length == 0)
        {
            last--;
        }

        return lines[first..(last + 1)];
    }

    private static string NewLine(string headingLine) => headingLine.EndsWith('\r') ? "\r\n" : "\n";

    private static string WithoutCarriageReturn(string line) =>
        line.EndsWith('\r') ? line[..^1] : line;

    private static bool TryParse(string line, out int level, out string text)
    {
        level = 0;
        text = string.Empty;

        var start = 0;
        while (start < line.Length && line[start] is ' ' or '\t')
        {
            start++;
        }

        var hashes = 0;
        while (start + hashes < line.Length && line[start + hashes] == '#')
        {
            hashes++;
        }

        if (hashes is 0 or > 6)
        {
            return false;
        }

        var textStart = start + hashes;
        if (textStart < line.Length && line[textStart] is not (' ' or '\t'))
        {
            return false;
        }

        level = hashes;
        text = line[textStart..].Trim();
        return true;
    }

    private static bool IsFencedLine(string line, ref char? fence)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
        {
            var marker = trimmed[0];
            if (fence == marker)
            {
                fence = null;
            }
            else if (fence is null)
            {
                fence = marker;
            }

            return true;
        }

        return fence is not null;
    }
}

public sealed record PlanningHeadingBlock(
    string Text,
    string HeadingLine,
    int Start,
    int End,
    bool EndsWithNewline);
