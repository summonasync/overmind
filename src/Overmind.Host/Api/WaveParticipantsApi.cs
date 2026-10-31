using Microsoft.Extensions.Configuration;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Planning;
using Overmind.Infrastructure.Wave;

namespace Overmind.Host.Api;

public static class WaveParticipantsApi
{
    public static RouteGroupBuilder MapWaveParticipantsApi(this IEndpointRouteBuilder routes)
    {
        var human = routes.MapGroup("/api");
        human.MapGet("/projects/{projectId:guid}/wave/participants", Participants);

        var agent = routes.MapGroup("/api/agent");
        agent.MapGet("/projects/{projectId:guid}/wave/participants", Participants);

        return human;
    }

    private static async Task<IResult> Participants(
        Guid projectId,
        LaneService lanes,
        WaveParticipantService sessions,
        IConfiguration configuration,
        CancellationToken ct)
    {
        try
        {
            var roster = await lanes.ListLanesAsync(projectId, ct);
            var claimless = await sessions.ListClaimlessSessionsAsync(projectId, roster, ct);
            return Results.Ok(WaveParticipantMapper.ToResponse(
                roster, claimless, WaveOwnerDefaults.ResolveAlias(configuration)));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }
}
