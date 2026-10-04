using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningArtifactService
{
    private readonly OvermindDbContext _db;
    private readonly IPlanningMaterializer _materializer;
    private readonly ILogger<PlanningArtifactService> _logger;
    private readonly TaskDependencyService _taskDependencies;

    public PlanningArtifactService(
        OvermindDbContext db,
        IPlanningMaterializer materializer,
        ILogger<PlanningArtifactService> logger,
        TaskDependencyService taskDependencies)
    {
        _db = db;
        _materializer = materializer;
        _logger = logger;
        _taskDependencies = taskDependencies;
    }

    public async Task<PlanningArtifact> UpsertAsync(
        Guid featureId, PlanningArtifactKind kind, string body, int? order,
        PlanningReviewLevel? review, string? logNote, CancellationToken ct = default,
        string? brief = null, PlanningLogSource? source = null,
        IReadOnlyList<int>? blockedBy = null)
    {
        if (brief is not null && kind != PlanningArtifactKind.Task)
        {
            throw PlanningException.BriefNotAllowedForKind(kind);
        }

        if (blockedBy is not null && kind != PlanningArtifactKind.Task)
        {
            throw PlanningException.BlockedByNotAllowedForKind(kind);
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
                try
                {
                    existing.UpdateBody(body, logNote, source);
                }
                catch (InvalidOperationException)
                {
                    throw PlanningException.ChangeNoteRequired();
                }

                mutated = true;

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
        }
        else
        {
            created = PlanningArtifact.Create(
                featureId, kind, body, resolvedOrder, review ?? PlanningReviewLevel.Light, brief);
            _db.PlanningArtifacts.Add(created);
            TransitionOnArtifactCreate(feature, kind);
            mutated = true;
        }

        if (blockedBy is not null)
        {
            mutated |= await _taskDependencies.StageBlockedByAsync(existing ?? created!, blockedBy, ct);
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

        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, feature.ProjectId, ct);

        return artifact;
    }

    public async Task<PlanningTaskBatchResult> UpsertTasksAsync(
        Guid featureId, IReadOnlyList<PlanningTaskUpsert> tasks, CancellationToken ct = default,
        PlanningLogSource? source = null)
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

        var staged = new List<PlanningArtifact>(tasks.Count);
        var created = 0;
        var mutated = false;

        for (var i = 0; i < tasks.Count; i++)
        {
            try
            {
                var (artifact, isNew, changed) = StageTask(featureId, tasks[i], siblings, source);
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
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (PlanningServiceGuard.IsUniqueViolation(ex))
        {
            throw PlanningException.TaskBatchConcurrentWrite();
        }

        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, feature.ProjectId, ct);

        return new PlanningTaskBatchResult(staged, created, staged.Count - created);
    }

    private (PlanningArtifact Artifact, bool IsNew, bool Mutated) StageTask(
        Guid featureId, PlanningTaskUpsert task, List<PlanningArtifact> siblings, PlanningLogSource? source)
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

            return (existing, false, changed);
        }

        var artifact = PlanningArtifact.Create(
            featureId, PlanningArtifactKind.Task, task.Body, resolvedOrder,
            task.Review ?? PlanningReviewLevel.Light, task.Brief);
        _db.PlanningArtifacts.Add(artifact);
        siblings.Add(artifact);
        return (artifact, true, true);
    }

    public async Task<PlanningArtifactStatusResult> SetStatusAsync(
        Guid artifactId, PlanningArtifactStatus status, PlanningLogSource source, string logNote,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(logNote))
        {
            throw PlanningException.ChangeNoteRequired();
        }

        var artifact = await LoadArtifactOrThrow(artifactId, ct);
        var feature = await _db.PlanningFeatures.FirstAsync(f => f.Id == artifact.FeatureId, ct);
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

        feature.Touch();

        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, feature.ProjectId, ct);

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

        return unticked > 0 ? [$"{unticked} of {items.Count} acceptance items unticked"] : null;
    }

    public async Task<PlanningArtifactPatchResult> PatchAsync(
        Guid artifactId, PlanningArtifactPatch patch, CancellationToken ct = default,
        PlanningLogSource? source = null)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var wantsSection = patch.Section is not null;
        var wantsTick = patch.TickIndex.HasValue || patch.TickText is not null;
        var wantsBrief = patch.Brief is not null;
        var actionCount = (wantsSection ? 1 : 0) + (wantsTick ? 1 : 0) + (wantsBrief ? 1 : 0);

        if (actionCount != 1 || (wantsSection && patch.Content is null) ||
            (wantsBrief && string.IsNullOrWhiteSpace(patch.Brief)))
        {
            throw PlanningException.InvalidPatchRequest();
        }

        var artifact = await LoadArtifactOrThrow(artifactId, ct);
        var feature = await _db.PlanningFeatures.FirstAsync(f => f.Id == artifact.FeatureId, ct);
        var alreadyTicked = false;

        try
        {
            if (wantsSection)
            {
                artifact.PatchSection(patch.Section!, patch.Content!, patch.LogNote, source);
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
                alreadyTicked = !artifact.TickAc(patch.TickIndex, patch.TickText, patch.LogNote, source);
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

        if (!alreadyTicked)
        {
            feature.Touch();
        }

        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, feature.ProjectId, ct);

        return new PlanningArtifactPatchResult(artifact, alreadyTicked);
    }

    public async Task<PlanningArtifact> ValidateAsync(
        Guid artifactId, string? logNote = null, PlanningLogSource? source = null,
        bool requireHumanProof = false, CancellationToken ct = default)
    {
        var artifact = await LoadArtifactOrThrow(artifactId, ct);
        var feature = await _db.PlanningFeatures.FirstAsync(f => f.Id == artifact.FeatureId, ct);

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

        feature.Touch();

        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, feature.ProjectId, ct);

        return artifact;
    }

    private async Task<string> BuildHumanProofLineAsync(PlanningArtifact artifact, CancellationToken ct)
    {
        var feature = await _db.PlanningFeatures
            .AsNoTracking()
            .Where(f => f.Id == artifact.FeatureId)
            .Select(f => new { f.Slug, f.ProjectId })
            .FirstAsync(ct);

        var proof = await _db.AgentNotifications
            .AsNoTracking()
            .Where(n => n.Kind == NotificationKind.Approval
                && n.ProjectId == feature.ProjectId
                && n.FeatureSlug == feature.Slug
                && n.ReplyText != null && n.ReplyText != string.Empty
                && n.RepliedAt >= artifact.UpdatedAt)
            .OrderByDescending(n => n.RepliedAt)
            .Select(n => new { n.Id, n.RepliedAt })
            .FirstOrDefaultAsync(ct);

        if (proof is null)
        {
            throw PlanningException.ValidationProofRequired(feature.Slug);
        }

        return $"validated with human proof: notification {proof.Id} replied {proof.RepliedAt:yyyy-MM-dd HH:mm:ss} UTC";
    }

    public async Task<PlanningArtifact> AppendLogAsync(Guid artifactId, string note, CancellationToken ct = default,
        PlanningLogSource? source = null)
    {
        var artifact = await LoadArtifactOrThrow(artifactId, ct);
        var feature = await _db.PlanningFeatures.FirstAsync(f => f.Id == artifact.FeatureId, ct);

        artifact.AppendLog(note, source);
        feature.Touch();

        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, feature.ProjectId, ct);

        return artifact;
    }

    public async Task<PlanningArtifact> DeleteAsync(
        Guid artifactId,
        bool byHuman,
        string? logNote = null,
        PlanningLogSource? source = null,
        CancellationToken ct = default)
    {
        var artifact = await _db.PlanningArtifacts
            .Include(a => a.BlockedBy)
            .FirstOrDefaultAsync(a => a.Id == artifactId, ct);

        if (artifact is null)
        {
            throw PlanningException.MissingArtifact(artifactId);
        }

        var dependents = await DependentOrdersAsync(artifactId, ct);
        if (dependents.Count > 0)
        {
            throw PlanningException.DeletionBlockedByDependents(
                [.. dependents.Select(o => $"task {o:D2}")]);
        }

        var feature = await _db.PlanningFeatures.FirstAsync(f => f.Id == artifact.FeatureId, ct);

        artifact.Delete(byHuman);

        _db.PlanningTaskDependencies.RemoveRange(artifact.BlockedBy);

        if (feature.Status == PlanningFeatureStatus.Review)
        {
            ReopenFeatureAfterDeletion(feature, artifact);
        }

        feature.AppendLog(DeletionAuditLine(artifact, byHuman, logNote), source ?? DeletionSource(byHuman));

        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, feature.ProjectId, ct);

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

    private static void TransitionOnArtifactCreate(PlanningFeature feature, PlanningArtifactKind kind)
    {
        if (feature.Status is PlanningFeatureStatus.Draft or PlanningFeatureStatus.Ready)
        {
            feature.SetStatus(
                PlanningFeatureStatus.InProgress, "artifact created → inProgress", PlanningLogSource.Auto);
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

    private async Task TryPromoteToReviewAsync(PlanningFeature feature, CancellationToken ct)
    {
        if (feature.Status is not (PlanningFeatureStatus.InProgress or PlanningFeatureStatus.Ready))
        {
            return;
        }

        var siblings = await _db.PlanningArtifacts
            .Where(a => a.FeatureId == feature.Id)
            .ToListAsync(ct);

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
