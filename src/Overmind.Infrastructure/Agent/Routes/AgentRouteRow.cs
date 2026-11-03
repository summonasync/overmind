using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Agent.Routes;

public sealed class AgentRouteRow
{
    public const int MaxTargetAgentLength = 64;

    private AgentRouteRow()
    {
    }

    public Guid Id { get; private set; }
    public string Pattern { get; private set; } = string.Empty;
    public string Domain { get; private set; } = string.Empty;
    public string? TargetAgent { get; private set; }
    public string MissReason { get; private set; } = string.Empty;
    public AgentRouteStatus Status { get; private set; }
    public int HitCount { get; private set; }
    public AgentRouteSource Source { get; private set; }
    public DateTimeOffset LastHitAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static AgentRouteRow Create(
        string pattern,
        string domain,
        string missReason,
        AgentRouteSource source,
        DateTimeOffset now)
    {
        var normalized = AgentRouteText.Normalize(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalized);

        return new AgentRouteRow
        {
            Id = Guid.NewGuid(),
            Pattern = normalized,
            Domain = AgentRouteText.Clamp(domain, AgentRouteText.MaxDomainLength),
            MissReason = AgentRouteText.Clamp(missReason, AgentRouteText.MaxMissReasonLength),
            Status = source == AgentRouteSource.Manual ? AgentRouteStatus.Confirmed : AgentRouteStatus.Provisional,
            HitCount = 1,
            Source = source,
            LastHitAt = now,
            CreatedAt = now,
        };
    }

    public void RegisterHit() => HitCount++;

    public void Touch(DateTimeOffset now) => LastHitAt = now;

    public void RefreshDomain(string domain) => Domain = domain;

    public void RefreshMissReason(string missReason) => MissReason = missReason;

    public void Promote() => Status = AgentRouteStatus.Confirmed;

    public void Demote() => Status = AgentRouteStatus.Provisional;
}
