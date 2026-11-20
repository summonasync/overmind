using Overmind.Core;

namespace Overmind.Infrastructure.Entries;

// Addresses a node the way an edge target does — kind plus id — reused at both ends of a link and as the focus
// handle. Task-kind ids are edge targets, never nodes, so a GraphNodeId can name something the field won't show.
public readonly record struct GraphNodeId(EntryLinkKind Kind, Guid Id);

public sealed record GraphNode
{
    public required EntryLinkKind Kind { get; init; }
    public required Guid Id { get; init; }
    public required string Label { get; init; }
    public required Guid ProjectId { get; init; }

    // Set only for entry nodes: Journal/Decision/Idea, which is what the canvas colours by.
    public EntryType? EntryType { get; init; }

    // Set only for feature nodes, so the legend can split live from the widened Done.
    public PlanningFeatureStatus? FeatureStatus { get; init; }

    // No counting edge touches this node in either direction. Computed from the shared triage predicate, so
    // the orphan pile the canvas shows is the same set the stream filter calls unlinked.
    public required bool IsOrphan { get; init; }
}

public sealed record GraphEdge(GraphNodeId From, GraphNodeId To, bool HasManual);

// Pinned layout coordinates. The list is always empty until T8 lands the position entity; the record exists now
// so the read contract T3 returns does not change shape when positions arrive.
public sealed record GraphPosition(EntryLinkKind Kind, Guid Id, double X, double Y);

public sealed record GraphCounts(
    int Nodes,
    int Edges,
    int Orphans,
    int EntryNodes,
    int ProjectNodes,
    int FeatureNodes);

// One load of the whole field — nodes, edges, positions and the counts a legend or an aria-label needs to be
// honest without a second request.
public sealed record GraphField(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    IReadOnlyList<GraphPosition> Positions,
    GraphCounts Counts);
