namespace Overmind.Core;

public sealed class PlanningDirective
{
    public const int MaxTextLength = 2000;
    public const int MaxIdentityLength = AgentSession.MaxIdentityLength;

    private PlanningDirective()
    {
    }

    public Guid Id { get; private set; }
    public Guid PlanningFeatureId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public string? AuthorAlias { get; private set; }
    public string? AuthorVendor { get; private set; }
    public Guid? AuthorSessionId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? AckByAlias { get; private set; }
    public string? AckVendor { get; private set; }
    public DateTime? AckAt { get; private set; }

    public bool Acked => AckAt is not null;

    public static PlanningDirective Create(
        Guid planningFeatureId, string text, string? authorAlias, string? authorVendor, Guid? authorSessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        // MaxTextLength is the soft cap of an imperative text — the API door rejects anything past
        // the grace ceiling with a preview instead of cutting, so this guard only catches bypasses.
        var hardCeiling = SoftCap.Ceiling(MaxTextLength);
        var trimmed = text.Trim();
        if (trimmed.Length > hardCeiling)
        {
            throw new ArgumentException(
                $"Directive text exceeds the {hardCeiling} character hard ceiling (soft cap {MaxTextLength}).",
                nameof(text));
        }

        return new PlanningDirective
        {
            Id = Guid.NewGuid(),
            PlanningFeatureId = planningFeatureId,
            Text = trimmed,
            AuthorAlias = NormalizeIdentity(authorAlias),
            AuthorVendor = NormalizeIdentity(authorVendor),
            AuthorSessionId = authorSessionId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Ack(string? alias, string? vendor, DateTime at)
    {
        if (Acked)
        {
            return;
        }

        AckByAlias = NormalizeIdentity(alias);
        AckVendor = NormalizeIdentity(vendor);
        AckAt = at;
    }

    private static string? NormalizeIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxIdentityLength)
        {
            throw new ArgumentException(
                $"Directive identity '{trimmed}' exceeds the {MaxIdentityLength} character cap.");
        }

        return trimmed;
    }
}
