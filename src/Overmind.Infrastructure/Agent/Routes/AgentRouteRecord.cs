namespace Overmind.Infrastructure.Agent.Routes;

public sealed record AgentRouteRecord(
    Guid Id,
    string Pattern,
    string Domain,
    string? TargetAgent,
    string MissReason,
    AgentRouteStatus Status,
    int HitCount,
    AgentRouteSource Source,
    DateTimeOffset LastHitAt,
    DateTimeOffset CreatedAt);
