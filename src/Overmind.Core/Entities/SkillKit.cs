namespace Overmind.Core;

public sealed class SkillKit
{
    private SkillKit()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public KitSourceKind SourceKind { get; private set; }
    public string? SourcePath { get; private set; }
    public DateTime? ImportedAtUtc { get; private set; }

    public List<SkillKitEntry> SkillKitEntries { get; private set; } = [];

    public static SkillKit Create(string name, int version, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);

        return new SkillKit
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Version = version,
            Description = description
        };
    }

    public void MarkImported(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        SourceKind = KitSourceKind.Imported;
        SourcePath = sourcePath.Trim();
        ImportedAtUtc = DateTime.UtcNow;
    }

    public void ClearSource()
    {
        SourceKind = KitSourceKind.Manual;
        SourcePath = null;
        ImportedAtUtc = null;
    }

    public void AddEntry(Guid entryId, string? importedContentHash = null, string? relativePath = null)
    {
        if (SkillKitEntries.Any(e => e.EntryId == entryId))
        {
            throw new InvalidOperationException("Entry is already part of this kit.");
        }

        SkillKitEntries.Add(SkillKitEntry.Create(Id, entryId, importedContentHash, relativePath));
    }
}
