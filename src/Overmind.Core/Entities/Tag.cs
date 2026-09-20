namespace Overmind.Core;

public sealed class Tag
{
    private Tag()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Color { get; private set; }

    public List<EntryTag> EntryTags { get; private set; } = [];

    public static Tag Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Tag
        {
            Id = Guid.NewGuid(),
            Name = name.Trim()
        };
    }

    public void SetColor(string color)
    {
        Color = color.Length == 0 ? null : color;
    }
}
