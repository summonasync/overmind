namespace Overmind.Core;

public sealed class PlanningFeature
{
    private PlanningFeature()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public PlanningFeatureStatus Status { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public List<PlanningArtifact> Artifacts { get; private set; } = [];

    public static PlanningFeature Create(Guid projectId, string slug, string title, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var now = DateTime.UtcNow;
        return new PlanningFeature
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Slug = slug.Trim(),
            Title = title.Trim(),
            Body = body ?? string.Empty,
            Status = PlanningFeatureStatus.Draft,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static PlanningFeature Restore(
        Guid projectId,
        string slug,
        string title,
        string body,
        PlanningFeatureStatus status,
        int version,
        DateTime createdAt,
        DateTime updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new PlanningFeature
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Slug = slug.Trim(),
            Title = title.Trim(),
            Body = body,
            Status = status,
            Version = version,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void Update(string? title, string? body)
    {
        if (title is not null)
        {
            Title = title.Trim();
        }

        if (body is not null)
        {
            Body = body;
        }

        UpdatedAt = DateTime.UtcNow;

        if (Status != PlanningFeatureStatus.Draft)
        {
            Version++;
        }
    }

    public void SetStatus(PlanningFeatureStatus status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }
}
