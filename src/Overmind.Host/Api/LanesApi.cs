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
        human.MapPut("/projects/{projectId:guid}/planning/lanes", Set);

        var agent = routes.MapGroup("/api/agent");
        agent.MapGet("/projects/{projectId:guid}/planning/lanes", List);
        agent.MapPut("/projects/{projectId:guid}/planning/lanes", Set);

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

    private static LanesResponse ToResponse(IReadOnlyList<LaneRow> roster) => new(
        [.. roster.Select(lane => new LaneRowResponse(
            lane.Name,
            lane.Contour,
            lane.SerialOrder,
            lane.OpenFeatureCount,
            lane.Holder is { } holder
                ? new LaneHolderResponse(holder.Alias, holder.Vendor, holder.ClaimState)
                : null,
            [.. lane.Features.Select(f => new LaneFeatureResponse(
                f.Slug, f.Status, f.ClaimedByAlias, f.ClaimedByVendor, f.ClaimState))]))]);

    private static IReadOnlyList<LaneUpsert> ToUpserts(LaneUpsertRequest[] lanes) =>
    [
        .. lanes.Select(lane => new LaneUpsert(lane.Name, lane.Contour, lane.SerialOrder))
    ];
}
