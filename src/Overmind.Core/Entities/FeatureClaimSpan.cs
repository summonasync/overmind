namespace Overmind.Core;

public sealed class FeatureClaimSpan
{
    private FeatureClaimSpan()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid FeatureId { get; private set; }
    public string Alias { get; private set; } = string.Empty;
    public string Vendor { get; private set; } = string.Empty;
    public Guid SessionId { get; private set; }
    public DateTime ClaimedAt { get; private set; }
    public DateTime? ReleasedAt { get; private set; }
    public bool ByHuman { get; private set; }

    public static FeatureClaimSpan Open(
        Guid projectId,
        Guid featureId,
        string alias,
        string vendor,
        Guid sessionId,
        DateTime claimedAt,
        bool byHuman = false)
    {
        return new FeatureClaimSpan
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            FeatureId = featureId,
            Alias = NormalizeIdentity(alias),
            Vendor = NormalizeIdentity(vendor),
            SessionId = sessionId,
            ClaimedAt = claimedAt,
            ByHuman = byHuman
        };
    }

    public void Close(DateTime releasedAt) => ReleasedAt = releasedAt;

    private static string NormalizeIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Claim identity must not be empty.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > AgentSession.MaxIdentityLength)
        {
            throw new ArgumentException(
                $"Claim identity '{trimmed}' exceeds the {AgentSession.MaxIdentityLength} character cap.");
        }

        return trimmed;
    }
}
