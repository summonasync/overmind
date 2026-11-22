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

    public const int BriefMaxLength = 500;

    public const string AmendmentMarker = "<amendment>";

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
    public bool Independent { get; private set; }
    public int Order { get; private set; }
    public DateTime? FrozenAt { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime ContentUpdatedAt { get; private set; }
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
    public DateTime? ValidationProofVoided { get; private set; }

    [NotMapped]
    public string? ValidationProofVoidCause { get; private set; }

    [NotMapped]
    public string LastLogLine => PlanningLog.Parse(Body).LastOrDefault()?.Text ?? string.Empty;

    public void RecordFeatureDemotion() => FeatureStatusChanged = FeatureDemotedStatusEcho;

    public void RecordValidationProofVoided(DateTime? repliedAt, string? cause = null)
    {
        ValidationProofVoided = repliedAt;
        ValidationProofVoidCause = cause;
    }

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
        EnsureSingleLogBlock(body);

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
            Brief = NormalizeBrief(brief),
            Order = order,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
            ContentUpdatedAt = now
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
            Brief = NormalizeBrief(brief),
            Order = order,
            FrozenAt = frozenAt,
            Version = version,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ContentUpdatedAt = updatedAt,
            PreservedFrontmatter = preservedFrontmatter
        };
    }

    public bool UpdateBody(string body, string? logNote = null, PlanningLogSource? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        EnsureSingleLogBlock(body);

        var merged = SpliceLogHistory(body);
        if (merged == Body && string.IsNullOrWhiteSpace(logNote))
        {
            return false;
        }

        RequireChangeNoteWhenFrozen(logNote);
        ApplyBodyMutation(merged, logNote, source);
        return true;
    }

    public void PatchSection(string section, string? content, string? logNote = null, PlanningLogSource? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        if (string.Equals(section, "log", StringComparison.OrdinalIgnoreCase))
        {
            throw PlanningException.PatchLogSectionNotReplaceable();
        }

        if (PlanningSections.TryFind(section, Body, out _))
        {
            if (content is null)
            {
                throw PlanningException.SectionContentRequiredForReplace(section);
            }

            RequireChangeNoteWhenFrozen(logNote);
            ApplyBodyMutation(PlanningSections.Replace(section, Body, content), logNote, source);
            return;
        }

        if (!PlanningSections.IsNameParseable(section))
        {
            throw PlanningException.PatchSectionNameNotParseable(section);
        }

        if (PlanningSectionCanonicalizer.HasInlineSection(section, Body) ||
            (Kind == PlanningArtifactKind.Task && !PlanningTaskSections.IsDeclared(section)))
        {
            throw SectionNotFound(section);
        }

        RequireChangeNoteWhenFrozen(logNote);
        ApplyBodyMutation(PlanningSections.Insert(section, Body, content), logNote, source);
    }

    public void PatchHeadingSection(
        string section,
        string? content,
        bool remove,
        string? reason,
        string? logNote = null,
        PlanningLogSource? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        if (Kind == PlanningArtifactKind.Task)
        {
            throw PlanningException.PatchSectionNotFound(
                section,
                PlanningSections.List(Body),
                "Heading patches apply to the free-form markdown of spec, plan and result artifacts; tasks keep the XML section vocabulary.",
                string.Join(", ", PlanningTaskSections.Declared));
        }

        if (remove && string.IsNullOrWhiteSpace(reason))
        {
            throw PlanningException.PatchRemoveReasonRequired(section);
        }

        if (!PlanningHeadingSections.TryGetHeadingText(section, out var headingText))
        {
            throw HeadingNotFound(section);
        }

        var blocks = PlanningHeadingSections.Find(headingText, Body);
        if (blocks.Count > 1)
        {
            throw PlanningException.PatchHeadingAmbiguous(
                section, [.. blocks.Select(b => b.HeadingLine.Trim())]);
        }

        if (remove)
        {
            if (blocks.Count == 0)
            {
                throw HeadingNotFound(section);
            }

            EnsureLogNotInside(blocks[0]);
            RequireChangeNoteWhenFrozen(logNote);
            ApplyBodyMutation(
                PlanningHeadingSections.Remove(
                    blocks[0], Body, PlanningHeadingSections.TombstoneLine(reason!)),
                logNote,
                source);
            return;
        }

        if (blocks.Count == 0)
        {
            RequireChangeNoteWhenFrozen(logNote);
            ApplyBodyMutation(PlanningHeadingSections.Insert(headingText, Body, content), logNote, source);
            return;
        }

        if (content is null)
        {
            throw PlanningException.SectionContentRequiredForReplace(section);
        }

        EnsureLogNotInside(blocks[0]);
        RequireChangeNoteWhenFrozen(logNote);
        ApplyBodyMutation(PlanningHeadingSections.Replace(blocks[0], Body, content), logNote, source);
    }

    public PlanningAcTickResult TickAc(int? index, string? text, string? logNote = null, PlanningLogSource? source = null) =>
        FlipAc(index, text, checking: true, logNote, source);

    public PlanningAcTickResult UntickAc(int? index, string? text, string? logNote = null, PlanningLogSource? source = null) =>
        FlipAc(index, text, checking: false, logNote, source);

    public void AmendResult(string amendmentText, string logNote, PlanningLogSource? source = null)
    {
        if (Kind != PlanningArtifactKind.Result)
        {
            throw PlanningException.AmendmentNotAllowedForKind(Kind);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(amendmentText);
        ArgumentException.ThrowIfNullOrWhiteSpace(logNote);

        StatusChanged = null;
        FeatureStatusChanged = null;
        ValidationProofVoided = null;
        ValidationProofVoidCause = null;

        amendmentText = PlanningSectionCanonicalizer.Canonicalize(amendmentText);

        var withBlock = AppendAmendmentBlock(Body, amendmentText);
        EnsureSingleLogBlock(withBlock);
        EnsureNoSectionPairs(amendmentText);

        Versions.Add(PlanningArtifactVersion.Create(Id, Version, Body));

        Body = PlanningLog.Append(withBlock, logNote, source: source);
        Version++;
        UpdatedAt = DateTime.UtcNow;
        ContentUpdatedAt = UpdatedAt;
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

    public void EditNewestLog(string text, PlanningLogSource actor)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw PlanningException.LogEditBlankText();
        }

        if (text.Contains('\r') || text.Contains('\n'))
        {
            throw PlanningException.LogEditMultilineText();
        }

        var lines = PlanningLog.Parse(Body);
        if (lines.Count == 0)
        {
            throw PlanningException.LogEditEmptyLog();
        }

        var newest = lines[^1];
        var ownedNewest = -1;
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (lines[i].Source == actor)
            {
                ownedNewest = i;
                break;
            }
        }

        if (newest.Source != actor)
        {
            throw ownedNewest >= 0
                ? PlanningException.LogEditNotNewest(actor, newest.Source, ownedNewest + 1, lines.Count)
                : PlanningException.LogEditWrongActor(actor, newest.Source);
        }

        Versions.Add(PlanningArtifactVersion.Create(Id, Version, Body));

        Body = PlanningLog.ReplaceNewest(Body, text);
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
        var normalized = NormalizeBrief(brief);
        var now = DateTime.UtcNow;
        if (normalized != Brief)
        {
            ContentUpdatedAt = now;
        }

        Brief = normalized;
        UpdatedAt = now;
    }

    // Author opt-in for current_task issuance while a feature-level blocker holds. Not a gated
    // edit: flipping the flag is a scheduling decision, not spec/plan content.
    public void SetIndependent(bool independent)
    {
        if (Kind != PlanningArtifactKind.Task)
        {
            throw new InvalidOperationException(
                $"independent is a task-only flag; artifact kind is '{Kind}'.");
        }

        Independent = independent;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeBrief(string? brief)
    {
        if (string.IsNullOrWhiteSpace(brief))
        {
            return null;
        }

        if (brief.Contains('\r') || brief.Contains('\n') || brief.Length > BriefMaxLength)
        {
            throw PlanningException.InvalidBrief();
        }

        return brief;
    }

    // Amendments append after everything, log block included: one heading per amendment, dated,
    // and the caller's lines verbatim — a later upsert can never confuse the block with a section.
    private static string AppendAmendmentBlock(string body, string amendmentText)
    {
        var lineBreak = body.EndsWith('\n') ? string.Empty : "\n";
        return $"{body}{lineBreak}\n## {AmendmentMarker} {DateTime.UtcNow:yyyy-MM-dd}\n{amendmentText.TrimEnd()}\n";
    }

    // A quoted wrapper would become the section: reads and the mirror locate by the last anchored
    // pair, so the real section's wrapper and content zone get evicted on render. Fences are
    // invisible to that grammar, which is why even fenced quoting is refused here.
    private static void EnsureNoSectionPairs(string amendmentText)
    {
        foreach (var name in PlanningSections.List(amendmentText))
        {
            if (name != "log")
            {
                throw PlanningException.AmendmentSectionPairRefused(name);
            }
        }
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

    private PlanningException SectionNotFound(string section) =>
        PlanningException.PatchSectionNotFound(
            section,
            PlanningSections.List(Body),
            PlanningSectionCanonicalizer.HasInlineSection(section, Body)
                ? PlanningException.InlineSectionHint(section)
                : null,
            Kind == PlanningArtifactKind.Task ? string.Join(", ", PlanningTaskSections.Declared) : null);

    private PlanningException HeadingNotFound(string section) =>
        PlanningException.PatchHeadingNotFound(section, PlanningHeadingSections.List(Body));

    private void EnsureLogNotInside(PlanningHeadingBlock block)
    {
        if (PlanningSections.TryFind("log", Body[block.Start..block.End], out _))
        {
            throw PlanningException.PatchLogSectionNotReplaceable();
        }
    }

    // The log is append-only across resends: stored lines are the truth, and the incoming body's
    // log section carries new lines only. Line k of the incoming parse either continues the stored
    // sequence or opens the new tail — a rewrite, a drop or a reorder all collapse into "new tail",
    // so a resend never needs to quote history and never truncates it either.
    private string SpliceLogHistory(string incomingBody)
    {
        var stored = PlanningLog.Parse(Body);
        if (stored.Count == 0)
        {
            return incomingBody;
        }

        var incoming = PlanningLog.Parse(incomingBody);
        var shared = 0;
        while (shared < incoming.Count && shared < stored.Count && incoming[shared] == stored[shared])
        {
            shared++;
        }

        if (shared == incoming.Count && incoming.Count == stored.Count)
        {
            return incomingBody;
        }

        var lines = new List<string>(stored.Count + incoming.Count - shared);
        lines.AddRange(stored.Select(RenderLogLine));
        for (var i = shared; i < incoming.Count; i++)
        {
            lines.Add(RenderLogLine(incoming[i]));
        }

        var content = string.Join("\n", lines);
        return PlanningSections.TryFind("log", incomingBody, out _)
            ? PlanningSections.Replace("log", incomingBody, content)
            : PlanningSections.Insert("log", incomingBody, content);
    }

    private static string RenderLogLine(PlanningLogLine line) =>
        line.Date is null
            ? $"- {line.Text}"
            : line.Source is { } source
                ? $"- {line.Date:yyyy-MM-dd} [{source.ToString().ToLowerInvariant()}] — {line.Text}"
                : $"- {line.Date:yyyy-MM-dd} — {line.Text}";

    private static void EnsureSingleLogBlock(string body)
    {
        var blocks = PlanningSections.CountAnchoredBlocks("log", body);
        if (blocks > 1)
        {
            throw PlanningException.LogBlocksDuplicated(blocks);
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
        ValidationProofVoided = null;
        ValidationProofVoidCause = null;

        Versions.Add(PlanningArtifactVersion.Create(Id, Version, Body));

        var now = DateTime.UtcNow;
        if (PlanningSections.GatedContentOf(newBody) != PlanningSections.GatedContentOf(Body))
        {
            ContentUpdatedAt = now;
        }

        Body = newBody;
        Version++;
        UpdatedAt = now;

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

    private PlanningAcTickResult FlipAc(int? index, string? text, bool checking, string? logNote, PlanningLogSource? source)
    {
        var target = ResolveAcTarget(index, text);

        if (target.Checked == checking)
        {
            return new PlanningAcTickResult(target.Index, BareAcText(target), Changed: false);
        }

        if (target.Checked is null)
        {
            throw PlanningException.TickItemNotCheckable(target.Index, target.RawText);
        }

        RequireChangeNoteWhenFrozen(logNote);
        ApplyBodyMutation(FlipAcMarker(Body, target.Index, checking), logNote, source);
        return new PlanningAcTickResult(target.Index, BareAcText(target), Changed: true);
    }

    private PlanningAcItem ResolveAcTarget(int? index, string? text)
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

        if (items.Count == 0 && PlanningSectionCanonicalizer.HasInlineSection("acceptance-criteria", Body))
        {
            throw SectionNotFound("acceptance-criteria");
        }

        if (index is { } i)
        {
            return i >= 1 && i <= items.Count
                ? items[i - 1]
                : throw PlanningException.TickItemNotFoundByIndex(i, items);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var wanted = DecodeEntities(text);
        var matches = new List<int>();
        for (var j = 0; j < items.Count; j++)
        {
            if (string.Equals(DecodeEntities(BareAcText(items[j])), wanted, StringComparison.Ordinal))
            {
                matches.Add(items[j].Index);
            }
        }

        return matches.Count switch
        {
            0 => throw PlanningException.TickItemNotFoundByText(text!, items),
            1 => items[matches[0] - 1],
            _ => throw PlanningException.TickTextAmbiguous(text!, matches)
        };
    }

    private string FlipAcMarker(string body, int itemNumber, bool checking)
    {
        var section = PlanningSections.Find("acceptance-criteria", body)
            ?? throw SectionNotFound("acceptance-criteria");

        var lines = section.Content.Split('\n');
        var seen = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Trim().StartsWith("- "))
            {
                continue;
            }

            if (++seen == itemNumber)
            {
                lines[i] = SwapAcMarker(lines[i], checking);
                break;
            }
        }

        return PlanningSections.Replace("acceptance-criteria", body, string.Join('\n', lines));
    }

    private static string SwapAcMarker(string line, bool checking)
    {
        var marker = checking ? "[ ]" : UntickMarker(line);
        var markerAt = line.IndexOf(marker, StringComparison.Ordinal);

        return string.Concat(line.AsSpan(0, markerAt), checking ? "[x]" : "[ ]", line.AsSpan(markerAt + 3));
    }

    private static string UntickMarker(string line)
    {
        var text = line.TrimStart();
        if (text.StartsWith("- ", StringComparison.Ordinal))
        {
            text = text[2..].TrimStart();
        }

        return text.StartsWith("[X]", StringComparison.Ordinal) ? "[X]" : "[x]";
    }

    private static string BareAcText(PlanningAcItem item)
    {
        if (item.Checked is null)
        {
            return item.RawText;
        }

        return item.RawText.Length > 4 ? item.RawText[4..] : string.Empty;
    }

    // Single decode pass, so a stored "&amp;lt;" stays "&lt;" — decoding &amp; first would
    // collapse a double-escaped literal into the readable form and match an item it must not.
    private static string DecodeEntities(string text) =>
        text.Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&amp;", "&", StringComparison.Ordinal);
}
