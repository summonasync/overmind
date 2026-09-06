namespace Overmind.Core;

public sealed class SkillKitEntry
{
    private SkillKitEntry()
    {
    }

    public Guid KitId { get; private set; }
    public Guid EntryId { get; private set; }

    public SkillKit Kit { get; private set; } = null!;
    public ContextEntry Entry { get; private set; } = null!;

    internal static SkillKitEntry Create(Guid kitId, Guid entryId)
    {
        return new SkillKitEntry
        {
            KitId = kitId,
            EntryId = entryId
        };
    }
}
