namespace Overmind.Core;

public sealed class EntryLink
{
    private EntryLink()
    {
    }

    public Guid Id { get; private set; }
    public Guid FromEntryId { get; private set; }
    public EntryLinkKind ToKind { get; private set; }
    public Guid ToId { get; private set; }
    public EntryLinkOrigin Origin { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static EntryLink CreateDerived(Guid fromEntryId, EntryLinkKind toKind, Guid toId)
    {
        if (toId == Guid.Empty)
        {
            throw new ArgumentException("A link target requires an id.", nameof(toId));
        }

        if (toId == fromEntryId)
        {
            throw new InvalidOperationException("An entry cannot link to itself.");
        }

        return new EntryLink
        {
            Id = Guid.NewGuid(),
            FromEntryId = fromEntryId,
            ToKind = toKind,
            ToId = toId,
            Origin = EntryLinkOrigin.Derived,
            CreatedAt = DateTime.UtcNow
        };
    }
}
