namespace Overmind.Core;

public sealed class IssueAppend
{
    private IssueAppend()
    {
    }

    public Guid Id { get; private set; }
    public Guid IssueId { get; private set; }
    public Guid AuthorProjectId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    public static IssueAppend Create(Guid issueId, Guid authorProjectId, string text, DateTime? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new IssueAppend
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            AuthorProjectId = authorProjectId,
            Text = text.Trim(),
            CreatedAt = now ?? DateTime.UtcNow
        };
    }
}
