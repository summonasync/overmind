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
        PlanningReviewLevel? review, string? logNote, CancellationToken ct = default)
    {
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
            try
            {
                existing.UpdateBody(body, logNote);
            }
            catch (InvalidOperationException)
            {
                throw PlanningException.ChangeNoteRequired();
            }

            if (review.HasValue)
            {
                existing.SetReview(review.Value);
            }
        }
        else
        {
            created = PlanningArtifact.Create(
                featureId, kind, body, resolvedOrder, review ?? PlanningReviewLevel.Light);
            _db.PlanningArtifacts.Add(created);
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
                PlanningArtifact.Create(featureId, kind, body, resolvedOrder, review ?? PlanningReviewLevel.Light));

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

        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, await GetProjectIdAsync(artifact.FeatureId, ct), ct);
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
