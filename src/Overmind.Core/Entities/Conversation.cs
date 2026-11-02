namespace Overmind.Core;

public sealed class Conversation
{
    public const int MaxTitleLength = 200;

    private Conversation()
    {
    }

    public Guid Id { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string? Title { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Conversation Create(Guid? projectId, string? title)
    {
        return new Conversation
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = NormalizeTitle(title),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Rename(string? title)
    {
        Title = NormalizeTitle(title);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Touch(DateTime? at = null)
    {
        UpdatedAt = at ?? DateTime.UtcNow;
    }

    private static string? NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var trimmed = title.Trim();
        if (trimmed.Length > MaxTitleLength)
        {
            throw new ArgumentException(
                $"Conversation title exceeds the {MaxTitleLength} character cap.", nameof(title));
        }

        return trimmed;
    }
}
