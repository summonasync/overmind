namespace Overmind.Core;

public sealed class EntryVersion
{
    private EntryVersion()
    {
    }

    public Guid Id { get; private set; }
    public Guid EntryId { get; private set; }
    public int Version { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    internal static EntryVersion Create(Guid entryId, int version, string content)
    {
        return new EntryVersion
        {
            Id = Guid.NewGuid(),
            EntryId = entryId,
            Version = version,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
    }
}
