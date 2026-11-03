namespace Overmind.Core;

public sealed class AgentRoute
{
    public const int MaxPatternLength = 300;
    public const int MaxDomainLength = 64;
    public const int MaxTargetAgentLength = 64;
    public const int MaxMissReasonLength = 200;
    public const int ConfirmationHits = 3;

    private AgentRoute()
    {
    }

    public Guid Id { get; private set; }
    public string Pattern { get; private set; } = string.Empty;
    public string Domain { get; private set; } = string.Empty;
    public string TargetAgent { get; private set; } = string.Empty;
    public string MissReason { get; private set; } = string.Empty;
    public AgentRouteStatus Status { get; private set; }
    public int HitCount { get; private set; }
    public DateTime LastHitAt { get; private set; }
    public AgentRouteSource Source { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static AgentRoute Create(
        string pattern,
        string domain,
        string targetAgent,
        string missReason,
        AgentRouteSource source)
    {
        var normalized = RequireCapped(pattern, MaxPatternLength, nameof(pattern));
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAgent);

        return new AgentRoute
        {
            Id = Guid.NewGuid(),
            Pattern = normalized,
            Domain = Cap(domain.Trim(), MaxDomainLength),
            TargetAgent = Cap(targetAgent.Trim(), MaxTargetAgentLength),
            MissReason = Cap((missReason ?? string.Empty).Trim(), MaxMissReasonLength),
            Status = source == AgentRouteSource.Manual ? AgentRouteStatus.Confirmed : AgentRouteStatus.Provisional,
            HitCount = 0,
            LastHitAt = DateTime.UtcNow,
            Source = source,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void RegisterHit()
    {
        HitCount++;
        LastHitAt = DateTime.UtcNow;
        if (Status == AgentRouteStatus.Provisional && HitCount >= ConfirmationHits)
        {
            Status = AgentRouteStatus.Confirmed;
        }
    }

    public void RefreshAnalysis(string domain, string targetAgent, string missReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAgent);

        Domain = Cap(domain.Trim(), MaxDomainLength);
        TargetAgent = Cap(targetAgent.Trim(), MaxTargetAgentLength);
        MissReason = Cap((missReason ?? string.Empty).Trim(), MaxMissReasonLength);
    }

    public void Promote() => Status = AgentRouteStatus.Confirmed;

    public void Demote() => Status = AgentRouteStatus.Provisional;

    private static string RequireCapped(string value, int maxLength, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Cap(value.Trim(), maxLength);
    }

    private static string Cap(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
