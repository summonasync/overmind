namespace Overmind.Core;

public static class PlanningSectionCanonicalizer
{
    // Deep one-line nesting (``<a><b>…</b></a>``) expands fully in one pass so Canonicalize
    // stays byte-idempotent; the cap only keeps a hostile body from recursing without bound.
    private const int MaxInlineNesting = 8;
    // ParseGroups recurses once per sibling group; past this many closers a line stays raw.
    private const int MaxGroupsPerLine = 256;

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
                !TrySplitGroups(StripCarriageReturn(line, out var carriageReturn), out var indent, out var groups))
            {
                expanded.Add(line);
                continue;
            }

            changed = true;
            for (var i = 0; i < groups.Count; i++)
            {
                Expand(
                    expanded,
                    indent,
                    groups[i].Name,
                    groups[i].Content,
                    carriageReturn: carriageReturn && i == groups.Count - 1,
                    depth: 0);
            }
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
            if (IsFencedLine(line, ref fence) ||
                !TrySplitGroups(StripCarriageReturn(line, out _), out _, out var groups))
            {
                continue;
            }

            foreach (var group in groups)
            {
                if (group.Name == name)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void Expand(
        List<string> expanded, string indent, string name, string content, bool carriageReturn, int depth)
    {
        var lineEnd = carriageReturn ? "\r" : string.Empty;
        expanded.Add(string.Concat(indent, "<", name, ">", lineEnd));
        EmitContent(expanded, content, carriageReturn, depth);
        expanded.Add(string.Concat(indent, "</", name, ">", lineEnd));
    }

    private static void EmitContent(List<string> expanded, string content, bool carriageReturn, int depth)
    {
        if (content.Trim().Length == 0)
        {
            return;
        }

        if (depth < MaxInlineNesting && TrySplitGroups(content, out _, out var groups))
        {
            foreach (var group in groups)
            {
                Expand(expanded, string.Empty, group.Name, group.Content, carriageReturn: false, depth + 1);
            }

            return;
        }

        expanded.Add(string.Concat(content, carriageReturn ? "\r" : string.Empty));
    }

    private static bool TrySplitGroups(string text, out string indent, out List<InlineGroup> groups)
    {
        var start = 0;
        while (start < text.Length && text[start] is ' ' or '\t')
        {
            start++;
        }

        indent = text[..start];
        groups = [];
        if (CountClosers(text) > MaxGroupsPerLine)
        {
            return false;
        }

        return ParseGroups(text, start, groups) && groups.Count > 0;
    }

    private static int CountClosers(string text)
    {
        var count = 0;
        var from = 0;
        while ((from = text.IndexOf("</", from, StringComparison.Ordinal)) >= 0)
        {
            count++;
            from += 2;
        }

        return count;
    }

    private static bool ParseGroups(string text, int pos, List<InlineGroup> groups)
    {
        while (pos < text.Length && text[pos] is ' ' or '\t')
        {
            pos++;
        }

        if (pos == text.Length)
        {
            return true;
        }

        if (text[pos] != '<')
        {
            return false;
        }

        var nameEnd = text.IndexOf('>', pos + 1);
        if (nameEnd < 0 || !IsValidName(text[(pos + 1)..nameEnd], out var name))
        {
            return false;
        }

        var contentStart = nameEnd + 1;
        var close = string.Concat("</", name, ">");
        var searchFrom = contentStart;
        while (true)
        {
            var closeStart = text.IndexOf(close, searchFrom, StringComparison.Ordinal);
            if (closeStart < 0)
            {
                return false;
            }

            var insertAt = groups.Count;
            if (ParseGroups(text, closeStart + close.Length, groups))
            {
                groups.Insert(insertAt, new InlineGroup(name, text[contentStart..closeStart]));
                return true;
            }

            searchFrom = closeStart + 1;
        }
    }

    private static bool IsValidName(string candidate, out string name)
    {
        name = candidate;
        if (candidate.Length == 0 || candidate[0] is < 'a' or > 'z')
        {
            return false;
        }

        for (var i = 1; i < candidate.Length; i++)
        {
            if (candidate[i] is not ('-' or (>= 'a' and <= 'z') or (>= '0' and <= '9')))
            {
                return false;
            }
        }

        return true;
    }

    private static string StripCarriageReturn(string line, out bool carriageReturn)
    {
        carriageReturn = line.EndsWith('\r');
        return carriageReturn ? line[..^1] : line;
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

    private readonly record struct InlineGroup(string Name, string Content);
}
