namespace Overmind.Core;

public sealed class EntryAck
{
    public const int MaxIdentityLength = AgentSession.MaxIdentityLength;

    private EntryAck()
    {
    }

    public Guid Id { get; private set; }
    public Guid ContextEntryId { get; private set; }
    public string? Alias { get; private set; }
    public string? Vendor { get; private set; }
    public DateTime At { get; private set; }

    public static EntryAck Create(Guid contextEntryId, string? alias, string? vendor, DateTime at)
    {
        return new EntryAck
        {
            Id = Guid.NewGuid(),
            ContextEntryId = contextEntryId,
            Alias = NormalizeIdentity(alias),
            Vendor = NormalizeIdentity(vendor),
            At = at
        };
    }

    public static string? NormalizeIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxIdentityLength)
        {
            throw new ArgumentException(
                $"Entry ack identity '{trimmed}' exceeds the {MaxIdentityLength} character cap.");
        }

        return trimmed;
    }
}
