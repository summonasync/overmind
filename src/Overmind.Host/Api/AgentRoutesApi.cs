using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Agent.Routes;

namespace Overmind.Host.Api;

public static class AgentRoutesApi
{
    public const int ListLimit = 500;

    public static RouteGroupBuilder MapAgentRoutesApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapGet("/agent/routes", List);
        group.MapDelete("/agent/routes", Clear);
        group.MapPost("/agent/routes/{id:guid}/promote", Promote);
        return group;
    }

    private static async Task<IResult> List(IAgentRouteStore store, CancellationToken ct) =>
        Results.Ok(new AgentRoutesResponse(
            [.. (await store.HotHeadAsync(ListLimit, ct)).Select(ToResponse)]));

    private static async Task<IResult> Clear(IAgentRouteStore store, CancellationToken ct) =>
        Results.Ok(new AgentRoutesClearResponse(await store.ClearAsync(ct)));

    private static async Task<IResult> Promote(Guid id, IAgentRouteStore store, CancellationToken ct)
    {
        var promoted = await store.PromoteAsync(id, ct);
        return promoted
            ? Results.Ok(new AgentRoutePromoteResponse(id, AgentRouteStatus.Confirmed))
            : HttpErrors.NotFound($"Agent route '{id}' was not found.");
    }

    private static AgentRouteResponse ToResponse(AgentRouteRecord route) =>
        new(route.Id, route.Pattern, route.Domain, route.TargetAgent, route.MissReason,
            route.Status, route.Source, route.HitCount, route.LastHitAt, route.CreatedAt);
}
