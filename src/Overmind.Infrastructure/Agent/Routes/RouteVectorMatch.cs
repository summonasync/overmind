namespace Overmind.Infrastructure.Agent.Routes;

public sealed record RouteVectorMatch(Guid RouteId, string Domain, string? Target, float Score);
