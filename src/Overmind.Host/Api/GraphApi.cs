using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

// The graph read door for the canvas: one human-door request that returns the whole field — nodes, edges,
// positions and counts — with the filters living in the URL so a reload reproduces the view. It sits outside
// /api/agent deliberately (spec R8): the field is the owner's own journal context, never an agent verb, and the
// route-table invariant that derives its cases from the agent prefix stays green because nothing here is a
// mutation under that prefix. The position write shares the placement: moving the owner's dots is the owner's
// act, so the PUT lives on this group too and AgentWriteSurfaceCoverageApiTests keeps the agent twin unmapped.
public static class GraphApi
{
    public static RouteGroupBuilder MapGraphApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapGet("/graph", ReadGraph);
        group.MapPut("/graph/positions", SavePositions);

        return group;
    }

    private static async Task<IResult> SavePositions(
        GraphPositionsRequest? request,
        GraphNodePositionWriter writer,
        CancellationToken ct)
    {
        if (GraphPositionsParser.Parse(request, out var writes) is { } failure)
        {
            return failure;
        }

        await writer.SaveAsync(writes, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ReadGraph(
        string[]? type,
        string? project,
        string? focus,
        string? depth,
        string? orphansOnly,
        string? search,
        string? includeDone,
        OvermindDbContext db,
        GraphReader reader,
        CancellationToken ct)
    {
        if (GraphQueryFilter.ValidateEntryTypes(type, out var entryTypes) is { } typeFailure)
        {
            return typeFailure;
        }

        if (GraphQueryFilter.ValidateFlag(orphansOnly, "orphansOnly", out var orphans) is { } orphansFailure)
        {
            return orphansFailure;
        }

        if (GraphQueryFilter.ValidateFlag(includeDone, "includeDone", out var includeDoneFeatures) is { } doneFailure)
        {
            return doneFailure;
        }

        if (GraphQueryFilter.ValidateFocus(focus, depth, out var focusNode, out var hops, out var focusRequested)
            is { } focusFailure)
        {
            return focusFailure;
        }

        GraphQueryFilter.ValidateSearch(search, out var term);

        // The projection answers orphans-only before it reaches the focus window, so the two together would drop
        // the focus silently. Orphans have no rendered links by definition, so a focus walk from one is a request
        // the view cannot answer — it names itself instead of quietly returning the orphan pile.
        if (orphans && focusRequested)
        {
            return HttpErrors.BadRequest(
                "'orphansOnly' returns the link-free pile, so it cannot combine with 'focus'.",
                "OrphansWithFocus");
        }

        var (projectId, projectFailure) = await EntryStreamFilter.ResolveProjectAsync(db, project, ct);
        if (projectFailure is not null)
        {
            return projectFailure;
        }

        var field = await reader.LoadAsync(new GraphReadRequest
        {
            IncludeDoneFeatures = includeDoneFeatures,
            OrphansOnly = orphans,
            ProjectId = projectId,
            EntryTypes = entryTypes,
            Search = term,
            Focus = focusNode,
            Depth = hops,
        }, ct);

        // The projection windows from a focus it recognises and answers empty for one it does not; an empty field
        // that was asked to centre on a node is a bad focus, not a field with nothing in it, so it names itself.
        // It also has to say WHICH half went wrong: when the node is real and a co-filter dropped it, blaming the
        // node sends the owner chasing the dot they clicked (review-237-03 m5).
        if (focusRequested && field.Nodes.Count == 0)
        {
            if (focusNode is { } node && await IsFieldNode(db, node, ct))
            {
                var coFilters = ActiveCoFilters(type, project, term, includeDoneFeatures);
                return HttpErrors.NotFound(
                    $"'focus' names a node that exists ({focus}), but this view filtered it out" +
                    (coFilters.Count > 0
                        ? $" — the filter to widen: {string.Join(", ", coFilters)}."
                        : " — no narrowing filter is active, so the node is outside the drawn universe."),
                    "UnknownGraphFocus");
            }

            return HttpErrors.NotFound(
                $"'focus' names no node in the graph: {focus}.",
                "UnknownGraphFocus");
        }

        return Results.Ok(GraphMapper.ToResponse(field));
    }

    // Existence in the node universe, not in this view: the same three entry types R1 fixes, the same live-row
    // hygiene triage applies, and a feature the default filter hides is still a node the canvas can centre on.
    private static async Task<bool> IsFieldNode(OvermindDbContext db, GraphNodeId node, CancellationToken ct)
    {
        return node.Kind switch
        {
            EntryLinkKind.Entry => await db.ContextEntries.AnyAsync(entry =>
                entry.Id == node.Id
                && GraphReadRequest.NodeEntryTypes.Contains(entry.Type)
                && entry.State == ContextEntry.ActiveState
                && entry.Type != EntryType.Tombstone, ct),
            EntryLinkKind.Project => await db.Projects.AnyAsync(project => project.Id == node.Id, ct),
            EntryLinkKind.Feature => await db.PlanningFeatures.AnyAsync(feature => feature.Id == node.Id, ct),
            _ => false,
        };
    }

    // Every filter that could have removed a real node from the field, named as the query parameter the owner
    // actually set. orphansOnly never reaches here — it is refused as contradictory with focus before the read.
    private static List<string> ActiveCoFilters(
        string[]? type, string? project, string? term, bool includeDoneFeatures)
    {
        var names = new List<string>();

        if (type is { Length: > 0 })
        {
            names.Add("'type'");
        }

        if (!string.IsNullOrWhiteSpace(project))
        {
            names.Add("'project'");
        }

        if (term is not null)
        {
            names.Add("'search'");
        }

        if (!includeDoneFeatures)
        {
            names.Add("'includeDone' (Done features are hidden until widened)");
        }

        return names;
    }
}
