using System.Collections.Frozen;

namespace Overmind.Core;

public sealed class PlanningArtifact
{
    private static readonly FrozenSet<PlanningArtifactStatus> SpecPlanResultStatuses =
        new HashSet<PlanningArtifactStatus>
        {
            PlanningArtifactStatus.Draft,
            PlanningArtifactStatus.Validated
        }.ToFrozenSet();

    private static readonly FrozenSet<PlanningArtifactStatus> TaskStatuses =
        new HashSet<PlanningArtifactStatus>
        {
            PlanningArtifactStatus.Todo,
            PlanningArtifactStatus.InProgress,
            PlanningArtifactStatus.Review,
            PlanningArtifactStatus.Done,
            PlanningArtifactStatus.Blocked
        }.ToFrozenSet();

    private PlanningArtifact()
    {
    }

    public Guid Id { get; private set; }
    public Guid FeatureId { get; private set; }
    public PlanningArtifactKind Kind { get; private set; }
    public PlanningArtifactStatus Status { get; private set; }
    public PlanningReviewLevel Review { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public DateTime? FrozenAt { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public List<PlanningArtifactVersion> Versions { get; private set; } = [];

    public bool IsFrozen => Status == PlanningArtifactStatus.Validated;

    public static IReadOnlySet<PlanningArtifactStatus> AllowedStatuses(PlanningArtifactKind kind) => kind switch
    {
        PlanningArtifactKind.Task => TaskStatuses,
        _ => SpecPlanResultStatuses
    };

    public static PlanningArtifact Create(
        Guid featureId,
        PlanningArtifactKind kind,
        string body,
        int order = 0,
        PlanningReviewLevel review = PlanningReviewLevel.Light)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        var now = DateTime.UtcNow;
        return new PlanningArtifact
        {
            Id = Guid.NewGuid(),
            FeatureId = featureId,
            Kind = kind,
            Status = kind == PlanningArtifactKind.Task
                ? PlanningArtifactStatus.Todo
                : PlanningArtifactStatus.Draft,
            Review = review,
            Body = body,
            Order = order,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static PlanningArtifact Restore(
        Guid featureId,
        PlanningArtifactKind kind,
        PlanningArtifactStatus status,
        PlanningReviewLevel review,
        string body,
        int order,
        DateTime? frozenAt,
        int version,
        DateTime createdAt,
        DateTime updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (!AllowedStatuses(kind).Contains(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                $"Status '{status}' is not allowed for artifact kind '{kind}'.");
        }

        var mustBeFrozen = status == PlanningArtifactStatus.Validated;
        if (mustBeFrozen != (frozenAt is not null))
        {
            throw new ArgumentException(
                $"FrozenAt must be set exactly when status is Validated (status={status}, frozenAt={frozenAt}).",
                nameof(frozenAt));
        }

        return new PlanningArtifact
        {
            Id = Guid.NewGuid(),
            FeatureId = featureId,
            Kind = kind,
            Status = status,
            Review = review,
            Body = body,
            Order = order,
            FrozenAt = frozenAt,
            Version = version,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void UpdateBody(string body, string? logNote = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (IsFrozen && string.IsNullOrWhiteSpace(logNote))
        {
            throw new InvalidOperationException(
                "Cannot update a frozen artifact without a change note.");
        }

        var snapshot = PlanningArtifactVersion.Create(Id, Version, Body);
        Versions.Add(snapshot);

        Body = body;
        Version++;
        UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(logNote))
        {
            Body = PlanningLog.Append(Body, logNote);
        }

        if (IsFrozen)
        {
            Status = PlanningArtifactStatus.Draft;
            FrozenAt = null;
        }
    }

    public void SetStatus(PlanningArtifactStatus status, string? logNote = null)
    {
        if (!AllowedStatuses(Kind).Contains(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                $"Status '{status}' is not allowed for artifact kind '{Kind}'.");
        }

        if (IsFrozen && status != PlanningArtifactStatus.Validated && string.IsNullOrWhiteSpace(logNote))
        {
            throw new InvalidOperationException(
                "Cannot leave the frozen state without a change note.");
        }

        if (!string.IsNullOrWhiteSpace(logNote))
        {
            Body = PlanningLog.Append(Body, logNote);
        }

        if (Status == PlanningArtifactStatus.Validated && status != PlanningArtifactStatus.Validated)
        {
            FrozenAt = null;
        }

        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Validate()
    {
        if (Kind == PlanningArtifactKind.Task)
        {
            throw new InvalidOperationException("Task artifacts cannot be validated.");
        }

        var now = DateTime.UtcNow;
        Status = PlanningArtifactStatus.Validated;
        FrozenAt = now;
        UpdatedAt = now;
    }

    public void AppendLog(string note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        Body = PlanningLog.Append(Body, note);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetReview(PlanningReviewLevel review)
    {
        Review = review;
        UpdatedAt = DateTime.UtcNow;
    }
}
