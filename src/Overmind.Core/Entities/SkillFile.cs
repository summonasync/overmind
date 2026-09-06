namespace Overmind.Core;

public sealed class SkillFile
{
    private SkillFile()
    {
    }

    public Guid Id { get; private set; }
    public Guid EntryId { get; private set; }
    public AgentPlatform Platform { get; private set; }
    public string RelativePath { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;

    public static SkillFile Create(AgentPlatform platform, string relativePath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return new SkillFile
        {
            Id = Guid.NewGuid(),
            Platform = platform,
            RelativePath = relativePath.Trim(),
            Content = content
        };
    }

    internal void AttachTo(Guid entryId)
    {
        EntryId = entryId;
    }
}
