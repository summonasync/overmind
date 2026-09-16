namespace Overmind.Core;

public sealed class SkillKitEntry
{
    private SkillKitEntry()
    {
    }

    public Guid KitId { get; private set; }
    public Guid EntryId { get; private set; }
    public string? RelativePath { get; private set; }
    public string? ImportedContentHash { get; private set; }

    public SkillKit Kit { get; private set; } = null!;
    public ContextEntry Entry { get; private set; } = null!;

    public void UpdateImportedContentHash(string importedContentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(importedContentHash);

        ImportedContentHash = importedContentHash;
    }

    internal static SkillKitEntry Create(
        Guid kitId, Guid entryId, string? importedContentHash = null, string? relativePath = null)
    {
        return new SkillKitEntry
        {
            KitId = kitId,
            EntryId = entryId,
            ImportedContentHash = importedContentHash,
            RelativePath = relativePath
        };
    }
}
