namespace Overmind.Core;

public sealed class EntryTag
{
    private EntryTag()
    {
    }

    public Guid EntryId { get; private set; }
    public Guid TagId { get; private set; }

    public ContextEntry Entry { get; private set; } = null!;
    public Tag Tag { get; private set; } = null!;

    internal static EntryTag Create(Guid entryId, Guid tagId)
    {
        return new EntryTag
        {
            EntryId = entryId,
            TagId = tagId
        };
    }
}
