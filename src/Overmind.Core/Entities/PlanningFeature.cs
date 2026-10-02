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
    public int QueuePosition { get; private set; }
    public string? ClaimedByAlias { get; private set; }
    public string? ClaimedByVendor { get; private set; }
    public DateTime? ClaimedAt { get; private set; }
    public Guid? ClaimSessionId { get; private set; }
    public bool ClaimedByHuman { get; private set; }
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
            Priority = priority,
            Version = version,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void Update(string? title, string? body, string? logNote = null, PlanningLogSource? source = null)
    {
        RequireLogHistoryAppendable(body ?? Body);

        if (title is not null)
        {
            Title = title.Trim();
        }

        if (body is not null)
        {
            Body = body;
        }

        if (!string.IsNullOrWhiteSpace(logNote))
        {
            Body = PlanningLog.Append(Body, logNote, source: source);
        }

        UpdatedAt = DateTime.UtcNow;

        if (Status != PlanningFeatureStatus.Draft)
        {
            Version++;
        }
    }

    private void RequireLogHistoryAppendable(string incomingBody)
    {
        var stored = PlanningLog.Parse(Body);
        if (stored.Count == 0)
        {
            return;
        }

        var incoming = PlanningLog.Parse(incomingBody);
        var shared = Math.Min(stored.Count, incoming.Count);

        for (var i = 0; i < shared; i++)
        {
            if (stored[i] != incoming[i])
            {
                throw PlanningException.LogHistoryNotAppendable(i, stored.Count, incoming.Count);
            }
        }

        if (incoming.Count < stored.Count)
        {
            throw PlanningException.LogHistoryNotAppendable(incoming.Count, stored.Count, incoming.Count);
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

    public void Claim(string alias, string vendor, Guid sessionId, DateTime now, bool byHuman = false)
    {
        if (Status == PlanningFeatureStatus.Done)
        {
            throw PlanningException.FeatureClaimRefused(Slug);
        }

        ClaimedByAlias = NormalizeIdentity(alias);
        ClaimedByVendor = NormalizeIdentity(vendor);
        ClaimedAt = now;
        ClaimSessionId = sessionId;
        ClaimedByHuman = byHuman;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Release()
    {
        ClaimedByAlias = null;
        ClaimedByVendor = null;
        ClaimedAt = null;
        ClaimSessionId = null;
        ClaimedByHuman = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AppendLog(string note, PlanningLogSource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        Body = PlanningLog.Append(Body, note, source: source);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Touch() => UpdatedAt = DateTime.UtcNow;

    public string LastLogLine => PlanningLog.Parse(Body).LastOrDefault()?.Text ?? string.Empty;

    public void SetPriority(FeaturePriority priority)
    {
        Priority = priority;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetQueuePosition(int position)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);
        QueuePosition = position;
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

    private static string NormalizeIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Claim identity must not be empty.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > AgentSession.MaxIdentityLength)
        {
            throw new ArgumentException(
                $"Claim identity '{trimmed}' exceeds the {AgentSession.MaxIdentityLength} character cap.");
        }

        return trimmed;
    }
}
