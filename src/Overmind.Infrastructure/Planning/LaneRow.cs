namespace Overmind.Infrastructure.Planning;

public sealed record LaneRow(
    string Name,
    string? Contour,
    int? SerialOrder,
    int? Priority,
    string? BoundAlias,
    IReadOnlyList<LiveSurfaceEntry> LiveSurfaces,
    int OpenFeatureCount,
    LaneHolder? Holder,
    IReadOnlyList<LaneFeatureSummary> Features);
