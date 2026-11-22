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

// One write the canvas sends when a drag settles or a pin is lifted. Kind rides as the wire's
// camelCase name ("entry", "project", "feature") and is parsed, not bound, so an unknown kind
// answers a named refusal instead of a binder's generic 400.
public sealed record GraphPositionWriteDto(string Kind, Guid Id, double X, double Y, bool Pinned);

public sealed record GraphPositionsRequest(IReadOnlyList<GraphPositionWriteDto> Positions);

public sealed record GraphCountsDto(
    int Nodes,
    int Edges,
    int Orphans,
    int EntryNodes,
    int ProjectNodes,
    int FeatureNodes,
    // A NODE count, not a link count: how many of the nodes drawn here are flagged linked by the shared triage
    // predicate yet carry no edge in this payload, because every one of their links leaves this view (to a Done
    // feature the default hides). A node with two such links still counts once. It is the honest answer to "why
    // is this dot isolated when it is not an orphan" — the legend says "N notes here link to something outside
    // this view", never "N links lead outside", which this number cannot back. The dropped-edge count is not carried
    // here and is NOT derivable from this payload: a node hiding one link and a node hiding two produce identical
    // nodes, edges, orphans and IsolatedInView. Anyone who wants the link tally must surface it where the edges are
    // dropped (GraphReader), not reconstruct it downstream.
    int IsolatedInView);

public sealed record GraphResponse(
    IReadOnlyList<GraphNodeDto> Nodes,
    IReadOnlyList<GraphEdgeDto> Edges,
    IReadOnlyList<GraphPositionDto> Positions,
    GraphCountsDto Counts);
