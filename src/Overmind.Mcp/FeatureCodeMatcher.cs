namespace Overmind.Mcp;

public static class FeatureCodeMatcher
{
    public static IReadOnlyList<FeatureCodeCandidate> Match(string slug, IReadOnlyList<FeatureCodeCandidate> candidates)
    {
        var number = NumberPrefixOf(slug);
        var matches = new List<FeatureCodeCandidate>();
        foreach (var candidate in candidates)
        {
            if (candidate.Names.Any(name => Matches(name, slug, number)))
            {
                matches.Add(candidate);
            }
        }

        return matches;
    }

    private static bool Matches(string name, string slug, string? number)
    {
        if (name.Contains(slug, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return number is not null && HasNumberToken(name, number);
    }

    private static bool HasNumberToken(string name, string number)
    {
        var from = 0;
        while ((from = name.IndexOf(number, from, StringComparison.Ordinal)) >= 0)
        {
            var boundaryBefore = from == 0 || name[from - 1] is '/' or '-';
            var after = from + number.Length;
            if (boundaryBefore && after < name.Length && name[after] is '-')
            {
                return true;
            }

            from++;
        }

        return false;
    }

    private static string? NumberPrefixOf(string slug)
    {
        var digits = 0;
        while (digits < slug.Length && char.IsAsciiDigit(slug[digits]))
        {
            digits++;
        }

        return digits == 0 || digits == slug.Length ? null : slug[..digits];
    }
}
