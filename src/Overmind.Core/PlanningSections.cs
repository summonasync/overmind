using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Overmind.Core;

public static partial class PlanningSections
{
    [GeneratedRegex(@"(?m)^[ \t]*<(?<name>[a-z][a-z0-9-]*)>[ \t]*\r?$", RegexOptions.None, 2000)]
    private static partial Regex OpenTagPattern();

    [GeneratedRegex(@"(?m)^[ \t]*</(?<name>[a-z][a-z0-9-]*)>[ \t]*\r?$", RegexOptions.None, 2000)]
    private static partial Regex CloseTagPattern();

    public static PlanningSection? Find(string name, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(body);

        if (FindLastAnchoredPair(name, body) is not { } pair)
        {
            return null;
        }

        var (openIndex, openLength, closeIndex) = pair;
        var content = ExtractContent(body, openIndex + openLength, closeIndex);
        return new PlanningSection(name, content, openIndex, openLength, closeIndex);
    }

    public static bool TryFind(string name, string body, [NotNullWhen(true)] out PlanningSection? section)
    {
        section = Find(name, body);
        return section is not null;
    }

    public static string Replace(string name, string body, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(content);

        var section = Find(name, body)
            ?? throw new InvalidOperationException($"Section '{name}' not found.");

        var openEnd = section.OpenIndex + section.OpenLength;
        if (body[openEnd - 1] == '\r')
        {
            openEnd--;
        }

        var newLine = body[openEnd] == '\r' ? "\r\n" : "\n";
        var wrapped = content.Length == 0 ? newLine : string.Concat(newLine, content, newLine);

        return string.Concat(body.AsSpan(0, openEnd), wrapped, body.AsSpan(section.CloseIndex));
    }

    public static IReadOnlyList<string> List(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var opens = OpenTagPattern().Matches(body);
        var closes = CloseTagPattern().Matches(body);
        var names = new List<string>(opens.Count);

        foreach (Match open in opens)
        {
            if (HasCloseAfter(open, closes))
            {
                names.Add(open.Groups["name"].Value);
            }
        }

        return names;
    }

    public static IReadOnlyList<PlanningAcItem> ParseAcceptanceCriteria(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (Find("acceptance-criteria", body) is not { } section)
        {
            return [];
        }

        var items = new List<PlanningAcItem>();

        foreach (var rawLine in section.Content.Split('\n'))
        {
            var trimmed = rawLine.Trim();
            if (!trimmed.StartsWith("- "))
            {
                continue;
            }

            var rawText = trimmed[2..];
            bool? isChecked = rawText.StartsWith("[ ]") ? false
                : rawText.StartsWith("[x]") || rawText.StartsWith("[X]") ? true
                : null;
            items.Add(new PlanningAcItem(items.Count + 1, isChecked, rawText));
        }

        return items;
    }

    private static bool HasCloseAfter(Match open, MatchCollection closes)
    {
        for (var j = closes.Count - 1; j >= 0; j--)
        {
            var close = closes[j];
            if (close.Index <= open.Index)
            {
                break;
            }

            if (close.Groups["name"].Value == open.Groups["name"].Value)
            {
                return true;
            }
        }

        return false;
    }

    private static (int OpenIndex, int OpenLength, int CloseIndex)? FindLastAnchoredPair(string name, string body)
    {
        var opens = OpenTagPattern().Matches(body);
        var closes = CloseTagPattern().Matches(body);

        for (var i = opens.Count - 1; i >= 0; i--)
        {
            var open = opens[i];
            if (open.Groups["name"].Value != name)
            {
                continue;
            }

            for (var j = closes.Count - 1; j >= 0; j--)
            {
                var close = closes[j];
                if (close.Index > open.Index && close.Groups["name"].Value == name)
                {
                    return (open.Index, open.Length, close.Index);
                }
            }
        }

        return null;
    }

    private static string ExtractContent(string body, int openEnd, int closeIndex)
    {
        var start = openEnd < body.Length && body[openEnd] == '\n' ? openEnd + 1 : openEnd;

        var end = closeIndex;
        if (end >= 2 && body[end - 1] == '\n')
        {
            end -= body[end - 2] == '\r' ? 2 : 1;
        }

        return start <= end ? body[start..end] : string.Empty;
    }
}

public sealed record PlanningSection(string Name, string Content, int OpenIndex, int OpenLength, int CloseIndex);

public sealed record PlanningAcItem(int Index, bool? Checked, string RawText);
