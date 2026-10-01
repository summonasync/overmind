namespace Overmind.Core;

public sealed class AgentSession
{
    public const int MaxIdentityLength = 32;

    private AgentSession()
    {
    }

    public Guid Id { get; private set; }
    public string Alias { get; private set; } = string.Empty;
    public string Vendor { get; private set; } = string.Empty;
    public DateTime StartedAt { get; private set; }
    public DateTime LastSeenAt { get; private set; }
    public DateTime? EndedAt { get; private set; }

    public static AgentSession Create(string alias, string vendor)
    {
        var now = DateTime.UtcNow;
        return new AgentSession
        {
            Id = Guid.NewGuid(),
            Alias = NormalizeIdentity(alias, nameof(alias)),
            Vendor = NormalizeIdentity(vendor, nameof(vendor)),
            StartedAt = now,
            LastSeenAt = now
        };
    }

    public static AgentSession Restore(
        Guid id,
        string alias,
        string vendor,
        DateTime startedAt,
        DateTime lastSeenAt,
        DateTime? endedAt = null)
    {
        return new AgentSession
        {
            Id = id,
            Alias = NormalizeIdentity(alias, nameof(alias)),
            Vendor = NormalizeIdentity(vendor, nameof(vendor)),
            StartedAt = startedAt,
            LastSeenAt = lastSeenAt,
            EndedAt = endedAt
        };
    }

    public void Touch()
    {
        if (EndedAt is not null)
        {
            return;
        }

        LastSeenAt = DateTime.UtcNow;
    }

    public void End() => EndedAt ??= DateTime.UtcNow;

    public AgentSessionLiveness Liveness(DateTime now, int staleThresholdMinutes)
    {
        if (EndedAt is not null)
        {
            return AgentSessionLiveness.Ended;
        }

        if (staleThresholdMinutes > 0 && now - LastSeenAt > TimeSpan.FromMinutes(staleThresholdMinutes))
        {
            return AgentSessionLiveness.Stale;
        }

        return AgentSessionLiveness.Alive;
    }

    private static string NormalizeIdentity(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Agent identity must not be empty.", paramName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxIdentityLength)
        {
            throw new ArgumentException(
                $"Agent identity '{trimmed}' exceeds the {MaxIdentityLength} character cap.", paramName);
        }

        return trimmed;
    }
}
