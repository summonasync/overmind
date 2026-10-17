using Overmind.Core;

namespace Overmind.Host.Api.Validation;

public static class CodeSurfaceRules
{
    private static readonly char[] UnsupportedMetacharacters = ['?', '[', ']', '{', '}', '\\'];

    public static readonly string SetSizeMessage =
        $"At most {PlanningFeature.MaxCodeSurfaces} code surface patterns are allowed.";

    public static readonly string EntryMessage =
        "Each code surface pattern must be a non-empty repo-relative glob using only the * and ** wildcards " +
        "(the metacharacters ?, [, ], {, } and \\ are not supported and are rejected as literals would misreport " +
        "intersections; no leading '/', no '.' or '..' or empty segments, at most " +
        $"{PlanningFeature.MaxCodeSurfaceLength} characters).";

    public static bool IsValidEntry(string? pattern)
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
