using Microsoft.EntityFrameworkCore;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

// The graph read door for the canvas: one human-door request that returns the whole field — nodes, edges,
// positions and counts — with the filters living in the URL so a reload reproduces the view. It sits outside
// /api/agent deliberately (spec R8): the field is the owner's own journal context, never an agent verb, and the
// route-table invariant that derives its cases from the agent prefix stays green because nothing here is a
// mutation under that prefix.
public static class GraphApi
{
    public static RouteGroupBuilder MapGraphApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapGet("/graph", ReadGraph);

        return group;
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
        if (focusRequested && field.Nodes.Count == 0)
        {
            return HttpErrors.NotFound(
                $"'focus' names no node in the graph: {focus}.",
                "UnknownGraphFocus");
        }

        return Results.Ok(GraphMapper.ToResponse(field));
    }
}
