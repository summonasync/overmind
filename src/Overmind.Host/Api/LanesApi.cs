using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class LanesApi
{
    private const string LanePathPrefix = "/projects/{projectId:guid}/";
    private const string CollectionLiteral = "planning/lanes";
    private const string RosterLiteral = "planning/lanes-roster";

    // The whole-roster writer lives on its own sibling literal: a client drops a `.` segment before the wire
    // (RFC 3986 reference resolution), so `PUT .../lanes/.` arrives as `PUT .../lanes/` and used to swap the
    // roster from a URL shaped like one-lane surgery. The collapse-writer is gone; a collapsed URL now lands
    // on the tombstone below, which binds no body and calls no service (LaneRouteShapeTests, RosterShapedPut_*).
    // Full rationale: CONTEXT.md, "MCP planning surface" (143). Do not point `Set` back at the collection.
    public static RouteGroupBuilder MapLanesApi(this IEndpointRouteBuilder routes)
    {
        var human = routes.MapGroup("/api");
        human.MapGet(LanePathPrefix + CollectionLiteral, List);
        human.MapPost(LanePathPrefix + CollectionLiteral, CreateRefused);
        human.MapPut(LanePathPrefix + CollectionLiteral, RosterWriterRetired);
        human.MapPut(LanePathPrefix + RosterLiteral, Set);
        human.MapPut(LanePathPrefix + CollectionLiteral + "/{name}", Update);
        human.MapDelete(LanePathPrefix + CollectionLiteral + "/{name}", Delete);

        var agent = routes.MapGroup("/api/agent");
        agent.MapGet(LanePathPrefix + CollectionLiteral, List);
        agent.MapPost(LanePathPrefix + CollectionLiteral, CreateRefused);
        agent.MapPut(LanePathPrefix + CollectionLiteral, RosterWriterRetired);
        agent.MapPut(LanePathPrefix + RosterLiteral, Set);
        agent.MapPut(LanePathPrefix + CollectionLiteral + "/{name}", Update);
        agent.MapDelete(LanePathPrefix + CollectionLiteral + "/{name}", Delete);

        return human;
    }

    internal static async Task<IResult> List(Guid projectId, LaneService lanes, CancellationToken ct)
    {
        try
        {
            var roster = await lanes.ListLanesAsync(projectId, ct);
            return Results.Ok(ToResponse(roster));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static IResult CreateRefused(HttpRequest request) =>
        PlanningErrors.Map(PlanningException.LanePostBirthRestricted(request.Path.ToString().TrimEnd('/')));

    internal static async Task<IResult> Set(
        Guid projectId,
        SetLanesRequest request,
        IValidator<SetLanesRequest> validator,
        LaneService lanes,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        try
        {
            var roster = await lanes.SetLanesAsync(projectId, ToUpserts(request.Lanes), ct);
            return Results.Ok(ToResponse(roster));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
        catch (ArgumentException ex)
        {
            return HttpErrors.BadRequest(ex.Message);
        }
    }

    internal static IResult RosterWriterRetired(HttpRequest request)
    {
        var retiredPath = request.Path.ToString().TrimEnd('/');
        var rosterPath = retiredPath[..(retiredPath.Length - CollectionLiteral.Length)] + RosterLiteral;
        return PlanningErrors.Map(PlanningException.LaneRosterWriteRetired(retiredPath, rosterPath));
    }

    internal static async Task<IResult> Update(
        Guid projectId,
        string name,
        UpdateLaneRequest request,
        IValidator<UpdateLaneRequest> validator,
        LaneService lanes,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return await WriteAsync(
            projectId,
            new LaneUpsert(name, request.Contour, request.SerialOrder, request.Priority),
            LaneWriteMode.Update,
            lanes,
            ct);
    }

    internal static async Task<IResult> Delete(
        Guid projectId,
        string name,
        LaneService lanes,
        CancellationToken ct)
    {
        try
        {
            var roster = await lanes.DeleteLaneAsync(projectId, name, ct);
            return Results.Ok(ToResponse(roster));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
        catch (ArgumentException ex)
        {
            return HttpErrors.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> WriteAsync(
        Guid projectId, LaneUpsert lane, LaneWriteMode mode, LaneService lanes, CancellationToken ct)
    {
        try
        {
            var roster = await lanes.UpsertLaneAsync(projectId, lane, mode, ct);
            return Results.Ok(ToResponse(roster));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
        catch (ArgumentException ex)
        {
            return HttpErrors.BadRequest(ex.Message);
        }
    }

    private static LanesResponse ToResponse(IReadOnlyList<LaneRow> roster) => new(
        [.. roster.Select(lane => new LaneRowResponse(
            lane.Name,
            lane.Contour,
            lane.SerialOrder,
            lane.Priority,
            lane.OpenFeatureCount,
            lane.Holder is { } holder
                ? new LaneHolderResponse(holder.Alias, holder.Vendor, holder.ClaimState)
                : null,
            [.. lane.Features.Select(f => new LaneFeatureResponse(
                f.Slug, f.Status, f.ClaimedByAlias, f.ClaimedByVendor, f.ClaimState))]))]);

    private static IReadOnlyList<LaneUpsert> ToUpserts(LaneUpsertRequest[] lanes) =>
    [
        .. lanes.Select(lane => new LaneUpsert(lane.Name, lane.Contour, lane.SerialOrder, lane.Priority))
    ];
}
