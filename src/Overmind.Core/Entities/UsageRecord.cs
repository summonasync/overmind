namespace Overmind.Core;

public sealed class UsageRecord
{
    public const int MaxModelLength = 100;

    private UsageRecord()
    {
    }

    public Guid Id { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string Alias { get; private set; } = string.Empty;
    public Guid SessionId { get; private set; }
    public DateTime Ts { get; private set; }
    public string Model { get; private set; } = string.Empty;
    public long InputTokens { get; private set; }
    public long OutputTokens { get; private set; }
    public long CacheReadTokens { get; private set; }
    public long CacheWriteTokens { get; private set; }

    public static UsageRecord Create(
        Guid? projectId,
        string alias,
        Guid sessionId,
        DateTime ts,
        string model,
        long inputTokens,
        long outputTokens,
        long cacheReadTokens = 0,
        long cacheWriteTokens = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(cacheReadTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(cacheWriteTokens);

        return new UsageRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Alias = NormalizeIdentity(alias),
            SessionId = sessionId,
            Ts = ts,
            Model = model.Trim(),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheReadTokens = cacheReadTokens,
            CacheWriteTokens = cacheWriteTokens
        };
    }

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
