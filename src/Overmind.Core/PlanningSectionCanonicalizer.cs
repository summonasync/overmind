using System.Text.RegularExpressions;

namespace Overmind.Core;

public static partial class PlanningSectionCanonicalizer
{
    // Deep one-line nesting (``<a><b>…</b></a>``) expands fully in one pass so Canonicalize
    // stays byte-idempotent; the cap only keeps a hostile body from recursing without bound.
    private const int MaxInlineNesting = 8;

    [GeneratedRegex(@"^(?<indent>[ \t]*)<(?<name>[a-z][a-z0-9-]*)>(?<content>.*)</\k<name>>[ \t]*\r?$", RegexOptions.None, 2000)]
    private static partial Regex InlineSectionPattern();

    public static string Canonicalize(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var lines = body.Split('\n');
        var expanded = new List<string>(lines.Length);
        var changed = false;
        char? fence = null;

        foreach (var line in lines)
        {
            if (IsFencedLine(line, ref fence) ||
                InlineSectionPattern().Match(line) is not { Success: true } match)
            {
                expanded.Add(line);
                continue;
            }

            changed = true;
            Expand(
                expanded,
                match.Groups["indent"].Value,
                match.Groups["name"].Value,
                match.Groups["content"].Value,
                line.EndsWith('\r'),
                depth: 0);
        }

        return changed ? string.Join("\n", expanded) : body;
    }

    public static bool HasInlineSection(string name, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(body);

        char? fence = null;
        foreach (var line in body.Split('\n'))
        {
            if (IsFencedLine(line, ref fence))
            {
                continue;
            }

            var match = InlineSectionPattern().Match(line);
            if (match.Success && match.Groups["name"].Value == name)
            {
                return true;
            }
        }

        return false;
    }

    private static void Expand(
        List<string> expanded, string indent, string name, string content, bool carriageReturn, int depth)
    {
        var lineEnd = carriageReturn ? "\r" : string.Empty;
        expanded.Add(string.Concat(indent, "<", name, ">", lineEnd));

        if (content.Trim().Length > 0)
        {
            var nested = depth < MaxInlineNesting ? InlineSectionPattern().Match(content) : Match.Empty;
            if (nested.Success)
            {
                Expand(
                    expanded,
                    string.Empty,
                    nested.Groups["name"].Value,
                    nested.Groups["content"].Value,
                    carriageReturn: false,
                    depth + 1);
            }
            else
            {
                expanded.Add(string.Concat(content, lineEnd));
            }
        }

        expanded.Add(string.Concat(indent, "</", name, ">", lineEnd));
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
