using Overmind.Infrastructure.Agent;

namespace Overmind.Host.Api.Contracts;

public sealed record AgentUsageResponse(
    int DailyBudget,
    long SpentToday,
    long? RemainingToday,
    int PerTurnMaxTokens)
{
    public static AgentUsageResponse From(AgentRunSettings settings, long spentToday) => new(
        settings.DailyBudget,
        spentToday,
        settings.DailyBudget > 0 ? Math.Max(0, settings.DailyBudget - spentToday) : null,
        settings.PerTurnMaxTokens);
}
