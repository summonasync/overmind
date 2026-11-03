using Overmind.Infrastructure.Agent.Routes;

namespace Overmind.Host.Api.Contracts;

public sealed record AgentRouteResponse(
    Guid Id,
    string Pattern,
    string Domain,
    string? TargetAgent,
    string MissReason,
    AgentRouteStatus Status,
    AgentRouteSource Source,
    int HitCount,
    DateTimeOffset LastHitAt,
    DateTimeOffset CreatedAt);

public sealed record AgentRoutesResponse(IReadOnlyList<AgentRouteResponse> Routes);

public sealed record AgentRoutesClearResponse(int Deleted);

public sealed record AgentRoutePromoteResponse(Guid Id, AgentRouteStatus Status);
