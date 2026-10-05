using System.Globalization;
using System.Text;

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

    public static ContextEntry CreateTombstone(
        ContextEntry original,
        PlanningLogSource door,
        string? actor,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (original.Type == EntryType.Tombstone)
        {
            throw new InvalidOperationException(
                "A tombstone cannot bury a tombstone: deleting a tombstone is a plain delete.");
        }

        if (door is not (PlanningLogSource.Agent or PlanningLogSource.Ui))
        {
            throw new ArgumentException(
                "Tombstones record the agent or human door only.", nameof(door));
        }

        return CreateCore(
            original.ProjectId,
            EntryType.Tombstone,
            $"{original.Title} (deleted)",
            TombstoneBody(original, door, actor, reason));
    }

    private static string TombstoneBody(ContextEntry original, PlanningLogSource door, string? actor, string? reason)
    {
        var doorName = door == PlanningLogSource.Agent ? "agent" : "human";
        var by = string.IsNullOrWhiteSpace(actor) ? string.Empty : $" by {actor.Trim()}";

        var body = new StringBuilder();
        body.Append("Deleted ")
            .Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
            .Append(" UTC via ")
            .Append(doorName)
            .Append(" door")
            .Append(by)
            .AppendLine(".");

        if (!string.IsNullOrWhiteSpace(reason))
        {
            body.AppendLine($"Reason: {reason.Trim()}");
        }

        body.AppendLine($"Original type: {original.Type.ToString().ToLowerInvariant()}");
        body.AppendLine();
        body.AppendLine("Final content:");
        body.AppendLine();
        body.Append(Fenced(original.Content));
        return body.ToString();
    }

    private static string Fenced(string content)
    {
        var longestRun = 0;
        var run = 0;
        foreach (var character in content)
        {
            run = character == '`' ? run + 1 : 0;
            longestRun = Math.Max(longestRun, run);
        }

        var fence = new string('`', Math.Max(3, longestRun + 1));
        return $"{fence}\n{content}\n{fence}";
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
