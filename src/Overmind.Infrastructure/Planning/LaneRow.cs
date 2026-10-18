namespace Overmind.Infrastructure.Planning;

public sealed record LaneRow(
    string Name,
    string? Contour,
    int? SerialOrder,
    int? Priority,
    int OpenFeatureCount,
    LaneHolder? Holder,
    IReadOnlyList<LaneFeatureSummary> Features);
