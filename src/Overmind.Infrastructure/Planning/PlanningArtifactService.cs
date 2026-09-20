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

    public PlanningArtifactService(
        OvermindDbContext db,
        IPlanningMaterializer materializer,
        ILogger<PlanningArtifactService> logger)
    {
        _db = db;
        _materializer = materializer;
        _logger = logger;
    }

    public async Task<PlanningArtifact> UpsertAsync(
        Guid featureId, PlanningArtifactKind kind, string body, int? order,
        PlanningReviewLevel? review, string? logNote, CancellationToken ct = default,
        string? brief = null)
    {
        if (brief is not null && kind != PlanningArtifactKind.Task)
        {
            throw PlanningException.BriefNotAllowedForKind(kind);
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

        if (existing is not null)
        {
            if (existing.Body != body || !string.IsNullOrWhiteSpace(logNote))
            {
                try
                {
                    existing.UpdateBody(body, logNote);
                }
                catch (InvalidOperationException)
                {
                    throw PlanningException.ChangeNoteRequired();
                }
            }

            if (review.HasValue)
            {
                existing.SetReview(review.Value);
            }

            if (brief is not null)
            {
                existing.SetBrief(brief);
            }
        }
        else
        {
            created = PlanningArtifact.Create(
                featureId, kind, body, resolvedOrder, review ?? PlanningReviewLevel.Light, brief);
            _db.PlanningArtifacts.Add(created);
            TransitionOnArtifactCreate(feature, kind);
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

            _db.PlanningArtifacts.Remove(created);
            resolvedOrder = await ResolveOrderAsync(kind, null, featureId, ct);
            _db.PlanningArtifacts.Add(
                PlanningArtifact.Create(featureId, kind, body, resolvedOrder, review ?? PlanningReviewLevel.Light, brief));
            TransitionOnArtifactCreate(feature, kind);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException retryEx) when (PlanningServiceGuard.IsUniqueViolation(retryEx))
            {
                throw PlanningException.DuplicateArtifact(kind, resolvedOrder);
            }
        }

        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, feature.ProjectId, ct);

        return existing ?? await _db.PlanningArtifacts
            .FirstAsync(a => a.FeatureId == featureId && a.Kind == kind && a.Order == resolvedOrder, ct);
    }

    public async Task SetStatusAsync(
        Guid artifactId, PlanningArtifactStatus status, string logNote, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(logNote))
        {
            throw PlanningException.ChangeNoteRequired();
        }

        var artifact = await LoadArtifactOrThrow(artifactId, ct);
        var feature = await _db.PlanningFeatures.FirstAsync(f => f.Id == artifact.FeatureId, ct);

        try
        {
            artifact.SetStatus(status, logNote);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw PlanningException.InvalidStatusForKind(artifact.Kind, status);
        }
        catch (InvalidOperationException)
        {
            throw PlanningException.ChangeNoteRequired();
        }

        if (artifact.Kind == PlanningArtifactKind.Task && artifact.Status == PlanningArtifactStatus.Done)
        {
            await TryPromoteToReviewAsync(feature, ct);
        }

        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, feature.ProjectId, ct);
    }

    public async Task ValidateAsync(Guid artifactId, CancellationToken ct = default)
    {
        var artifact = await LoadArtifactOrThrow(artifactId, ct);

        try
        {
            artifact.Validate();
        }
        catch (InvalidOperationException)
        {
            throw PlanningException.CannotValidateTask();
        }

        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, await GetProjectIdAsync(artifact.FeatureId, ct), ct);
    }

    public async Task AppendLogAsync(Guid artifactId, string note, CancellationToken ct = default)
    {
        var artifact = await LoadArtifactOrThrow(artifactId, ct);
        artifact.AppendLog(note);

        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, await GetProjectIdAsync(artifact.FeatureId, ct), ct);
    }

    public async Task DeleteAsync(Guid artifactId, CancellationToken ct = default)
    {
        var artifact = await _db.PlanningArtifacts
            .Include(a => a.Versions)
            .FirstOrDefaultAsync(a => a.Id == artifactId, ct);

        if (artifact is null)
        {
            throw PlanningException.MissingArtifact(artifactId);
        }

        var projectId = await GetProjectIdAsync(artifact.FeatureId, ct);
        _db.PlanningArtifacts.Remove(artifact);
        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, projectId, ct);
    }

    private async Task<PlanningArtifact> LoadArtifactOrThrow(Guid artifactId, CancellationToken ct)
    {
        var artifact = await _db.PlanningArtifacts.FirstOrDefaultAsync(a => a.Id == artifactId, ct);
        if (artifact is null)
        {
            throw PlanningException.MissingArtifact(artifactId);
        }

        return artifact;
    }

    private async Task<Guid> GetProjectIdAsync(Guid featureId, CancellationToken ct)
    {
        return await _db.PlanningFeatures
            .Where(f => f.Id == featureId)
            .Select(f => f.ProjectId)
            .FirstAsync(ct);
    }

    private static void TransitionOnArtifactCreate(PlanningFeature feature, PlanningArtifactKind kind)
    {
        if (feature.Status is PlanningFeatureStatus.Draft or PlanningFeatureStatus.Ready ||
            (feature.Status is PlanningFeatureStatus.Review && kind is PlanningArtifactKind.Task))
        {
            feature.SetStatus(PlanningFeatureStatus.InProgress);
        }
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
        var hasSpec = siblings.Any(a => a.Kind == PlanningArtifactKind.Spec);
        var hasPlan = siblings.Any(a => a.Kind == PlanningArtifactKind.Plan);

        if (tasks.Count == 0 ||
            !tasks.All(t => t.Status == PlanningArtifactStatus.Done) ||
            !hasSpec ||
            !hasPlan)
        {
            return;
        }

        feature.SetStatus(PlanningFeatureStatus.Review);
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
