namespace Overmind.Infrastructure.Planning;

public sealed record LaneUpsert(
    string Name,
    string? Contour,
    int? SerialOrder,
    int? Priority = null,
    IReadOnlyList<LiveSurfaceEntry>? LiveSurfaces = null,
    string? BoundAlias = null);
