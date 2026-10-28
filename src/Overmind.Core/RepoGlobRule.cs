namespace Overmind.Core;

public static class RepoGlobRule
{
    private static readonly char[] UnsupportedMetacharacters = ['?', '[', ']', '{', '}', '\\'];

    public static bool IsValid(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var trimmed = pattern.Trim();
        if (trimmed.Length > PlanningFeature.MaxCodeSurfaceLength ||
            trimmed.StartsWith('/') ||
            trimmed.IndexOfAny(UnsupportedMetacharacters) >= 0)
        {
            return false;
        }

        return trimmed.Split('/').All(segment => segment.Length > 0 && segment is not (".." or "."));
    }
}
