namespace Overmind.Infrastructure.Agent;

public interface IAgentBudgetStore
{
    Task<long> GetUsedAsync(DateOnly day, CancellationToken ct);

    Task<long> AddUsageAsync(DateOnly day, long tokens, CancellationToken ct);
}
