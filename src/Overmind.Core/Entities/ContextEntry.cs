namespace Overmind.Core;

public sealed class ContextEntry
{
    private ContextEntry()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public EntryType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public int CurrentVersion { get; private set; }

    public List<SkillFile> SkillFiles { get; private set; } = [];
    public List<EntryTag> EntryTags { get; private set; } = [];
    public List<EntryVersion> Versions { get; private set; } = [];

    public static ContextEntry Create(Guid projectId, EntryType type, string title, string content)
    {
        if (type == EntryType.Skill)
        {
            throw new InvalidOperationException("Skill entries require at least one skill file; use CreateSkill.");
        }

        return CreateCore(projectId, type, title, content);
    }

    public static ContextEntry CreateSkill(
        Guid projectId, string title, string content, IEnumerable<SkillFile> files)
    {
        var materialized = files.ToList();
        if (materialized.Count == 0)
        {
            throw new ArgumentException("Skill entries require at least one skill file.", nameof(files));
        }

        var entry = CreateCore(projectId, EntryType.Skill, title, content);
        foreach (var file in materialized)
        {
            file.AttachTo(entry.Id);
            entry.SkillFiles.Add(file);
        }

        return entry;
    }

    public EntryVersion UpdateContent(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var nextVersion = CurrentVersion + 1;
        var snapshot = EntryVersion.Create(Id, nextVersion, Content);
        Versions.Add(snapshot);

        Content = content;
        UpdatedAt = DateTime.UtcNow;
        CurrentVersion = nextVersion;
        return snapshot;
    }

    public void AddTag(Tag tag)
    {
        if (EntryTags.Any(t => t.TagId == tag.Id))
        {
            return;
        }

        EntryTags.Add(EntryTag.Create(Id, tag.Id));
    }

    private static ContextEntry CreateCore(Guid projectId, EntryType type, string title, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var now = DateTime.UtcNow;
        return new ContextEntry
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = type,
            Title = title.Trim(),
            Content = content,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
