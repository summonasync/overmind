using System.Text.RegularExpressions;

namespace Overmind.Infrastructure.Agent.Routes;

public static partial class AgentRouteText
{
    public const int MaxPatternLength = 120;
    public const int MaxDomainLength = 40;
    public const int MaxMissReasonLength = 120;

    private static readonly char[] KeywordSeparators = [',', ';'];

    [GeneratedRegex("\\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("[^\\p{L}\\p{N} ,;]")]
    private static partial Regex Symbols();

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var collapsed = Whitespace().Replace(Symbols().Replace(text.Trim().ToLowerInvariant(), " "), " ").Trim();
        return collapsed.Length <= MaxPatternLength ? collapsed : collapsed[..MaxPatternLength].TrimEnd();
    }

    public static string Clamp(string? text, int max) =>
        Normalize(text) is { Length: > 0 } normalized
            ? normalized.Length <= max ? normalized : normalized[..max].TrimEnd()
            : string.Empty;

    public static bool Matches(string normalizedText, string pattern)
    {
        var text = Normalize(normalizedText);
        if (text.Length == 0 || string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var keywords = pattern.Split(KeywordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (keywords.Length > 1)
        {
            return keywords.Any(keyword =>
                Normalize(keyword) is { Length: > 0 } key && text.Contains(key, StringComparison.Ordinal));
        }

        return Normalize(pattern)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(token => text.Contains(token, StringComparison.Ordinal));
    }
}
