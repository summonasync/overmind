using Overmind.Core;

namespace Overmind.Infrastructure.Entries;

// The filter set spec R1 says travels in the query string, expressed once so the endpoint (T3) maps params onto
// it and never re-derives the read. Every field narrows the field; none of them can widen the node universe past
// the three entry types R1 fixes.
public sealed record GraphReadRequest
{
    // Features default to open (Draft/Ready/InProgress/Review). Done features are the graveyard R1 keeps off the
    // first load; this flag is the explicit widening filter.
    public bool IncludeDoneFeatures { get; init; }

    public bool OrphansOnly { get; init; }

    // Narrows to one project's field. The project node itself is shown; every other node is kept only if it
    // belongs to this project.
    public Guid? ProjectId { get; init; }

    // Narrows the entry nodes to a subset of {Journal, Decision, Idea}. Null means all three; anything outside
    // that set is ignored rather than honoured, because the non-node types are not nodes in v1.
    public IReadOnlyList<EntryType>? EntryTypes { get; init; }

    // Case-insensitive substring over a node's label (title, or project name).
    public string? Search { get; init; }

    // When both are set, keeps only nodes within Depth hops of Focus over the rendered edges. Focus is always
    // kept if it is a node; an unknown focus yields an empty field for the endpoint to reject.
    public GraphNodeId? Focus { get; init; }
    public int? Depth { get; init; }

    public static readonly EntryType[] NodeEntryTypes = [EntryType.Journal, EntryType.Decision, EntryType.Idea];
}
