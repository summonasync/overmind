namespace Overmind.Core;

public sealed class AgentSession
{
    public const int MaxIdentityLength = 32;
    public const string PlaceholderAlias = "agent";
    public const string MarshalRole = "marshal";
    public const string EmbeddedRole = "embedded";
    public const string DefaultRole = "worker";

    private AgentSession()
    {
    }

    public Guid Id { get; private set; }
    public string Alias { get; private set; } = string.Empty;
    public string Vendor { get; private set; } = string.Empty;
    public string Role { get; private set; } = DefaultRole;
    public DateTime StartedAt { get; private set; }
    public DateTime LastSeenAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public DateTime? ReplyCursorAt { get; private set; }

    public static AgentSession Create(string alias, string vendor, string role = DefaultRole)
    {
        var now = DateTime.UtcNow;
        return new AgentSession
        {
            Id = Guid.NewGuid(),
            Alias = NormalizeIdentity(alias, nameof(alias)),
            Vendor = NormalizeIdentity(vendor, nameof(vendor)),
            Role = NormalizeRole(role),
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
        DateTime? endedAt = null,
        string role = DefaultRole)
    {
        return new AgentSession
        {
            Id = id,
            Alias = NormalizeIdentity(alias, nameof(alias)),
            Vendor = NormalizeIdentity(vendor, nameof(vendor)),
            Role = NormalizeRole(role),
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

    public void AdvanceReplyCursor(DateTime at)
    {
        if (ReplyCursorAt is null || at > ReplyCursorAt)
        {
            ReplyCursorAt = at;
        }
    }

    public void SetRole(string role) => Role = NormalizeRole(role);

    public void RenameAlias(string alias)
    {
        if (!Alias.Equals(PlaceholderAlias, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Only the placeholder '{PlaceholderAlias}' session can be renamed; this session is '{Alias}'.");
        }

        Alias = NormalizeIdentity(alias, nameof(alias));
    }

    public void SetIdentity(string alias, string vendor)
    {
        var normalizedAlias = NormalizeIdentity(alias, nameof(alias));
        var normalizedVendor = NormalizeIdentity(vendor, nameof(vendor));

        Alias = normalizedAlias;
        Vendor = normalizedVendor;
    }

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

    public static string NormalizeRole(string role)
    {
        var trimmed = role?.Trim();
        if (trimmed is null)
        {
            return DefaultRole;
        }

        if (trimmed.Equals(MarshalRole, StringComparison.Ordinal))
        {
            return MarshalRole;
        }

        return trimmed.Equals(EmbeddedRole, StringComparison.Ordinal) ? EmbeddedRole : DefaultRole;
    }
}
