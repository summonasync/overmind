using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class LanesApi
{
    public static RouteGroupBuilder MapLanesApi(this IEndpointRouteBuilder routes)
    {
        var human = routes.MapGroup("/api");
        human.MapGet("/projects/{projectId:guid}/planning/lanes", List);
        human.MapPost("/projects/{projectId:guid}/planning/lanes", Create);
        human.MapPut("/projects/{projectId:guid}/planning/lanes", Set);
        human.MapPut("/projects/{projectId:guid}/planning/lanes/{name}", Update);
        human.MapDelete("/projects/{projectId:guid}/planning/lanes/{name}", Delete);

        var agent = routes.MapGroup("/api/agent");
        agent.MapGet("/projects/{projectId:guid}/planning/lanes", List);
        agent.MapPost("/projects/{projectId:guid}/planning/lanes", Create);
        agent.MapPut("/projects/{projectId:guid}/planning/lanes", Set);
        agent.MapPut("/projects/{projectId:guid}/planning/lanes/{name}", Update);
        agent.MapDelete("/projects/{projectId:guid}/planning/lanes/{name}", Delete);

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

    internal static async Task<IResult> Create(
        Guid projectId,
        LaneUpsertRequest request,
        IValidator<LaneUpsertRequest> validator,
        LaneService lanes,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return await WriteAsync(projectId, ToUpsert(request), LaneWriteMode.Create, lanes, ct);
    }

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

    private static LaneUpsert ToUpsert(LaneUpsertRequest lane) =>
        new(lane.Name, lane.Contour, lane.SerialOrder, lane.Priority);

    private static IReadOnlyList<LaneUpsert> ToUpserts(LaneUpsertRequest[] lanes) =>
    [
        .. lanes.Select(lane => new LaneUpsert(lane.Name, lane.Contour, lane.SerialOrder, lane.Priority))
    ];
}
