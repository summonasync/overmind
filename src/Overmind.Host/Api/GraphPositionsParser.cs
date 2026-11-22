using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Entries;

namespace Overmind.Host.Api;

// The PUT body, read once into writer input. The same posture as GraphQueryFilter: a value the
// store cannot honour answers a named 400 instead of a binder's generic complaint, and the node
// union stays three members — a task id is an edge target, never a position.
internal static class GraphPositionsParser
{
    public static IResult? Parse(GraphPositionsRequest? request, out List<GraphNodePositionWrite> writes)
    {
        writes = [];

        if (request?.Positions is not { Count: > 0 })
        {
            return HttpErrors.BadRequest(
                "'positions' must carry at least one write.",
                "InvalidGraphPositionsShape");
        }

        var parsed = new List<GraphNodePositionWrite>(request.Positions.Count);
        foreach (var row in request.Positions)
        {
            EntryLinkKind? kind = row.Kind?.ToLowerInvariant() switch
            {
                "entry" => EntryLinkKind.Entry,
                "project" => EntryLinkKind.Project,
                "feature" => EntryLinkKind.Feature,
                _ => null,
            };

            if (kind is not { } nodeKind)
            {
                return HttpErrors.BadRequest(
                    $"Unknown node kind '{row.Kind}'. Valid values: entry, project, feature.",
                    "UnknownGraphNodeKind");
            }

            if (row.Id == Guid.Empty)
            {
                return HttpErrors.BadRequest(
                    "A position write requires a node id.",
                    "InvalidGraphPositionsShape");
            }

            if (!double.IsFinite(row.X) || !double.IsFinite(row.Y))
            {
                return HttpErrors.BadRequest(
                    $"A coordinate must be a finite number; got ({row.X}, {row.Y}).",
                    "InvalidGraphPositionsShape");
            }

            parsed.Add(new GraphNodePositionWrite(nodeKind, row.Id, row.X, row.Y, row.Pinned));
        }

        writes = parsed;
        return null;
    }
}
