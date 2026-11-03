using Microsoft.Extensions.Logging;

namespace Overmind.Infrastructure.Agent.Routes;

public sealed class RouteVectorIndexingStore(
    IAgentRouteStore inner,
    IRouteVectorIndex index,
    ILogger<RouteVectorIndexingStore> logger) : IAgentRouteStore
{
    public async Task<AgentRouteRecord> RecordAsync(AgentRouteRecording recording, CancellationToken ct = default)
    {
        var row = await inner.RecordAsync(recording, ct);
        await TryMirrorAsync(
            () => index.UpsertAsync(
                row.Id,
                row.Pattern,
                new RouteVectorPayload(row.Domain, row.TargetAgent, row.Status.ToString().ToLowerInvariant(), row.HitCount),
                ct),
            row.Id, "upsert", ct);
        return row;
    }

    public Task<IReadOnlyList<AgentRouteRecord>> HotHeadAsync(int maxResults, CancellationToken ct = default) =>
        inner.HotHeadAsync(maxResults, ct);

    public async Task<bool> PromoteAsync(Guid id, CancellationToken ct = default) =>
        await inner.PromoteAsync(id, ct);

    public async Task<bool> DemoteAsync(Guid id, CancellationToken ct = default)
    {
        var demoted = await inner.DemoteAsync(id, ct);
        if (demoted)
        {
            await TryMirrorAsync(() => index.DeleteAsync(id, ct), id, "delete", ct);
        }

        return demoted;
    }

    public async Task<int> ClearAsync(CancellationToken ct = default)
    {
        var removed = await inner.ClearAsync(ct);
        if (removed > 0)
        {
            await TryMirrorAsync(() => index.ClearAsync(ct), null, "clear", ct);
        }

        return removed;
    }

    private async Task TryMirrorAsync(
        Func<Task> operation, Guid? routeId, string operationName, CancellationToken ct)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Route vector index {Operation} failed for route {RouteId}; the SQLite ledger stays authoritative",
                operationName,
                routeId?.ToString() ?? "all");
        }
    }
}
