namespace Overmind.Core;

public sealed class WaveWatermark
{
    public const int MaxIdentityLength = AgentSession.MaxIdentityLength;

    private WaveWatermark()
    {
    }

    public Guid ProjectId { get; private set; }
    public string Alias { get; private set; } = string.Empty;
    public DateTime LastReadAt { get; private set; }

    public static WaveWatermark Create(Guid projectId, string alias, DateTime at) => new()
    {
        ProjectId = projectId,
        Alias = NormalizeAlias(alias),
        LastReadAt = at,
    };

    public void Advance(DateTime at)
    {
        if (at > LastReadAt)
        {
            LastReadAt = at;
        }
    }

    public static string NormalizeAlias(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        var trimmed = alias.Trim().ToLowerInvariant();
        if (trimmed.Length > MaxIdentityLength)
        {
            throw new ArgumentException(
                $"Agent identity '{alias.Trim()}' exceeds the {MaxIdentityLength} character cap.", nameof(alias));
        }

        return trimmed;
    }
}
