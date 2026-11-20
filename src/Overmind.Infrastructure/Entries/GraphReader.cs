using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Entries;

// The single read door for the graph field. It composes nodes from three tables and links plus orphans from
// EntryLinkTriage, so the orphan pile the canvas draws is literally the set the stream filter calls unlinked —
// one definition, two consumers. No marker parsing lives here: a node exists only if its row exists, which is
// why an unresolved [[..]] can never surface as a node.
public sealed class GraphReader
{
    private readonly OvermindDbContext _db;
    private readonly EntryLinkTriage _triage;

    public GraphReader(OvermindDbContext db, EntryLinkTriage triage)
    {
        _db = db;
        _triage = triage;
    }

    public async Task<GraphField> LoadAsync(GraphReadRequest request, CancellationToken ct = default)
    {
        var entryTypes = EffectiveEntryTypes(request.EntryTypes);

        var entries = FilterEntries(entryTypes, request);
        var projects = FilterProjects(request);
        var features = FilterFeatures(request);

        var orphanEntries = (await _triage.WhereUnlinked(entries).Select(e => e.Id).ToListAsync(ct)).ToHashSet();
        var orphanProjects = (await _triage.WhereUnlinkedProjects(projects).Select(p => p.Id).ToListAsync(ct)).ToHashSet();
        var orphanFeatures = (await _triage.WhereUnlinkedFeatures(features).Select(f => f.Id).ToListAsync(ct)).ToHashSet();

        var nodes = new List<GraphNode>();
        nodes.AddRange(await ProjectNodes(entries, orphanEntries, ct));
        nodes.AddRange(await ProjectProjects(projects, orphanProjects, ct));
        nodes.AddRange(await ProjectFeatures(features, orphanFeatures, ct));

        var nodeIds = nodes.Select(node => new GraphNodeId(node.Kind, node.Id)).ToHashSet();

        if (request.OrphansOnly)
        {
            var orphanNodes = nodes.Where(node => node.IsOrphan).ToList();
            return new GraphField(orphanNodes, [], [], CountsOf(orphanNodes, edges: 0));
        }

        var edges = await BuildEdgesAsync(nodeIds, ct);

        if (request.Focus is { } focus && request.Depth is > 0 and { } depth)
        {
            var reachable = ReachableWithin(focus, depth, nodes, edges);
            nodes = nodes.Where(node => reachable.Contains(new GraphNodeId(node.Kind, node.Id))).ToList();
            var kept = nodes.Select(node => new GraphNodeId(node.Kind, node.Id)).ToHashSet();
            edges = [.. edges.Where(edge => kept.Contains(edge.From) && kept.Contains(edge.To))];
        }

        return new GraphField(nodes, edges, [], CountsOf(nodes, edges.Count));
    }

    private static EntryType[] EffectiveEntryTypes(IReadOnlyList<EntryType>? requested)
    {
        if (requested is not { Count: > 0 })
        {
            return GraphReadRequest.NodeEntryTypes;
        }

        EntryType[] narrowed = [.. GraphReadRequest.NodeEntryTypes.Where(requested.Contains)];
        return narrowed.Length > 0 ? narrowed : GraphReadRequest.NodeEntryTypes;
    }

    private IQueryable<ContextEntry> FilterEntries(EntryType[] entryTypes, GraphReadRequest request)
    {
        var query = _db.ContextEntries
            .Where(entry =>
                entryTypes.Contains(entry.Type)
                && entry.State == ContextEntry.ActiveState
                && entry.Type != EntryType.Tombstone);

        if (request.ProjectId is { } projectId)
        {
            query = query.Where(entry => entry.ProjectId == projectId);
        }

        if (LikePattern(request.Search) is { } pattern)
        {
            query = query.Where(entry => EF.Functions.Like(entry.Title, pattern, "\\"));
        }

        return query;
    }

    private IQueryable<Project> FilterProjects(GraphReadRequest request)
    {
        var query = _db.Projects.AsQueryable();

        if (request.ProjectId is { } projectId)
        {
            query = query.Where(project => project.Id == projectId);
        }

        if (LikePattern(request.Search) is { } pattern)
        {
            query = query.Where(project => EF.Functions.Like(project.Name, pattern, "\\"));
        }

        return query;
    }

    private IQueryable<PlanningFeature> FilterFeatures(GraphReadRequest request)
    {
        var query = _db.PlanningFeatures.AsQueryable();

        if (!request.IncludeDoneFeatures)
        {
            query = query.Where(feature => feature.Status != PlanningFeatureStatus.Done);
        }

        if (request.ProjectId is { } projectId)
        {
            query = query.Where(feature => feature.ProjectId == projectId);
        }

        if (LikePattern(request.Search) is { } pattern)
        {
            query = query.Where(feature => EF.Functions.Like(feature.Title, pattern, "\\"));
        }

        return query;
    }

    // A search term becomes a LIKE pattern that matches a substring, case-insensitive for ASCII as SQLite's LIKE
    // is; a term that is only wildcards or blank yields null so the caller skips the filter rather than matching
    // the literal '%'. The escape character keeps a real % or _ in the term as itself.
    private static string? LikePattern(string? search)
    {
        var value = search?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var escaped = value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return $"%{escaped}%";
    }

    private static async Task<IReadOnlyList<GraphNode>> ProjectNodes(
        IQueryable<ContextEntry> entries, HashSet<Guid> orphans, CancellationToken ct)
    {
        var rows = await entries
            .OrderBy(entry => entry.UpdatedAt)
            .ThenBy(entry => entry.Id)
            .Select(entry => new { entry.Id, entry.Title, entry.ProjectId, entry.Type })
            .ToListAsync(ct);

        return
        [
            .. rows.Select(row => new GraphNode
            {
                Kind = EntryLinkKind.Entry,
                Id = row.Id,
                Label = row.Title,
                ProjectId = row.ProjectId,
                EntryType = row.Type,
                IsOrphan = orphans.Contains(row.Id)
            })
        ];
    }

