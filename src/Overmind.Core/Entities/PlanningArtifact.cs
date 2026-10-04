using System.Collections.Frozen;
using System.ComponentModel.DataAnnotations.Schema;

namespace Overmind.Core;

public sealed class PlanningArtifact
{
    public const string DemotedStatusEcho = "validated → draft";

    public const string DemotionLogNote = "artifact demoted: validated → draft";

    public const string FeatureDemotedStatusEcho = "review → inProgress";

    public const string AgentDeletionMarker = "agent";

    public const string HumanDeletionMarker = "human";

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
    public string? Brief { get; private set; }
    public int Order { get; private set; }
    public DateTime? FrozenAt { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string? PreservedFrontmatter { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public List<PlanningArtifactVersion> Versions { get; private set; } = [];
    public List<PlanningTaskDependency> BlockedBy { get; private set; } = [];

    public bool IsFrozen => Status == PlanningArtifactStatus.Validated;

    public bool IsDeleted => DeletedAt is not null;

    [NotMapped]
    public string? StatusChanged { get; private set; }

    [NotMapped]
    public string? FeatureStatusChanged { get; private set; }

    [NotMapped]
    public string LastLogLine => PlanningLog.Parse(Body).LastOrDefault()?.Text ?? string.Empty;

    public void RecordFeatureDemotion() => FeatureStatusChanged = FeatureDemotedStatusEcho;

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
        PlanningReviewLevel review = PlanningReviewLevel.Light,
        string? brief = null)
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
            Brief = brief == string.Empty ? null : brief,
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
        DateTime updatedAt,
        string? preservedFrontmatter = null,
        string? brief = null)
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
            Brief = brief == string.Empty ? null : brief,
            Order = order,
            FrozenAt = frozenAt,
            Version = version,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            PreservedFrontmatter = preservedFrontmatter
        };
    }

    public void UpdateBody(string body, string? logNote = null, PlanningLogSource? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        RequireLogHistoryAppendable(body);
        RequireChangeNoteWhenFrozen(logNote);
        ApplyBodyMutation(body, logNote, source);
    }

    public void PatchSection(string section, string content, string? logNote = null, PlanningLogSource? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        if (string.Equals(section, "log", StringComparison.OrdinalIgnoreCase))
        {
            throw PlanningException.PatchLogSectionNotReplaceable();
        }

        if (!PlanningSections.TryFind(section, Body, out _))
        {
            throw PlanningException.PatchSectionNotFound(section, PlanningSections.List(Body));
        }

        RequireChangeNoteWhenFrozen(logNote);
        ApplyBodyMutation(PlanningSections.Replace(section, Body, content), logNote, source);
    }

    public bool TickAc(int? index, string? text, string? logNote = null, PlanningLogSource? source = null)
    {
        if (index is null && text is null)
        {
            throw new ArgumentException("Specify the acceptance-criteria item by index or by text.");
        }

        if (index is not null && text is not null)
        {
            throw new ArgumentException("Specify either index or text, not both.");
        }

        var items = PlanningSections.ParseAcceptanceCriteria(Body);
        PlanningAcItem target;

        if (index is { } i)
        {
            target = i >= 0 && i < items.Count
                ? items[i]
                : throw PlanningException.TickItemNotFoundByIndex(i, items);
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);

            var matches = new List<int>();
            for (var j = 0; j < items.Count; j++)
            {
                if (string.Equals(BareAcText(items[j]), text, StringComparison.Ordinal))
                {
                    matches.Add(j);
                }
            }

            target = matches.Count switch
            {
                0 => throw PlanningException.TickItemNotFoundByText(text!, items),
                1 => items[matches[0]],
                _ => throw PlanningException.TickTextAmbiguous(text!, matches)
            };
        }

        if (target.Checked is true)
        {
            return false;
        }

        if (target.Checked is null)
        {
            throw PlanningException.TickItemNotCheckable(target.Index, target.RawText);
        }

        RequireChangeNoteWhenFrozen(logNote);
        ApplyBodyMutation(FlipAcMarker(Body, target.Index), logNote, source);
        return true;
    }

    public void SetStatus(PlanningArtifactStatus status, string? logNote = null, PlanningLogSource? source = null)
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
                "Cannot move a validated artifact to another status without a change note.");
        }

        if (!string.IsNullOrWhiteSpace(logNote))
        {
            Body = PlanningLog.Append(Body, logNote, source: source);
        }

        if (Status == PlanningArtifactStatus.Validated && status != PlanningArtifactStatus.Validated)
        {
            FrozenAt = null;
        }

        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Validate(string? logNote = null, PlanningLogSource? source = null)
    {
        if (Kind == PlanningArtifactKind.Task)
        {
            throw new InvalidOperationException("Task artifacts cannot be validated.");
        }

        var now = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(logNote))
        {
            Body = PlanningLog.Append(Body, logNote, now, source);
        }

        Status = PlanningArtifactStatus.Validated;
        FrozenAt = now;
        UpdatedAt = now;
    }

    public void Delete(bool byHuman)
    {
        if (!byHuman && !IsAgentDeletable)
        {
            throw PlanningException.AgentDeletionRefused(Kind, Status);
        }

        DeletedAt = DateTime.UtcNow;
        DeletedBy = byHuman ? HumanDeletionMarker : AgentDeletionMarker;
    }

    public bool IsAgentDeletable =>
        Kind == PlanningArtifactKind.Task
            ? Status != PlanningArtifactStatus.Done
            : Status == PlanningArtifactStatus.Draft;

    public void AppendLog(string note, PlanningLogSource? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        Versions.Add(PlanningArtifactVersion.Create(Id, Version, Body));

        Body = PlanningLog.Append(Body, note, source: source);
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetReview(PlanningReviewLevel review)
    {
        Review = review;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPreservedFrontmatter(string? preservedFrontmatter)
    {
        PreservedFrontmatter = preservedFrontmatter;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetBrief(string? brief)
    {
        Brief = brief == string.Empty ? null : brief;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReplaceBlockedEdges(IReadOnlyCollection<Guid> blockingIds)
    {
        ArgumentNullException.ThrowIfNull(blockingIds);

        var incoming = new HashSet<Guid>(blockingIds);
        for (var i = BlockedBy.Count - 1; i >= 0; i--)
        {
            if (!incoming.Contains(BlockedBy[i].BlockingId))
            {
                BlockedBy.RemoveAt(i);
            }
        }

        var present = BlockedBy.Select(d => d.BlockingId).ToHashSet();
        foreach (var id in blockingIds)
        {
            if (present.Add(id))
            {
                BlockedBy.Add(PlanningTaskDependency.Create(Id, id));
            }
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

    private void RequireChangeNoteWhenFrozen(string? logNote)
    {
        if (IsFrozen && string.IsNullOrWhiteSpace(logNote))
        {
            throw new InvalidOperationException(
                "Cannot update a validated artifact without a change note.");
        }
    }

    private void ApplyBodyMutation(string newBody, string? logNote, PlanningLogSource? source)
    {
        var wasValidated = IsFrozen;
        StatusChanged = null;
        FeatureStatusChanged = null;

        Versions.Add(PlanningArtifactVersion.Create(Id, Version, Body));

        Body = newBody;
        Version++;
        UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(logNote))
        {
            Body = PlanningLog.Append(Body, logNote, source: source);
        }

        if (wasValidated)
        {
            Status = PlanningArtifactStatus.Draft;
            FrozenAt = null;
            StatusChanged = DemotedStatusEcho;
            Body = PlanningLog.Append(Body, DemotionLogNote, source: PlanningLogSource.Auto);
        }
    }

    private string FlipAcMarker(string body, int itemIndex)
    {
        var section = PlanningSections.Find("acceptance-criteria", body)
            ?? throw PlanningException.PatchSectionNotFound("acceptance-criteria", PlanningSections.List(body));

        var lines = section.Content.Split('\n');
        var seen = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Trim().StartsWith("- "))
            {
                continue;
            }

            if (seen == itemIndex)
            {
                var markerAt = lines[i].IndexOf("[ ]", StringComparison.Ordinal);
                lines[i] = string.Concat(lines[i].AsSpan(0, markerAt), "[x]", lines[i].AsSpan(markerAt + 3));
                break;
            }

            seen++;
        }

        return PlanningSections.Replace("acceptance-criteria", body, string.Join('\n', lines));
    }

    private static string BareAcText(PlanningAcItem item)
    {
        if (item.Checked is null)
        {
            return item.RawText;
        }

        return item.RawText.Length > 4 ? item.RawText[4..] : string.Empty;
    }
}
