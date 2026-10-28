using Overmind.Core;

namespace Overmind.Host.Api.Validation;

public static class CodeSurfaceRules
{
    public static readonly string SetSizeMessage =
        $"At most {PlanningFeature.MaxCodeSurfaces} code surface patterns are allowed.";

    public static readonly string EntryMessage =
        "Each code surface pattern must be a non-empty repo-relative glob using only the * and ** wildcards " +
        "(the metacharacters ?, [, ], {, } and \\ are not supported and are rejected as literals would misreport " +
        "intersections; no leading '/', no '.' or '..' or empty segments, at most " +
        $"{PlanningFeature.MaxCodeSurfaceLength} characters).";

    public static readonly string LiveSurfaceOwnerMessage =
        $"Each live surface requires a non-empty owner alias of at most " +
        $"{PlanningLane.MaxLiveSurfaceOwnerLength} characters.";

    public static readonly string LiveSurfaceSetSizeMessage =
        $"At most {PlanningLane.MaxLiveSurfaces} live surfaces are allowed per lane.";

    public static bool IsValidEntry(string? pattern) => RepoGlobRule.IsValid(pattern);
}
