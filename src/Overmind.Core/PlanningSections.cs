using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace Overmind.Core;

public static partial class PlanningSections
{
    public const string EmptyMarker = "(none)";

    [GeneratedRegex(@"(?m)^[ \t]*<(?<name>[a-z][a-z0-9-]*)>[ \t]*\r?$", RegexOptions.None, 2000)]
    private static partial Regex OpenTagPattern();

    [GeneratedRegex(@"(?m)^[ \t]*</(?<name>[a-z][a-z0-9-]*)>[ \t]*\r?$", RegexOptions.None, 2000)]
    private static partial Regex CloseTagPattern();

    [GeneratedRegex(@"\[[xX ]\]")]
    private static partial Regex AcMarker();

    [GeneratedRegex(@"^[a-z][a-z0-9-]*$", RegexOptions.None, 200)]
    private static partial Regex NamePattern();

    public static bool IsNameParseable(string name) =>
        !string.IsNullOrEmpty(name) && NamePattern().IsMatch(name);

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
        var sanitized = StripWrapperTags(name, content);
        var wrapped = sanitized.Length == 0 ? newLine : string.Concat(newLine, sanitized, newLine);

        return string.Concat(body.AsSpan(0, openEnd), wrapped, body.AsSpan(section.CloseIndex));
    }

    // Remove is the tombstone twin of Replace: the whole anchored pair — wrapper lines included —
    // collapses into the caller's marker line, so the section stops resolving while the trace stays.
    public static string Remove(string name, string body, string markerLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(markerLine);

        var section = Find(name, body)
            ?? throw new InvalidOperationException($"Section '{name}' not found.");

        var openEnd = section.OpenIndex + section.OpenLength;
        if (body[openEnd - 1] == '\r')
        {
            openEnd--;
        }

        var newLine = body[openEnd] == '\r' ? "\r\n" : "\n";
        var closeEnd = body.IndexOf('\n', section.CloseIndex) is { } lineFeed && lineFeed >= 0
            ? lineFeed + 1
            : body.Length;

        return string.Concat(
            body.AsSpan(0, section.OpenIndex),
            markerLine,
            newLine,
            body.AsSpan(closeEnd));
    }

    // Insert is the append-side twin of Replace: patch cannot create a section, so a patch of a
    // declared-but-absent section lands here. Absent or blank content renders the declared-empty
    // marker so every task section is either caller-written or visibly empty — never missing.
    public static string Insert(string name, string body, string? content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(body);

        var sanitized = string.IsNullOrWhiteSpace(content)
            ? EmptyMarker
            : StripWrapperTags(name, content);
        if (sanitized.Length == 0)
        {
            sanitized = EmptyMarker;
        }

        var section = string.Concat("<", name, ">\n", sanitized, "\n</", name, ">\n");
        return body.Length == 0 || body.EndsWith('\n')
            ? string.Concat(body, section)
            : string.Concat(body, "\n", section);
    }

    // The void-relevant content of a validated spec/plan: body minus the log section, with AC
    // checkbox markers canonicalized. Log appends and tick flips are bookkeeping — they compare
    // equal here, so the content clock ignores them; everything else is gated (feature 259).
    public static string GatedContentOf(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var withoutLog = body;
        if (TryFind("log", body, out var log))
        {
            var closeEnd = body.IndexOf('\n', log.CloseIndex) is { } lineFeed && lineFeed >= 0
                ? lineFeed + 1
                : body.Length;
            withoutLog = string.Concat(body.AsSpan(0, log.OpenIndex), body.AsSpan(closeEnd));
        }

        return AcMarker().Replace(withoutLog, "[ ]").TrimEnd();
    }

    public static string Normalize(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var tokens = CollectTagTokens(body);
        if (tokens.Count == 0)
        {
            return body;
        }

        var drops = new List<(int Start, int End)>();
        foreach (var name in tokens.Select(t => t.Name).Distinct())
        {
            CollectWrapperDrops(body, name, tokens, drops);
        }

        if (drops.Count == 0)
        {
            return body;
        }

        drops.Sort((left, right) => right.Start.CompareTo(left.Start));
        var result = body;
        foreach (var (start, end) in drops)
        {
            result = string.Concat(result.AsSpan(0, start), result.AsSpan(end));
        }

        return result;
    }

    public static int CountAnchoredBlocks(string name, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(body);

        var closes = CloseTagPattern().Matches(body);
        var count = 0;

        foreach (Match open in OpenTagPattern().Matches(body))
        {
            if (open.Groups["name"].Value == name && HasCloseAfter(open, closes))
            {
                count++;
            }
        }

        return count;
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

    // Read-door twin of the write-side canonicalization: locating on the canonical form is what
    // makes sections reads deterministic for one-line/inline wrappers. The raw body still decides
    // missingSections, so a tag the body carries is never reported absent.
    public static PlanningFilteredRead FilteredRead(string body, IEnumerable<string> requestedNames)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(requestedNames);

        var canonical = PlanningSectionCanonicalizer.Canonicalize(body);
        var found = new List<PlanningSection>();
        var missing = new List<string>();

        foreach (var name in requestedNames.Distinct())
        {
            if (Find(name, canonical) is not { } section)
            {
                // A name HasInlineSection sees but canonical locate misses only lives past the
                // canonicalizer caps — the same blind spot the write door has. Never missing.
                if (!PlanningSectionCanonicalizer.HasInlineSection(name, body))
                {
                    missing.Add(name);
                }

                continue;
            }

            found.Add(section);
        }

        var narrowed = new StringBuilder();
        foreach (var section in found.OrderBy(s => s.OpenIndex))
        {
            if (narrowed.Length > 0)
            {
                narrowed.Append('\n');
            }

            narrowed.Append('<').Append(section.Name).Append(">\n")
                .Append(section.Content)
                .Append("\n</").Append(section.Name).Append('>');
        }

        return new PlanningFilteredRead(narrowed.ToString(), missing);
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

    // Duplicate anchored pairs of one name only arise from legacy bodies the skeleton once
    // poisoned with an empty `(none)` tail copy: the true-last rule would serve that empty
    // copy and hide the real acceptance-criteria. The last pair whose content is not the
    // empty marker wins; an all-empty body keeps the true-last pair.
    private static (int OpenIndex, int OpenLength, int CloseIndex)? FindLastAnchoredPair(string name, string body)
    {
        var opens = OpenTagPattern().Matches(body);
        var closes = CloseTagPattern().Matches(body);
        (int OpenIndex, int OpenLength, int CloseIndex)? fallback = null;

        for (var i = opens.Count - 1; i >= 0; i--)
        {
            var open = opens[i];
            if (open.Groups["name"].Value != name)
            {
                continue;
            }

            foreach (Match close in closes)
            {
                if (close.Index > open.Index && close.Groups["name"].Value == name)
                {
                    fallback ??= (open.Index, open.Length, close.Index);
                    if (ExtractContent(body, open.Index + open.Length, close.Index).Trim() != EmptyMarker)
                    {
                        return (open.Index, open.Length, close.Index);
                    }

                    break;
                }
            }
        }

        return fallback;
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

    // A patch payload is section CONTENT, but callers routinely paste the rendered wrapper
    // along with it; splicing those tags verbatim inside the real wrapper breeds stray
    // duplicates that board reads (last anchored pair) never see again.
    private static string StripWrapperTags(string name, string content)
    {
        var openTag = string.Concat("<", name, ">");
        var closeTag = string.Concat("</", name, ">");
        var kept = new List<string>();

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim(' ', '\t');
            if (trimmed.EndsWith('\r'))
            {
                trimmed = trimmed[..^1];
            }

            if (trimmed == openTag || trimmed == closeTag)
            {
                continue;
            }

            kept.Add(line);
        }

        while (kept.Count > 0 && kept[0].Trim().Length == 0)
        {
            kept.RemoveAt(0);
        }

        while (kept.Count > 0 && kept[^1].Trim().Length == 0)
        {
            kept.RemoveAt(kept.Count - 1);
        }

        return string.Join('\n', kept);
    }

    // Renders the body the way the board reads it: one wrapper per section (the last open with
    // the first close after it), text a section read cannot see inside duplicated wrappers
    // dropped with them. Zones holding other sections are left intact.
    private static void CollectWrapperDrops(
        string body, string name, List<SectionTag> tokens, List<(int Start, int End)> drops)
    {
        if (FindRenderPair(body, name) is not { } canonical)
        {
            return;
        }

        var (openIndex, closeIndex) = canonical;
        var sameName = tokens.Where(t => t.Name == name).ToList();

        for (var i = 0; i < sameName.Count; i++)
        {
            var token = sameName[i];
            if (token.LineStart != openIndex && token.LineStart != closeIndex)
            {
                drops.Add((token.LineStart, token.NextLineStart));
            }

            if (i == sameName.Count - 1)
            {
                continue;
            }

            var next = sameName[i + 1];
            if (token.LineStart == openIndex && next.LineStart == closeIndex)
            {
                continue;
            }

            var zoneStart = token.NextLineStart;
            var zoneEnd = next.LineStart;
            if (zoneEnd <= zoneStart || ContainsAnyTag(tokens, zoneStart, zoneEnd))
            {
                continue;
            }

            drops.Add((zoneStart, zoneEnd));
        }
    }

    private static (int OpenIndex, int CloseIndex)? FindRenderPair(string body, string name)
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

            foreach (Match close in closes)
            {
                if (close.Index > open.Index && close.Groups["name"].Value == name)
                {
                    return (open.Index, close.Index);
                }
            }
        }

        return null;
    }

    private static List<SectionTag> CollectTagTokens(string body)
    {
        var opens = OpenTagPattern().Matches(body);
        var closes = CloseTagPattern().Matches(body);
        var tokens = new List<SectionTag>(opens.Count + closes.Count);

        foreach (Match open in opens)
        {
            tokens.Add(TagFromMatch(body, open, open.Groups["name"].Value));
        }

        foreach (Match close in closes)
        {
            tokens.Add(TagFromMatch(body, close, close.Groups["name"].Value));
        }

        return tokens.OrderBy(t => t.LineStart).ToList();
    }

    private static SectionTag TagFromMatch(string body, Match match, string name)
    {
        var lineEnd = match.Index + match.Length;
        var nextLineStart = lineEnd < body.Length && body[lineEnd] == '\n' ? lineEnd + 1 : lineEnd;
        return new SectionTag(name, match.Index, lineEnd, nextLineStart);
    }

    private static bool ContainsAnyTag(List<SectionTag> tokens, int start, int end) =>
        tokens.Any(t => t.LineStart >= start && t.LineStart < end);

    private sealed record SectionTag(string Name, int LineStart, int LineEnd, int NextLineStart);
}

public sealed record PlanningSection(string Name, string Content, int OpenIndex, int OpenLength, int CloseIndex);

public sealed record PlanningFilteredRead(string Body, IReadOnlyList<string> MissingNames);

public sealed record PlanningAcItem(int Index, bool? Checked, string RawText);
