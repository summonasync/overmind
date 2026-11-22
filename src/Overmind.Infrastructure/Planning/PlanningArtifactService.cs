using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningArtifactService
{
    private readonly OvermindDbContext _db;
    private readonly PlanningMaterializationDispatcher _materialization;
    private readonly TaskDependencyService _taskDependencies;
    private readonly PlanningBatchScope? _batchScope;

    public PlanningArtifactService(
        OvermindDbContext db,
        PlanningMaterializationDispatcher materialization,
        TaskDependencyService taskDependencies,
        PlanningBatchScope? batchScope = null)
    {
        _db = db;
        _materialization = materialization;
        _taskDependencies = taskDependencies;
        _batchScope = batchScope;
    }

    public async Task<PlanningArtifact> UpsertAsync(
        Guid featureId, PlanningArtifactKind kind, string body, int? order,
        PlanningReviewLevel? review, string? logNote, CancellationToken ct = default,
        string? brief = null, PlanningLogSource? source = null,
        IReadOnlyList<int>? blockedBy = null, bool dryRun = false, bool? independent = null)
    {
        body = PlanningSectionCanonicalizer.Canonicalize(body);
        PointerMarkerGrammar.EnsureValid(body);
        if (kind == PlanningArtifactKind.Task)
        {
            body = PlanningTaskSections.WithSkeleton(body);
        }

        if (brief is not null && kind != PlanningArtifactKind.Task)
        {
            throw PlanningException.BriefNotAllowedForKind(kind);
        }

        if (blockedBy is not null && kind != PlanningArtifactKind.Task)
        {
            throw PlanningException.BlockedByNotAllowedForKind(kind);
        }

        if (independent is not null && kind != PlanningArtifactKind.Task)
        {
            throw PlanningException.IndependentNotAllowedForKind(kind);
        }

        var feature = await _db.PlanningFeatures.FirstOrDefaultAsync(f => f.Id == featureId, ct);
        if (feature is null)
        {
            throw PlanningException.MissingFeature(featureId.ToString());
        }

        var resolvedOrder = await ResolveOrderAsync(kind, order, featureId, ct);

        var existing = await _db.PlanningArtifacts
            .FirstOrDefaultAsync(a => a.FeatureId == featureId && a.Kind == kind && a.Order == resolvedOrder, ct);

        PlanningArtifact? created = null;
        var mutated = false;

        if (existing is not null)
        {
            if (existing.Body != body || !string.IsNullOrWhiteSpace(logNote))
            {
                var preMutationContentUpdatedAt = existing.ContentUpdatedAt;
                var wasValidated = existing.Status == PlanningArtifactStatus.Validated;
                var wasNeverEdited = existing.ContentUpdatedAt == existing.CreatedAt;
                bool bodyMutated;
                try
                {
                    bodyMutated = existing.UpdateBody(body, logNote, source);
                }
                catch (InvalidOperationException)
                {
                    throw PlanningException.ChangeNoteRequired();
                }

                mutated = true;

                if (bodyMutated && existing.Kind != PlanningArtifactKind.Task
                    && existing.ContentUpdatedAt != preMutationContentUpdatedAt && wasValidated)
                {
                    var voidedProof = await FindLiveApprovalProofAsync(
                        feature.ProjectId, feature.Slug, preMutationContentUpdatedAt, wasNeverEdited, ct);
                    existing.RecordValidationProofVoided(voidedProof?.RepliedAt, "gated body changed via upsert");
                }

                if (existing.StatusChanged is not null)
                {
                    DemoteReviewFeatureOnDocumentInvalidation(existing, feature);
                }
            }

            if (review.HasValue)
            {
                existing.SetReview(review.Value);
                mutated = true;
            }

            if (brief is not null)
            {
                existing.SetBrief(brief);
                mutated = true;
            }

            if (independent.HasValue)
            {
                existing.SetIndependent(independent.Value);
                mutated = true;
            }
        }
        else
        {
            created = PlanningArtifact.Create(
                featureId, kind, body, resolvedOrder, review ?? PlanningReviewLevel.Light, brief);
            if (independent == true)
            {
                created.SetIndependent(true);
            }

            if (!dryRun)
            {
                _db.PlanningArtifacts.Add(created);
                TransitionOnArtifactCreate(feature, kind);
            }

            mutated = true;
        }

        if (blockedBy is not null)
        {
            mutated |= await _taskDependencies.StageBlockedByAsync(existing ?? created!, blockedBy, ct);
        }

        if (dryRun)
        {
            return existing ?? created!;
        }

        if (mutated)
        {
            feature.Touch();
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (PlanningServiceGuard.IsUniqueViolation(ex))
        {
            if (created is null || order.HasValue || kind != PlanningArtifactKind.Task)
            {
                throw PlanningException.DuplicateArtifact(kind, resolvedOrder);
            }

            _db.PlanningTaskDependencies.RemoveRange(created.BlockedBy);
            _db.PlanningArtifacts.Remove(created);
            resolvedOrder = await ResolveOrderAsync(kind, null, featureId, ct);
            created = PlanningArtifact.Create(
                featureId, kind, body, resolvedOrder, review ?? PlanningReviewLevel.Light, brief);
            if (independent == true)
            {
                created.SetIndependent(true);
            }

            _db.PlanningArtifacts.Add(created);

            if (blockedBy is not null)
            {
                await _taskDependencies.StageBlockedByAsync(created, blockedBy, ct);
            }

            TransitionOnArtifactCreate(feature, kind);
            feature.Touch();

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException retryEx) when (PlanningServiceGuard.IsUniqueViolation(retryEx))
            {
                throw PlanningException.DuplicateArtifact(kind, resolvedOrder);
            }
        }

        var artifact = existing ?? created
            ?? await _db.PlanningArtifacts
                .FirstAsync(a => a.FeatureId == featureId && a.Kind == kind && a.Order == resolvedOrder, ct);

        await _materialization.DispatchAsync(feature.ProjectId, ct);

        return artifact;
    }

    public async Task<PlanningTaskBatchResult> UpsertTasksAsync(
        Guid featureId, IReadOnlyList<PlanningTaskUpsert> tasks, CancellationToken ct = default,
        PlanningLogSource? source = null, bool dryRun = false)
    {
        var feature = await _db.PlanningFeatures.FirstOrDefaultAsync(f => f.Id == featureId, ct);
        if (feature is null)
        {
            throw PlanningException.MissingFeature(featureId.ToString());
        }

        var siblings = await _db.PlanningArtifacts
            .Include(a => a.BlockedBy)
            .Where(a => a.FeatureId == featureId && a.Kind == PlanningArtifactKind.Task)
            .ToListAsync(ct);
        StagePendingArtifacts(featureId, PlanningArtifactKind.Task, siblings);

        var staged = new List<PlanningArtifact>(tasks.Count);
        var created = 0;
        var mutated = false;

        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i] with
            {
                Body = PlanningTaskSections.WithSkeleton(
                    PlanningSectionCanonicalizer.Canonicalize(tasks[i].Body))
            };
            try
            {
                PointerMarkerGrammar.EnsureValid(task.Body);

                var (artifact, isNew, changed) = StageTask(featureId, task, siblings, source, dryRun);
                staged.Add(artifact);
                mutated |= changed;
                if (isNew)
                {
                    created++;
                }
            }
            catch (PlanningException ex)
            {
                throw PlanningException.TaskBatchItem(i, ex);
            }
            catch (InvalidOperationException)
            {
                throw PlanningException.TaskBatchItem(i, PlanningException.ChangeNoteRequired());
            }
        }

        var siblingsById = siblings.ToDictionary(a => a.Id);
        for (var i = 0; i < tasks.Count; i++)
        {
            if (tasks[i].BlockedBy is not { } blockedBy)
            {
                continue;
            }

            try
            {
                mutated |= _taskDependencies.StageBlockedBy(staged[i], blockedBy, siblingsById);
            }
            catch (PlanningException ex)
            {
                throw PlanningException.TaskBatchItem(i, ex);
            }
        }

        var blockedOrders = staged
            .Select(s => TaskDependencyService.BlockedOrdersOf(s, siblingsById))
            .ToArray();

        if (dryRun)
        {
            return new PlanningTaskBatchResult(staged, created, staged.Count - created, blockedOrders);
        }

        if (created > 0)
        {
            TransitionOnArtifactCreate(feature, PlanningArtifactKind.Task);
        }

        if (mutated)
        {
            feature.Touch();
        }

        try
        {
            await SaveAndDispatchAsync(feature.ProjectId, ct);
        }
        catch (DbUpdateException ex) when (PlanningServiceGuard.IsUniqueViolation(ex))
        {
            throw PlanningException.TaskBatchConcurrentWrite();
        }

        return new PlanningTaskBatchResult(staged, created, staged.Count - created, blockedOrders);
    }

    private (PlanningArtifact Artifact, bool IsNew, bool Mutated) StageTask(
        Guid featureId, PlanningTaskUpsert task, List<PlanningArtifact> siblings,
        PlanningLogSource? source, bool dryRun)
    {
        var resolvedOrder = task.Order ?? (siblings.Count == 0 ? 0 : siblings.Max(a => a.Order) + 1);
        var existing = siblings.FirstOrDefault(a => a.Order == resolvedOrder);

        if (existing is not null)
        {
            var changed = false;

            if (existing.Body != task.Body || !string.IsNullOrWhiteSpace(task.LogNote))
            {
                existing.UpdateBody(task.Body, task.LogNote, source);
                changed = true;
            }

            if (task.Review.HasValue)
            {
                existing.SetReview(task.Review.Value);
                changed = true;
            }

            if (task.Brief is not null)
            {
                existing.SetBrief(task.Brief);
                changed = true;
            }

            if (task.Independent.HasValue)
            {
                existing.SetIndependent(task.Independent.Value);
                changed = true;
            }

            return (existing, false, changed);
        }

        var artifact = PlanningArtifact.Create(
            featureId, PlanningArtifactKind.Task, task.Body, resolvedOrder,
            task.Review ?? PlanningReviewLevel.Light, task.Brief);
        if (task.Independent == true)
        {
            artifact.SetIndependent(true);
        }

        if (!dryRun)
        {
            _db.PlanningArtifacts.Add(artifact);
        }

        siblings.Add(artifact);
        return (artifact, true, true);
    }

    public async Task<PlanningArtifactReadRow?> ReadAsync(
        Guid artifactId, CancellationToken ct = default, Guid? callerProjectId = null)
    {
        var row = await _db.PlanningArtifacts
            .Where(a => a.Id == artifactId)
            .Select(a => new PlanningArtifactReadRow(
                a.Id, a.Kind, a.Status, a.Version, a.Order, a.UpdatedAt, a.Brief, a.Independent,
                a.Body, a.FeatureId,
                _db.PlanningFeatures.Where(f => f.Id == a.FeatureId).Select(f => f.ProjectId).First()))
            .FirstOrDefaultAsync(ct);

        if (row is not null)
        {
            GuardCallerScope(artifactId, row.OwningProjectId, callerProjectId);
        }

        return row;
    }

    public async Task<PlanningArtifactStatusResult> SetStatusAsync(
        Guid artifactId, PlanningArtifactStatus status, PlanningLogSource source, string logNote,
        CancellationToken ct = default, bool requireHumanProof = false, Guid? callerProjectId = null)
    {
        if (string.IsNullOrWhiteSpace(logNote))
        {
            throw PlanningException.ChangeNoteRequired();
        }

        var artifact = await LoadArtifactOrThrow(artifactId, ct);
        var feature = await _db.PlanningFeatures.FirstAsync(f => f.Id == artifact.FeatureId, ct);
        GuardCallerScope(artifact.Id, feature.ProjectId, callerProjectId);

        if (requireHumanProof && IsDocumentValidation(artifact.Kind, status))
        {
            await RequireLiveApprovalProofAsync(feature.ProjectId, feature.Slug, artifact, ct);
        }

        var wasValidated = artifact.IsFrozen;

        try
        {
            artifact.SetStatus(status, logNote, source);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw PlanningException.InvalidStatusForKind(artifact.Kind, status);
        }
        catch (InvalidOperationException)
        {
            throw PlanningException.ChangeNoteRequired();
        }

        if (wasValidated && status != PlanningArtifactStatus.Validated)
        {
            DemoteReviewFeatureOnDocumentInvalidation(artifact, feature);
        }

        if (artifact.Kind == PlanningArtifactKind.Task && artifact.Status == PlanningArtifactStatus.Done)
        {
            await TryPromoteToReviewAsync(feature, ct);
        }

        if (IsDocumentValidation(artifact.Kind, artifact.Status))
        {
            await TryPromoteToReviewAsync(feature, ct);
        }

        if (artifact.Kind == PlanningArtifactKind.Task &&
            artifact.Status is PlanningArtifactStatus.InProgress or PlanningArtifactStatus.Review or PlanningArtifactStatus.Done)
        {
            PromoteFeatureOnTaskTake(feature, artifact);
        }

        feature.Touch();

        await SaveAndDispatchAsync(feature.ProjectId, ct);

        return new PlanningArtifactStatusResult(
            artifact.Id, artifact.Status, artifact.Version, artifact.LastLogLine,
            UntickedAcWarning(artifact, status), artifact.FeatureStatusChanged);
    }

    private static string[]? UntickedAcWarning(PlanningArtifact artifact, PlanningArtifactStatus status)
    {
        if (status != PlanningArtifactStatus.Done || artifact.Kind != PlanningArtifactKind.Task)
        {
            return null;
        }

        var items = PlanningSections.ParseAcceptanceCriteria(artifact.Body);
        var unticked = items.Count(item => item.Checked == false);

        return unticked > 0
            ? [$"{unticked} of {items.Count(item => item.Checked.HasValue)} acceptance items unticked"]
            : null;
    }

    public async Task<PlanningArtifactPatchResult> PatchAsync(
        Guid artifactId, PlanningArtifactPatch patch, CancellationToken ct = default,
        PlanningLogSource? source = null, Guid? callerProjectId = null)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var wantsAmend = patch.Amend;
        var wantsSection = patch.Section is not null;
        var wantsTick = patch.TickIndex.HasValue || patch.TickText is not null;
        var wantsBrief = patch.Brief is not null;
        var wantsRemove = patch.Remove;
        var actionCount = (wantsAmend ? 1 : 0) + (wantsSection ? 1 : 0) + (wantsTick ? 1 : 0) + (wantsBrief ? 1 : 0);

        if (actionCount != 1 ||
            (wantsBrief && string.IsNullOrWhiteSpace(patch.Brief)) ||
            (wantsAmend && string.IsNullOrWhiteSpace(patch.Content)) ||
            (wantsRemove && (!wantsSection || patch.Content is not null)))
        {
            throw PlanningException.InvalidPatchRequest();
        }

        var artifact = await LoadArtifactOrThrow(artifactId, ct);
        var feature = await _db.PlanningFeatures.FirstAsync(f => f.Id == artifact.FeatureId, ct);
        GuardCallerScope(artifact.Id, feature.ProjectId, callerProjectId);
        var preMutationContentUpdatedAt = artifact.ContentUpdatedAt;
        var wasValidated = artifact.Status == PlanningArtifactStatus.Validated;
        var wasNeverEdited = artifact.ContentUpdatedAt == artifact.CreatedAt;
        var noChange = false;
        int? tickedIndex = null;
        string? tickedText = null;

        try
        {
            if (wantsAmend)
            {
                if (string.IsNullOrWhiteSpace(patch.LogNote))
                {
                    throw PlanningException.AmendmentNoteRequired();
                }

                var amendmentText = PlanningSectionCanonicalizer.Canonicalize(patch.Content!);
                PointerMarkerGrammar.EnsureValid(amendmentText);
                artifact.AmendResult(amendmentText, patch.LogNote, source);
            }
            else if (wantsSection)
            {
                var content = patch.Content is null
                    ? null
                    : PlanningSectionCanonicalizer.Canonicalize(patch.Content);
                if (content is not null)
                {
                    PointerMarkerGrammar.EnsureValid(content);
                }

                if (PlanningSections.IsNameParseable(patch.Section!))
                {
                    if (wantsRemove)
                    {
                        throw PlanningException.PatchRemoveNotAllowedForXmlSection(patch.Section!);
                    }

                    artifact.PatchSection(patch.Section!, content, patch.LogNote, source);
                }
                else
                {
                    artifact.PatchHeadingSection(
                        patch.Section!, content, wantsRemove, patch.Reason, patch.LogNote, source);
                }
            }
            else if (wantsBrief)
            {
                if (artifact.Kind != PlanningArtifactKind.Task)
                {
                    throw PlanningException.BriefNotAllowedForKind(artifact.Kind);
                }

                artifact.SetBrief(patch.Brief);
            }
            else
            {
                var flip = patch.Untick
                    ? artifact.UntickAc(patch.TickIndex, patch.TickText, patch.LogNote, source)
                    : artifact.TickAc(patch.TickIndex, patch.TickText, patch.LogNote, source);
                noChange = !flip.Changed;
                tickedIndex = flip.Index;
                tickedText = flip.Text;
            }

            if (artifact.StatusChanged is not null)
            {
                DemoteReviewFeatureOnDocumentInvalidation(artifact, feature);
            }
        }
        catch (InvalidOperationException)
        {
            throw PlanningException.ChangeNoteRequired();
        }

        // Row 7: a task can never hold a validation proof (tasks are todo→review→done, the proof
        // gate belongs to spec/plan/result), so echoing one after a task write tells the caller a
        // document approval died when nothing of the sort existed. Suppressed for task mutations;
        // a proof can only die on an artifact that was validated.
        if (!noChange && !wantsBrief && artifact.Kind != PlanningArtifactKind.Task
            && artifact.ContentUpdatedAt != preMutationContentUpdatedAt && wasValidated)
        {
            var voidedProof = await FindLiveApprovalProofAsync(
                feature.ProjectId, feature.Slug, preMutationContentUpdatedAt, wasNeverEdited, ct);
            var voidCause = wantsAmend ? "amendment appended"
                : wantsRemove ? "heading tombstone"
                : patch.Section is not null ? $"section '{patch.Section}' changed"
                : "gated body changed";
            artifact.RecordValidationProofVoided(voidedProof?.RepliedAt, voidCause);
        }

        if (!noChange)
        {
            feature.Touch();
        }

        await SaveAndDispatchAsync(feature.ProjectId, ct);

        return new PlanningArtifactPatchResult(artifact, noChange, tickedIndex, tickedText);
    }

    public async Task<PlanningArtifact> ValidateAsync(
        Guid artifactId, string? logNote = null, PlanningLogSource? source = null,
        bool requireHumanProof = false, CancellationToken ct = default, Guid? callerProjectId = null)
    {
        var artifact = await LoadArtifactOrThrow(artifactId, ct);
        var feature = await _db.PlanningFeatures.FirstAsync(f => f.Id == artifact.FeatureId, ct);
        GuardCallerScope(artifact.Id, feature.ProjectId, callerProjectId);

        if (requireHumanProof && string.IsNullOrWhiteSpace(logNote))
        {
            logNote = await BuildHumanProofLineAsync(artifact, ct);
        }

        try
        {
            artifact.Validate(logNote, source);
        }
        catch (InvalidOperationException)
        {
            throw PlanningException.CannotValidateTask();
        }

        if (IsDocumentValidation(artifact.Kind, artifact.Status))
        {
            await TryPromoteToReviewAsync(feature, ct);
        }

        feature.Touch();

        await _db.SaveChangesAsync(ct);
        await _materialization.DispatchAsync(feature.ProjectId, ct);

        return artifact;
    }

    private static bool IsDocumentValidation(PlanningArtifactKind kind, PlanningArtifactStatus status) =>
        kind is PlanningArtifactKind.Spec or PlanningArtifactKind.Plan &&
        status == PlanningArtifactStatus.Validated;

    // A live proof: the newest replied approval for the feature still stands — it postdates the
    // artifact's last gated change, or the artifact has never been gated-edited since creation
    // and the tap covers first drafts (feature 259).
    private async Task<(Guid NotificationId, DateTime? RepliedAt)?> FindLiveApprovalProofAsync(
        Guid projectId, string featureSlug, DateTime contentUpdatedAt, bool neverEdited, CancellationToken ct)
    {
        var proof = await _db.AgentNotifications
            .AsNoTracking()
            .Where(n => n.Kind == NotificationKind.Approval
                && n.ProjectId == projectId
                && n.FeatureSlug == featureSlug
                && n.ReplyText != null && n.ReplyText != string.Empty)
            .OrderByDescending(n => n.RepliedAt)
            .Select(n => new { n.Id, n.RepliedAt })
            .FirstOrDefaultAsync(ct);

        if (proof is null)
        {
            return null;
        }

        var covers = proof.RepliedAt >= contentUpdatedAt || neverEdited;
        return covers ? (proof.Id, proof.RepliedAt) : null;
    }

    private async Task<(Guid NotificationId, DateTime? RepliedAt)> RequireLiveApprovalProofAsync(
        Guid projectId, string featureSlug, PlanningArtifact artifact, CancellationToken ct)
    {
        var proof = await FindLiveApprovalProofAsync(
            projectId, featureSlug, artifact.ContentUpdatedAt,
            artifact.ContentUpdatedAt == artifact.CreatedAt, ct);

        if (proof is null)
        {
            throw PlanningException.ValidationProofRequired(featureSlug);
        }

        return proof.Value;
    }

    private async Task<string> BuildHumanProofLineAsync(PlanningArtifact artifact, CancellationToken ct)
    {
        var feature = await _db.PlanningFeatures
            .AsNoTracking()
            .Where(f => f.Id == artifact.FeatureId)
            .Select(f => new { f.Slug, f.ProjectId })
            .FirstAsync(ct);

        var proof = await RequireLiveApprovalProofAsync(feature.ProjectId, feature.Slug, artifact, ct);

        return $"validated with human proof: notification {proof.NotificationId} replied {proof.RepliedAt:yyyy-MM-dd HH:mm:ss} UTC";
    }

    public async Task<PlanningArtifact> DeclineAsync(Guid artifactId, string? reason, CancellationToken ct = default)
    {
        var artifact = await LoadArtifactOrThrow(artifactId, ct);

        if (artifact.Kind == PlanningArtifactKind.Task)
        {
            throw PlanningException.CannotDeclineTask();
        }

        var feature = await _db.PlanningFeatures.FirstAsync(f => f.Id == artifact.FeatureId, ct);

        return await AppendLogAsync(artifact, feature, DeclineNote(reason), ct, PlanningLogSource.Ui);
    }

    private static string DeclineNote(string? reason)
    {
        var trimmed = reason?.Trim();

        return string.IsNullOrEmpty(trimmed)
            ? "declined by human"
            : $"declined by human: {trimmed}";
    }

    public async Task<PlanningArtifact> AppendLogAsync(
        Guid artifactId, string note, CancellationToken ct = default,
        PlanningLogSource? source = null, Guid? callerProjectId = null)
    {
        var artifact = await LoadArtifactOrThrow(artifactId, ct);
        var feature = await _db.PlanningFeatures.FirstAsync(f => f.Id == artifact.FeatureId, ct);
        GuardCallerScope(artifact.Id, feature.ProjectId, callerProjectId);

        return await AppendLogAsync(artifact, feature, note, ct, source);
    }

    private async Task<PlanningArtifact> AppendLogAsync(
        PlanningArtifact artifact, PlanningFeature feature, string note, CancellationToken ct,
        PlanningLogSource? source)
    {
        artifact.AppendLog(note, source);
        feature.Touch();

        await SaveAndDispatchAsync(feature.ProjectId, ct);

        return artifact;
    }

    public async Task<PlanningArtifact> EditNewestLogAsync(
        Guid artifactId, string text, PlanningLogSource actor, CancellationToken ct = default,
        Guid? callerProjectId = null)
    {
        var artifact = await LoadArtifactOrThrow(artifactId, ct);
        var feature = await _db.PlanningFeatures.FirstAsync(f => f.Id == artifact.FeatureId, ct);
        GuardCallerScope(artifact.Id, feature.ProjectId, callerProjectId);

        artifact.EditNewestLog(text, actor);
        feature.Touch();

        await _db.SaveChangesAsync(ct);
        await _materialization.DispatchAsync(feature.ProjectId, ct);

        return artifact;
    }

    public async Task<PlanningArtifact> DeleteAsync(
        Guid artifactId,
        bool byHuman,
        string? logNote = null,
        PlanningLogSource? source = null,
        CancellationToken ct = default,
        Guid? callerProjectId = null)
    {
        var artifact = await _db.PlanningArtifacts
            .Include(a => a.BlockedBy)
            .FirstOrDefaultAsync(a => a.Id == artifactId, ct);

        if (artifact is null)
        {
            throw PlanningException.MissingArtifact(artifactId);
        }

        var feature = await _db.PlanningFeatures.FirstAsync(f => f.Id == artifact.FeatureId, ct);
        GuardCallerScope(artifact.Id, feature.ProjectId, callerProjectId);

        var dependents = await DependentOrdersAsync(artifactId, ct);
        if (dependents.Count > 0)
        {
            throw PlanningException.DeletionBlockedByDependents(
                [.. dependents.Select(o => $"task {o:D2}")]);
        }

        artifact.Delete(byHuman);

        _db.PlanningTaskDependencies.RemoveRange(artifact.BlockedBy);

        if (feature.Status == PlanningFeatureStatus.Review)
        {
            ReopenFeatureAfterDeletion(feature, artifact);
        }

        feature.AppendLog(DeletionAuditLine(artifact, byHuman, logNote), source ?? DeletionSource(byHuman));

        await _db.SaveChangesAsync(ct);
        await _materialization.DispatchAsync(feature.ProjectId, ct);

        return artifact;
    }

    private async Task<List<int>> DependentOrdersAsync(Guid artifactId, CancellationToken ct)
    {
        var dependentIds = await _db.PlanningTaskDependencies
            .Where(d => d.BlockingId == artifactId)
            .Select(d => d.BlockedById)
            .ToListAsync(ct);

        if (dependentIds.Count == 0)
        {
            return [];
        }

        return await _db.PlanningArtifacts
            .Where(a => dependentIds.Contains(a.Id))
            .OrderBy(a => a.Order)
            .Select(a => a.Order)
            .ToListAsync(ct);
    }

    private static void ReopenFeatureAfterDeletion(PlanningFeature feature, PlanningArtifact artifact)
    {
        if (artifact.Kind == PlanningArtifactKind.Task)
        {
            feature.SetStatus(
                PlanningFeatureStatus.InProgress,
                "task deleted while in review — reopened",
                PlanningLogSource.Auto);
            return;
        }

        if (artifact.Kind is PlanningArtifactKind.Spec or PlanningArtifactKind.Plan)
        {
            feature.SetStatus(
                PlanningFeatureStatus.InProgress,
                $"feature demoted: review → inProgress ({artifact.Kind.ToString().ToLowerInvariant()} deleted)",
                PlanningLogSource.Auto);
        }
    }

    private static string DeletionAuditLine(PlanningArtifact artifact, bool byHuman, string? logNote)
    {
        var label = artifact.Kind == PlanningArtifactKind.Task
            ? $"task {artifact.Order:D2}"
            : artifact.Kind.ToString().ToLowerInvariant();
        var marker = byHuman ? PlanningArtifact.HumanDeletionMarker : PlanningArtifact.AgentDeletionMarker;

        return string.IsNullOrWhiteSpace(logNote)
            ? $"artifact {label} deleted by {marker}"
            : $"artifact {label} deleted by {marker}, {logNote.Trim()}";
    }

    private static PlanningLogSource DeletionSource(bool byHuman) =>
        byHuman ? PlanningLogSource.Ui : PlanningLogSource.Agent;

    private async Task<PlanningArtifact> LoadArtifactOrThrow(Guid artifactId, CancellationToken ct)
    {
        var artifact = await _db.PlanningArtifacts.FirstOrDefaultAsync(a => a.Id == artifactId, ct);
        if (artifact is null)
        {
            throw PlanningException.MissingArtifact(artifactId);
        }

        return artifact;
    }

    private static void GuardCallerScope(Guid artifactId, Guid owningProjectId, Guid? callerProjectId)
    {
        if (callerProjectId is { } caller && owningProjectId != caller)
        {
            throw PlanningException.ArtifactOutsideCallerProject(artifactId, owningProjectId, caller);
        }
    }

    private async Task SaveAndDispatchAsync(Guid projectId, CancellationToken ct)
    {
        if (_batchScope is { Active: true })
        {
            return;
        }

        await _db.SaveChangesAsync(ct);
        await _materialization.DispatchAsync(projectId, ct);
    }

    private void StagePendingArtifacts(Guid featureId, PlanningArtifactKind? kind, List<PlanningArtifact> siblings)
    {
        if (_batchScope is not { Active: true })
        {
            return;
        }

        siblings.AddRange(_db.PlanningArtifacts.Local.Where(a =>
            _db.Entry(a).State == EntityState.Added &&
            a.FeatureId == featureId &&
            (kind is null || a.Kind == kind) &&
            siblings.All(s => s.Id != a.Id)));
    }

    private static void TransitionOnArtifactCreate(PlanningFeature feature, PlanningArtifactKind kind)
    {
        if (feature.Status is PlanningFeatureStatus.Draft or PlanningFeatureStatus.Ready)
        {
            if (feature.Status == PlanningFeatureStatus.Draft)
            {
                feature.SetStatus(
                    PlanningFeatureStatus.Ready, "artifact created → ready", PlanningLogSource.Auto);
            }

            return;
        }

        if (feature.Status is PlanningFeatureStatus.Review && kind is PlanningArtifactKind.Task)
        {
            feature.SetStatus(
                PlanningFeatureStatus.InProgress,
                "task created while in review — reopened for the new work",
                PlanningLogSource.Auto);
        }
    }

    private static void DemoteReviewFeatureOnDocumentInvalidation(
        PlanningArtifact artifact, PlanningFeature feature)
    {
        if (artifact.Kind is not (PlanningArtifactKind.Spec or PlanningArtifactKind.Plan) ||
            feature.Status != PlanningFeatureStatus.Review)
        {
            return;
        }

        feature.SetStatus(
            PlanningFeatureStatus.InProgress,
            $"feature demoted: review → inProgress ({artifact.Kind.ToString().ToLowerInvariant()} no longer validated)",
            PlanningLogSource.Auto);
        artifact.RecordFeatureDemotion();
    }

    private static void PromoteFeatureOnTaskTake(PlanningFeature feature, PlanningArtifact artifact)
    {
        if (feature.Status is not (PlanningFeatureStatus.Draft or PlanningFeatureStatus.Ready))
        {
            return;
        }

        feature.SetStatus(
            PlanningFeatureStatus.InProgress,
            $"task {artifact.Order:D2} taken → inProgress",
            PlanningLogSource.Auto);
    }

    private async Task TryPromoteToReviewAsync(PlanningFeature feature, CancellationToken ct)
    {
        if (feature.Status is not (PlanningFeatureStatus.InProgress or PlanningFeatureStatus.Ready))
        {
            return;
        }

        var siblings = await _db.PlanningArtifacts
            .Where(a => a.FeatureId == feature.Id)
            .ToListAsync(ct);
        StagePendingArtifacts(feature.Id, null, siblings);

        var tasks = siblings.Where(a => a.Kind == PlanningArtifactKind.Task).ToList();
        var hasValidatedSpec = siblings.Any(a =>
            a.Kind == PlanningArtifactKind.Spec && a.Status == PlanningArtifactStatus.Validated);
        var hasValidatedPlan = siblings.Any(a =>
            a.Kind == PlanningArtifactKind.Plan && a.Status == PlanningArtifactStatus.Validated);

        if (tasks.Count == 0 ||
            !tasks.All(t => t.Status == PlanningArtifactStatus.Done) ||
            !hasValidatedSpec ||
            !hasValidatedPlan)
        {
            return;
        }

        feature.SetStatus(
            PlanningFeatureStatus.Review,
            "all tasks done + spec + plan validated → review",
            PlanningLogSource.Auto);
    }

    private async Task<int> ResolveOrderAsync(PlanningArtifactKind kind, int? order, Guid featureId, CancellationToken ct)
    {
        if (kind != PlanningArtifactKind.Task)
        {
            return 0;
        }

        if (order.HasValue)
        {
            return order.Value;
        }

        var maxOrder = await _db.PlanningArtifacts
            .Where(a => a.FeatureId == featureId && a.Kind == PlanningArtifactKind.Task)
            .MaxAsync(a => (int?)a.Order, ct);

        return (maxOrder ?? -1) + 1;
    }
}
