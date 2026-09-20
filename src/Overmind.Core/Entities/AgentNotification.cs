namespace Overmind.Core;

public sealed class AgentNotification
{
    private const string DefaultAgent = "agent";

    private AgentNotification()
    {
    }

    public Guid Id { get; private set; }
    public Guid? ProjectId { get; private set; }
    public NotificationKind Kind { get; private set; }
    public string? FeatureSlug { get; private set; }
    public string? Tab { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public string Agent { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public string? ReplyText { get; private set; }
    public DateTime? RepliedAt { get; private set; }

    public static IReadOnlySet<string> AllowedTabs { get; } =
        new HashSet<string>(["spec", "plan", "tasks", "result"], StringComparer.Ordinal);

    public static AgentNotification Create(
        NotificationKind kind,
        string text,
        Guid? projectId = null,
        string? featureSlug = null,
        string? tab = null,
        string? agent = null)
    {
        ThrowIfKindInvalid(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        string? normalizedTab = null;
        if (!string.IsNullOrWhiteSpace(tab))
        {
            normalizedTab = tab.Trim().ToLowerInvariant();
            if (!AllowedTabs.Contains(normalizedTab))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tab), tab, $"Tab '{tab}' is not a valid deep-link target.");
            }
        }

        return new AgentNotification
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Kind = kind,
            FeatureSlug = string.IsNullOrWhiteSpace(featureSlug) ? null : featureSlug.Trim(),
            Tab = normalizedTab,
            Text = text.Trim(),
            Agent = string.IsNullOrWhiteSpace(agent) ? DefaultAgent : agent.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static AgentNotification Restore(
        Guid id,
        Guid? projectId,
        NotificationKind kind,
        string? featureSlug,
        string? tab,
        string text,
        string agent,
        DateTime createdAt,
        DateTime? readAt = null,
        string? replyText = null,
        DateTime? repliedAt = null)
    {
        ThrowIfKindInvalid(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(agent);

        if ((replyText is null) != (repliedAt is null))
        {
            throw new ArgumentException(
                $"ReplyText and RepliedAt must be set together (replyText set: {replyText is not null}, repliedAt set: {repliedAt is not null}).",
                nameof(replyText));
        }

        return new AgentNotification
        {
            Id = id,
            ProjectId = projectId,
            Kind = kind,
            FeatureSlug = featureSlug,
            Tab = tab,
            Text = text,
            Agent = agent,
            CreatedAt = createdAt,
            ReadAt = readAt,
            ReplyText = replyText,
            RepliedAt = repliedAt,
        };
    }

    public void MarkRead()
    {
        ReadAt ??= DateTime.UtcNow;
    }

    public void Reply(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        ReplyText = text.Trim();
        RepliedAt = DateTime.UtcNow;
    }

    private static void ThrowIfKindInvalid(NotificationKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind), kind, $"Kind '{kind}' is not a valid notification kind.");
        }
    }
}
