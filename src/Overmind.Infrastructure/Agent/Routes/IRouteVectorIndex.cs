namespace Overmind.Infrastructure.Agent.Routes;

public interface IRouteVectorIndex
{
    Task<RouteVectorMatch?> MatchTopAsync(string text, CancellationToken ct = default);

    Task UpsertAsync(Guid routeId, string patternText, RouteVectorPayload payload, CancellationToken ct = default);

    Task DeleteAsync(Guid routeId, CancellationToken ct = default);

    Task ClearAsync(CancellationToken ct = default);
}
