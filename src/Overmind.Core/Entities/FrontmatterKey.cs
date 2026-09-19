namespace Overmind.Core;

public sealed class FrontmatterKey
{
    private FrontmatterKey()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    public static FrontmatterKey Create(string name, string color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(color);

        return new FrontmatterKey
        {
            Id = Guid.NewGuid(),
            Name = Normalize(name),
            Color = color.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public static FrontmatterKey Restore(Guid id, string name, string color, DateTime createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(color);

        return new FrontmatterKey
        {
            Id = id,
            Name = Normalize(name),
            Color = color.Trim(),
            CreatedAt = createdAt
        };
    }

    private static string Normalize(string name) => name.Trim().ToLowerInvariant();
}
