namespace Overmind.Infrastructure.Agent.Routes;

public sealed record RouteVectorPayload(string Domain, string? Target, string Status, int HitCount);
