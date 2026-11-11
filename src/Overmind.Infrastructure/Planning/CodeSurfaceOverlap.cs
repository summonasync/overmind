namespace Overmind.Infrastructure.Planning;

public static class CodeSurfaceOverlap
{
    public static bool GlobsOverlap(string left, string right) =>
        BoundaryPrefixOverlaps(LiteralPrefix(left), LiteralPrefix(right));

    public static bool SetsOverlap(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        foreach (var l in left)
        {
            foreach (var r in right)
            {
                if (GlobsOverlap(l, r))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static IReadOnlyList<string> SharedGlobs(
        IReadOnlyList<string> left, IReadOnlyList<string> right, int cap)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ArgumentOutOfRangeException.ThrowIfNegative(cap);

        var shared = new List<string>();
        foreach (var l in left)
        {
            if (shared.Count >= cap)
            {
                break;
            }

            if (right.Any(r => GlobsOverlap(l, r)))
            {
                shared.Add(l);
            }
        }

        return shared;
    }

    public static bool IsExactMatch(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);

    public static string? BestPartnerPattern(string glob, IReadOnlyList<string> partners)
    {
        ArgumentNullException.ThrowIfNull(partners);

        string? best = null;
        var bestShared = -1;
        foreach (var candidate in partners)
        {
            if (!GlobsOverlap(glob, candidate))
            {
                continue;
            }

            if (IsExactMatch(glob, candidate))
            {
                return candidate;
            }

            var shared = SharedPrefixLength(LiteralPrefix(glob), LiteralPrefix(candidate));
            if (shared > bestShared
                || (shared == bestShared && string.CompareOrdinal(candidate, best) < 0))
            {
                best = candidate;
                bestShared = shared;
            }
        }

        return best;
    }

    internal static string LiteralPrefix(string glob)
    {
        var segments = glob.Split('/');
        var lastLiteral = -1;
        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i].Contains('*'))
            {
                break;
            }

            lastLiteral = i;
        }

        if (lastLiteral == segments.Length - 1)
        {
            return glob;
        }

        return lastLiteral < 0
            ? string.Empty
            : string.Concat(string.Join('/', segments[..(lastLiteral + 1)]), "/");
    }

    private static int SharedPrefixLength(string left, string right)
    {
        var max = Math.Min(left.Length, right.Length);
        for (var i = 0; i < max; i++)
        {
            if (left[i] != right[i])
            {
                return i;
            }
        }

        return max;
    }

    private static bool BoundaryPrefixOverlaps(string left, string right)
    {
        var (shorter, longer) = left.Length <= right.Length ? (left, right) : (right, left);

        if (shorter.Length == 0)
        {
            return true;
        }

        if (!longer.StartsWith(shorter, StringComparison.Ordinal))
        {
            return false;
        }

        return shorter.Length == longer.Length || shorter.EndsWith('/') || longer[shorter.Length] == '/';
    }
}
