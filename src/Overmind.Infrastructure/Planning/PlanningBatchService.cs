using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningBatchService(
    OvermindDbContext db,
    PlanningArtifactService artifacts,
    NotificationsService notifications,
    PlanningMaterializationDispatcher materialization,
    PlanningBatchScope batch)
{
    public async Task<PlanningBatchResult> ApplyAsync(
        Guid projectId, string featureSlug, IReadOnlyList<PlanningBatchOp> ops, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ops);
        if (ops.Count == 0)
        {
            throw PlanningException.BatchEmpty();
        }

        var feature = await db.PlanningFeatures.FirstOrDefaultAsync(
            f => f.ProjectId == projectId && f.Slug == featureSlug, ct)
            ?? throw PlanningException.MissingFeature(featureSlug);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        batch.Active = true;
        var warnings = new List<string>();
        PlanningBatchResult result;
        try
        {
            result = await ApplyOpsAsync(feature, ops, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            try
            {
                notifications.DeliverStaged();
            }
            catch (Exception ex)
            {
                warnings.Add($"post-commit notification delivery failed: {ex.Message}");
            }
        }
        catch (DbUpdateException ex) when (PlanningServiceGuard.IsUniqueViolation(ex))
        {
            throw PlanningException.TaskBatchConcurrentWrite();
        }
        finally
        {
            batch.Active = false;
            batch.ClearStagedNotifications();
        }

        try
        {
            await materialization.DispatchAsync(feature.ProjectId, ct);
        }
        catch (Exception ex)
        {
            warnings.Add($"post-commit materialization dispatch failed: {ex.Message}");
        }

        return warnings.Count == 0 ? result : result with { Warnings = warnings };
    }

    private async Task<PlanningBatchResult> ApplyOpsAsync(
        PlanningFeature feature, IReadOnlyList<PlanningBatchOp> ops, CancellationToken ct)
    {
        var results = new List<PlanningBatchOpResult>(ops.Count);
        for (var i = 0; i < ops.Count; i++)
        {
            results.Add(await ApplyOpAsync(feature, i, ops[i], ct));
        }

        return new PlanningBatchResult(feature.Slug, results);
    }

    private async Task<PlanningBatchOpResult> ApplyOpAsync(
        PlanningFeature feature, int index, PlanningBatchOp op, CancellationToken ct)
    {
        try
        {
            return await ExecuteAsync(feature, op, ct);
        }
        catch (PlanningException ex)
        {
            throw PlanningException.BatchOp(index, ex);
        }
    }

    private Task<PlanningBatchOpResult> ExecuteAsync(
        PlanningFeature feature, PlanningBatchOp op, CancellationToken ct) =>
        op.Op switch
        {
            PlanningBatchOpName.Patch => PatchAsync(feature, op, ct),
            PlanningBatchOpName.AppendLog => AppendLogAsync(feature, op, ct),
            PlanningBatchOpName.SetStatus => SetStatusAsync(feature, op, ct),
            PlanningBatchOpName.UpsertTasks => UpsertTasksAsync(feature, op, ct),
            PlanningBatchOpName.Notify => NotifyAsync(feature, op, ct),
            _ => throw PlanningException.BatchOpInvalid(
                $"Op '{op.Op}' is not in the batch vocabulary: patch, append_log, set_status, upsert_tasks, notify."),
        };

    private async Task<PlanningBatchOpResult> PatchAsync(
        PlanningFeature feature, PlanningBatchOp op, CancellationToken ct)
    {
        var artifactId = await AddressArtifactAsync(feature, op, ct);
        if (op.Patch is null)
        {
            throw PlanningException.BatchOpInvalid(
                "patch requires the patch payload of its standalone tool: a section with its replacement content, " +
                "an acceptance-criteria tick or un-tick (index or exact text), a brief, or an amendment.");
        }

        var result = await artifacts.PatchAsync(artifactId, op.Patch, ct, PlanningLogSource.Agent);
        return new PlanningBatchOpResult(op.Op, result);
    }

    private async Task<PlanningBatchOpResult> AppendLogAsync(
        PlanningFeature feature, PlanningBatchOp op, CancellationToken ct)
    {
        var artifactId = await AddressArtifactAsync(feature, op, ct);
        if (string.IsNullOrWhiteSpace(op.Note))
        {
            throw PlanningException.BatchOpInvalid("append_log requires the note of its standalone tool.");
        }

        var artifact = await artifacts.AppendLogAsync(artifactId, op.Note, ct, PlanningLogSource.Agent);
        return new PlanningBatchOpResult(op.Op, artifact);
    }

    private async Task<PlanningBatchOpResult> SetStatusAsync(
        PlanningFeature feature, PlanningBatchOp op, CancellationToken ct)
    {
        var artifactId = await AddressArtifactAsync(feature, op, ct);
        if (op.Status is not { } status)
        {
            throw PlanningException.BatchOpInvalid("set_status requires the artifact status of its standalone tool.");
        }

        var result = await artifacts.SetStatusAsync(
            artifactId, status, PlanningLogSource.Agent, op.LogNote ?? string.Empty, ct, requireHumanProof: true);
        return new PlanningBatchOpResult(op.Op, result);
    }

    private async Task<PlanningBatchOpResult> UpsertTasksAsync(
        PlanningFeature feature, PlanningBatchOp op, CancellationToken ct)
    {
        if (op.Tasks is not { } tasks || tasks.Count == 0)
        {
            throw PlanningException.BatchOpInvalid(
                "upsert_tasks requires a non-empty tasks[] with the same item shape as planning_upsert_tasks.");
        }

        var result = await artifacts.UpsertTasksAsync(feature.Id, tasks, ct, PlanningLogSource.Agent);
        return new PlanningBatchOpResult(op.Op, result);
    }

    private async Task<PlanningBatchOpResult> NotifyAsync(
        PlanningFeature feature, PlanningBatchOp op, CancellationToken ct)
    {
        if (op.Kind is not { } kind)
        {
            throw PlanningException.BatchOpInvalid("notify requires the notification kind of its standalone tool.");
        }

        if (string.IsNullOrWhiteSpace(op.Text))
        {
            throw PlanningException.BatchOpInvalid("notify requires the notification text.");
        }

        var notification = await notifications.CreateAsync(
            kind, op.Text, feature.ProjectId, op.FeatureSlug ?? feature.Slug, op.Tab, op.From, ct)
            ?? throw PlanningException.MissingProject(feature.ProjectId);

        return new PlanningBatchOpResult(op.Op, notification);
    }

    private async Task<Guid> AddressArtifactAsync(
        PlanningFeature feature, PlanningBatchOp op, CancellationToken ct)
    {
        if (op.ArtifactId is not { } artifactId)
        {
            throw PlanningException.BatchOpInvalid(
                $"{op.Op.ToString().ToLowerInvariant()} requires artifactId addressing the artifact to mutate.");
        }

        var artifact = await db.PlanningArtifacts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == artifactId, ct)
            ?? StagedArtifact(feature.Id, artifactId)
            ?? throw PlanningException.MissingArtifact(artifactId);

        if (artifact.FeatureId != feature.Id)
        {
            throw PlanningException.BatchArtifactOutsideFeature(artifactId, feature.Slug);
        }

        return artifactId;
    }

    private PlanningArtifact? StagedArtifact(Guid featureId, Guid artifactId)
    {
        if (!batch.Active)
        {
            return null;
        }

        return db.PlanningArtifacts.Local.FirstOrDefault(a =>
            db.Entry(a).State == EntityState.Added &&
            a.Id == artifactId &&
            a.FeatureId == featureId);
    }
}
