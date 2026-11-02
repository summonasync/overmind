namespace Overmind.Infrastructure.Agent;

public sealed record AgentRunSettings(string Alias, int PerTurnMaxTokens, int DailyBudget);
