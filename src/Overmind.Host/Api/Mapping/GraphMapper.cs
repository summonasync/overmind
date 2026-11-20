using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Entries;

namespace Overmind.Host.Api.Mapping;

// GraphField onto the wire. The projection hands back an unordered edge set (its collapse is a dictionary over
// pairs), so the two collections are ordered here on their identity key: the payload the canvas reads is stable
// across loads, and a response-shape test never flakes on link order.
public static class GraphMapper
{
    public static GraphResponse ToResponse(GraphField field) => new(
        [.. field.Nodes
            .OrderBy(node => node.Kind)
            .ThenBy(node => node.Label, StringComparer.Ordinal)
            .ThenBy(node => node.Id)
            .Select(ToNode)],
        [.. field.Edges
            .OrderBy(edge => edge.From.Kind)
            .ThenBy(edge => edge.From.Id)
            .ThenBy(edge => edge.To.Kind)
            .ThenBy(edge => edge.To.Id)
            .Select(ToEdge)],
        [.. field.Positions
            .OrderBy(position => position.Kind)
            .ThenBy(position => position.Id)
            .Select(ToPosition)],
        ToCounts(field));

    private static GraphNodeDto ToNode(GraphNode node) =>
        new(node.Kind, node.Id, node.Label, node.ProjectId, node.EntryType, node.FeatureStatus, node.IsOrphan);

    private static GraphEdgeDto ToEdge(GraphEdge edge) =>
        new(edge.From.Kind, edge.From.Id, edge.To.Kind, edge.To.Id, edge.HasManual);

    private static GraphPositionDto ToPosition(GraphPosition position) =>
        new(position.Kind, position.Id, position.X, position.Y);

    // A node the field marks as linked but that carries no rendered edge has every real link leaving this view —
    // it would draw as an isolated dot the legend cannot otherwise explain. Off-view is counted from the projection
    // it ships with, so the metric cannot disagree with the payload it describes.
    private static GraphCountsDto ToCounts(GraphField field)
    {
        var rendered = new HashSet<GraphNodeId>();
        foreach (var edge in field.Edges)
        {
            rendered.Add(edge.From);
            rendered.Add(edge.To);
        }

        var offView = field.Nodes.Count(node => !node.IsOrphan
            && !rendered.Contains(new GraphNodeId(node.Kind, node.Id)));

        var counts = field.Counts;
        return new GraphCountsDto(
            counts.Nodes, counts.Edges, counts.Orphans,
            counts.EntryNodes, counts.ProjectNodes, counts.FeatureNodes,
            offView);
    }
}
