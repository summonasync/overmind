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
    public FeaturePriority Priority { get; private set; } = FeaturePriority.P2;
    public bool IsCurrent { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public List<PlanningArtifact> Artifacts { get; private set; } = [];
    public List<PlanningFeatureDependency> BlockedBy { get; private set; } = [];

    public static PlanningFeature Create(
        Guid projectId, string slug, string title, string body,
        FeaturePriority priority = FeaturePriority.P2)
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
            Priority = priority,
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
        DateTime updatedAt,
        bool isCurrent,
        FeaturePriority priority = FeaturePriority.P2)
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
            IsCurrent = isCurrent,
            Priority = priority,
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

    public void SetStatus(PlanningFeatureStatus status, string logNote, PlanningLogSource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logNote);

        Body = PlanningLog.Append(Body, logNote, source: source);
        SetStatus(status);
    }

    public void SetCurrent(bool isCurrent)
    {
        IsCurrent = isCurrent;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPriority(FeaturePriority priority)
    {
        Priority = priority;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddBlockedBy(PlanningFeature blocker)
    {
        ArgumentNullException.ThrowIfNull(blocker);

        if (blocker.Id == Id)
        {
            throw new ArgumentException("A feature cannot be blocked by itself.");
        }

        if (blocker.ProjectId != ProjectId)
        {
            throw new ArgumentException(
                $"Feature '{blocker.Slug}' belongs to another project; dependencies stay within one project.");
        }

        if (BlockedBy.Any(d => d.BlockedByFeatureId == blocker.Id))
        {
            return;
        }

        BlockedBy.Add(PlanningFeatureDependency.Create(Id, blocker.Id));
    }

    public void RemoveBlockedBy(PlanningFeature blocker)
    {
        ArgumentNullException.ThrowIfNull(blocker);

        var link = BlockedBy.FirstOrDefault(d => d.BlockedByFeatureId == blocker.Id);
        if (link is not null)
        {
            BlockedBy.Remove(link);
        }
    }

    public IReadOnlyList<PlanningFeature> GetBlocks(IEnumerable<PlanningFeature> projectFeatures)
    {
        ArgumentNullException.ThrowIfNull(projectFeatures);

        return projectFeatures
            .Where(f => f.BlockedBy.Any(d => d.BlockedByFeatureId == Id))
            .ToList();
    }
}
