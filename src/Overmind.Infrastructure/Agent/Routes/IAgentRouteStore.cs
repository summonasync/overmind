namespace Overmind.Infrastructure.Agent.Routes;

public interface IAgentRouteStore
{
    Task<AgentRouteRecord> RecordAsync(AgentRouteRecording recording, CancellationToken ct = default);

    Task<IReadOnlyList<AgentRouteRecord>> HotHeadAsync(int maxResults, CancellationToken ct = default);

    Task<bool> PromoteAsync(Guid id, CancellationToken ct = default);

    Task<bool> DemoteAsync(Guid id, CancellationToken ct = default);

    Task<int> ClearAsync(CancellationToken ct = default);
}
