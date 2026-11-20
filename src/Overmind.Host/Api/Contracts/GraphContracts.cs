using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

// The graph read payload, mapped straight off GraphField. The list shapes and the counts are the same numbers the
// projection computed, so an aria-label or a legend can be honest without a second request — and every node an edge
// names is already in Nodes, so the canvas never issues a per-node follow-up to draw a link.
public sealed record GraphNodeDto(
    EntryLinkKind Kind,
    Guid Id,
    string Label,
    Guid ProjectId,
    EntryType? EntryType,
    PlanningFeatureStatus? FeatureStatus,
    bool Orphan);

// A link addressed by both endpoints' kind and id, collapsed to one row per undirected pair. A manual edge and a
// derived edge over the same pair arrive as one row with HasManual set.
public sealed record GraphEdgeDto(
    EntryLinkKind FromKind,
    Guid FromId,
    EntryLinkKind ToKind,
    Guid ToId,
    bool HasManual);

// A pinned layout coordinate. Empty until T8 lands the position entity; the shape is here now so the payload does
// not change when positions start arriving.
public sealed record GraphPositionDto(
    EntryLinkKind Kind,
    Guid Id,
    double X,
    double Y);

public sealed record GraphCountsDto(
    int Nodes,
    int Edges,
    int Orphans,
    int EntryNodes,
    int ProjectNodes,
    int FeatureNodes,
    // Nodes drawn here that are linked in reality but whose every link leaves this view (to a Done feature the
    // default hides, or a task the canvas never draws). It is the honest answer to "why is this dot isolated when
    // it is not an orphan" — the legend can say "N notes link elsewhere" instead of the payload lying by omission.
    int OffViewLinks);

public sealed record GraphResponse(
    IReadOnlyList<GraphNodeDto> Nodes,
    IReadOnlyList<GraphEdgeDto> Edges,
    IReadOnlyList<GraphPositionDto> Positions,
    GraphCountsDto Counts);
