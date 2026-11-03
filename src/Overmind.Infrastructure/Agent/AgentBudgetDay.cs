namespace Overmind.Infrastructure.Agent;

public static class AgentBudgetDay
{
    public static DateOnly Current() => DateOnly.FromDateTime(DateTime.UtcNow);
}
