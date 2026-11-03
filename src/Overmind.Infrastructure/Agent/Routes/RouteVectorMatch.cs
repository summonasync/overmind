namespace Overmind.Infrastructure.Agent.Routes;

public sealed record RouteVectorMatch(Guid RouteId, string Domain, string? Target, float Score)
{
    public const float Threshold = 0.82f;

    public bool IsRoutable => Score >= Threshold;
}
