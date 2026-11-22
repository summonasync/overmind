namespace Overmind.Mcp.ToolBinding;

// Near-miss naming (266/T3): when a caller names a parameter (or a slug) that does not exist, the
// error may point at the closest real one — but only when exactly one candidate wins. Ambiguity
// stays a plain rejection: a guessed write is worse than a refused call.
// Two shapes count as near:
//   whole-segment identity — one name is a whole word inside the other (logNote answers note,
//   251-global answers 251), scored 0;
//   small edit distance once separators and case are folded (project_id vs projectId, room vs
//   rooms, entryIdd vs entryId), scored by the distance itself.
// The distance limit grows with the name and is read off the SHORTER of the two forms, so a
// three-letter name never borrows the slack of a long one.
internal static class NearestName
{
    public static string? Suggest(string name, IEnumerable<string> candidates, bool wholeSegment = true)
    {
        var target = Fold(name);
        if (target.Length == 0)
        {
            return null;
        }

        string? best = null;
        var bestScore = int.MaxValue;
        var rivals = 0;

        foreach (var candidate in candidates)
        {
            var other = Fold(candidate);
            if (other.Length == 0)
            {
                continue;
            }

            var score = Score(name, target, candidate, other, wholeSegment);
            if (score > Limit(Math.Min(target.Length, other.Length)))
            {
                continue;
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
                rivals = 1;
            }
            else if (score == bestScore && !string.Equals(best, candidate, StringComparison.Ordinal))
            {
                rivals++;
            }
        }

        return rivals == 1 ? best : null;
    }

    private static int Score(string rawTarget, string target, string rawOther, string other, bool wholeSegment)
    {
        if (target == other)
        {
            return 0;
        }

        if (wholeSegment && SharesWholeSegment(rawTarget, rawOther))
        {
            return 0;
        }

        return Distance(target, other);
    }

    private static bool SharesWholeSegment(string rawTarget, string rawOther)
    {
        var target = Segments(rawTarget);
        var other = Segments(rawOther);
        foreach (var segment in target)
        {
            if (other.Contains(segment, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // Word boundaries: hyphens and underscores (slugs, snake_case params) plus camelCase humps.
    private static IReadOnlyList<string> Segments(string name)
    {
        var parts = new List<string>();
        var start = 0;
        for (var i = 0; i < name.Length; i++)
        {
            var boundary = i > start && (name[i] is '-' or '_'
                || (char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]) && name[i - 1] != '-' && name[i - 1] != '_'));
            if (!boundary)
            {
                continue;
            }

            Add(parts, name[start..i]);
            start = i;
            if (name[i] is '-' or '_')
            {
                start++;
            }
        }

        Add(parts, name[start..]);
        return parts;
    }

    private static void Add(List<string> parts, string segment)
    {
        var folded = Fold(segment);
        if (folded.Length > 0)
        {
            parts.Add(folded);
        }
    }

    private static string Fold(string text) =>
        text.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();

    private static int Limit(int shortest) => Math.Max(2, shortest / 2);

    private static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
