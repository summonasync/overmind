using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Agent;

namespace Overmind.Host.Api;

public static class AgentUsageApi
{
    public static RouteGroupBuilder MapAgentUsageApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapAgentGroup();
        group.MapGet("/usage", GetUsage);
        return group;
    }

    private static async Task<IResult> GetUsage(
        IAgentRunSettingsProvider settingsProvider,
        IAgentBudgetStore budget,
        CancellationToken ct)
    {
        var settings = await settingsProvider.GetAsync(ct);
        var spentToday = await budget.GetUsedAsync(AgentBudgetDay.Current(), ct);
        return Results.Ok(AgentUsageResponse.From(settings, spentToday));
    }
}
