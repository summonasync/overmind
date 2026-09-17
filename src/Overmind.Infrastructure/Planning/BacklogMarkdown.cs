using System.Globalization;

namespace Overmind.Infrastructure.Planning;

internal static class BacklogMarkdown
{
    public static (IReadOnlyDictionary<string, string> Fields, string Body) Parse(string content)
    {
        var firstLineEnd = content.IndexOf('\n');
        var firstLine = content[..(firstLineEnd < 0 ? content.Length : firstLineEnd)].Trim();
        if (firstLine != "---")
        {
            return (EmptyFields, content);
        }

        var boundary = FindFrontmatterEnd(content);
        if (boundary is null)
        {
            return (EmptyFields, content);
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var headerLines = content[3..boundary.Value.SplitIndex].Split('\n');
        foreach (var line in headerLines)
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (key.Length > 0 && value.Length > 0)
            {
                fields[key] = value;
            }
        }

        return (fields, content[boundary.Value.BodyIndex..].TrimStart('\r', '\n'));
    }

    public static DateOnly? ParseDate(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length >= 10
            && DateOnly.TryParseExact(
                trimmed[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    public static string? FirstHeading(string body)
    {
        foreach (var line in body.Split('\n'))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return line[2..].Trim();
            }
        }

        return null;
    }

    public static string StripLeadingHeadings(string body)
    {
        var lines = body.Split('\n');
        return string.Join('\n', lines[LeadingContentStart(lines)..]);
    }

    public static string CollapseLeadingHeadings(string body)
    {
        var lines = body.Split('\n');
        var heading = LeadingHeadingLine(lines);
        if (heading < 0)
        {
            return body;
        }

        var rest = string.Join('\n', lines[LeadingContentStart(lines)..]);
        var title = lines[heading].TrimEnd('\r');
        return rest.Length == 0 ? title : title + "\n\n" + rest;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyFields =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static int LeadingHeadingLine(string[] lines)
    {
        var index = 0;
        while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index]))
        {
            index++;
        }

        return index < lines.Length && lines[index].StartsWith("# ", StringComparison.Ordinal) ? index : -1;
    }

    private static int LeadingContentStart(string[] lines)
    {
        var index = 0;
        while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index]))
        {
            index++;
        }

        while (index < lines.Length && lines[index].StartsWith("# ", StringComparison.Ordinal))
        {
            index++;
            while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
            }
        }

        return index;
    }

    private static (int SplitIndex, int BodyIndex)? FindFrontmatterEnd(string content)
    {
        var lineStart = content.IndexOf('\n') + 1;
        while (lineStart > 0 && lineStart < content.Length)
        {
            var lineEnd = content.IndexOf('\n', lineStart);
            var line = content[lineStart..(lineEnd < 0 ? content.Length : lineEnd)].Trim();
            if (line == "---")
            {
                return (lineStart, lineEnd < 0 ? content.Length : lineEnd + 1);
            }

            if (lineEnd < 0)
            {
                return null;
            }

            lineStart = lineEnd + 1;
        }

        return null;
    }
}