    private static async Task<IReadOnlyList<GraphNode>> ProjectProjects(
        IQueryable<Project> projects, HashSet<Guid> orphans, CancellationToken ct)
    {
        var rows = await projects
            .OrderBy(project => project.CreatedAt)
            .ThenBy(project => project.Id)
            .Select(project => new { project.Id, project.Name })
            .ToListAsync(ct);

        return
        [
            .. rows.Select(row => new GraphNode
            {
                Kind = EntryLinkKind.Project,
                Id = row.Id,
                Label = row.Name,
                ProjectId = row.Id,
                IsOrphan = orphans.Contains(row.Id)
            })
        ];
    }

    private static async Task<IReadOnlyList<GraphNode>> ProjectFeatures(
        IQueryable<PlanningFeature> features, HashSet<Guid> orphans, CancellationToken ct)
    {
        var rows = await features
            .OrderBy(feature => feature.QueuePosition)
            .ThenBy(feature => feature.Id)
            .Select(feature => new { feature.Id, feature.Title, feature.ProjectId, feature.Status })
            .ToListAsync(ct);

        return
        [
            .. rows.Select(row => new GraphNode
            {
                Kind = EntryLinkKind.Feature,
                Id = row.Id,
                Label = row.Title,
                ProjectId = row.ProjectId,
                FeatureStatus = row.Status,
                IsOrphan = orphans.Contains(row.Id)
            })
        ];
    }

    private async Task<List<GraphEdge>> BuildEdgesAsync(HashSet<GraphNodeId> nodeIds, CancellationToken ct)
    {
        // The edge universe is triage's; the only thing added here is that an endpoint must be a node in this
        // field. A link to a Done feature hidden by the default filter, or a Task the canvas never draws, drops
        // out — but the source's IsOrphan flag, computed from the same triage predicate, stays "linked", because
        // it is genuinely connected to something real that the view is merely concealing.
        var rows = await _triage.Edges()
            .Select(link => new { link.FromEntryId, link.ToKind, link.ToId, link.Origin })
            .ToListAsync(ct);

        // The canvas draws undirected links, so a pair is collapsed regardless of which way each stored row
        // points. A note named in a body and the note that names it back are two rows over one pair — and a
        // manual edge in one direction with a derived edge in the other (R2) is the same case. The unique index
        // forbids two rows for one *directed* pair, so the only duplicates here are opposite directions.
        var collapsed = new Dictionary<(GraphNodeId A, GraphNodeId B), bool>();
        foreach (var row in rows)
        {
            var from = new GraphNodeId(EntryLinkKind.Entry, row.FromEntryId);
            var to = new GraphNodeId(row.ToKind, row.ToId);
            if (!nodeIds.Contains(from) || !nodeIds.Contains(to))
            {
                continue;
            }

            var pair = Order(from, to);
            collapsed.TryGetValue(pair, out var manual);
            collapsed[pair] = manual || row.Origin == EntryLinkOrigin.Manual;
        }

        return
        [
            .. collapsed.Select(entry => new GraphEdge(entry.Key.A, entry.Key.B, entry.Value))
        ];
    }

    private static (GraphNodeId A, GraphNodeId B) Order(GraphNodeId x, GraphNodeId y) =>
        Compare(x, y) <= 0 ? (x, y) : (y, x);

    private static int Compare(GraphNodeId x, GraphNodeId y)
    {
        var byKind = ((int)x.Kind).CompareTo((int)y.Kind);
        return byKind != 0 ? byKind : x.Id.CompareTo(y.Id);
    }

    private static HashSet<GraphNodeId> ReachableWithin(
        GraphNodeId focus, int depth, IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges)
    {
        var reached = new HashSet<GraphNodeId>();
        if (!nodes.Any(node => new GraphNodeId(node.Kind, node.Id) == focus))
        {
            return reached;
        }

        var adjacency = new Dictionary<GraphNodeId, List<GraphNodeId>>();
        foreach (var edge in edges)
        {
            AddNeighbour(adjacency, edge.From, edge.To);
            AddNeighbour(adjacency, edge.To, edge.From);
        }

        var frontier = new List<GraphNodeId> { focus };
        reached.Add(focus);

        for (var hop = 0; hop < depth; hop++)
        {
            var next = new List<GraphNodeId>();
            foreach (var current in frontier)
            {
                if (!adjacency.TryGetValue(current, out var neighbours))
                {
                    continue;
                }

                foreach (var neighbour in neighbours)
                {
                    if (reached.Add(neighbour))
                    {
                        next.Add(neighbour);
                    }
                }
            }

            frontier = next;
            if (frontier.Count == 0)
            {
                break;
            }
        }

        return reached;
    }

    private static void AddNeighbour(
        Dictionary<GraphNodeId, List<GraphNodeId>> adjacency, GraphNodeId from, GraphNodeId to)
    {
        if (!adjacency.TryGetValue(from, out var neighbours))
        {
            neighbours = [];
            adjacency[from] = neighbours;
        }

        neighbours.Add(to);
    }

    private static GraphCounts CountsOf(IReadOnlyList<GraphNode> nodes, int edges) => new(
        Nodes: nodes.Count,
        Edges: edges,
        Orphans: nodes.Count(node => node.IsOrphan),
        EntryNodes: nodes.Count(node => node.Kind == EntryLinkKind.Entry),
        ProjectNodes: nodes.Count(node => node.Kind == EntryLinkKind.Project),
        FeatureNodes: nodes.Count(node => node.Kind == EntryLinkKind.Feature));
}
