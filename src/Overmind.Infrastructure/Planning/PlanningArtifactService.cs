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

        var resolvedOrder = ResolveOrder(kind, order, featureId);

        var existing = await _db.PlanningArtifacts
            .FirstOrDefaultAsync(a => a.FeatureId == featureId && a.Kind == kind && a.Order == resolvedOrder, ct);

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
            var artifact = PlanningArtifact.Create(
                featureId, kind, body, resolvedOrder, review ?? PlanningReviewLevel.Light);
            _db.PlanningArtifacts.Add(artifact);
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw PlanningException.DuplicateArtifact(kind, resolvedOrder);
        }

        await SyncAsync(feature.ProjectId, ct);

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
        await SyncAsync(await GetProjectIdAsync(artifact.FeatureId, ct), ct);
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
        await SyncAsync(await GetProjectIdAsync(artifact.FeatureId, ct), ct);
    }

    public async Task AppendLogAsync(Guid artifactId, string note, CancellationToken ct = default)
    {
        var artifact = await LoadArtifactOrThrow(artifactId, ct);
        artifact.AppendLog(note);

        await _db.SaveChangesAsync(ct);
        await SyncAsync(await GetProjectIdAsync(artifact.FeatureId, ct), ct);
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
        await SyncAsync(projectId, ct);
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

    private int ResolveOrder(PlanningArtifactKind kind, int? order, Guid featureId)
    {
        if (kind != PlanningArtifactKind.Task)
        {
            return 0;
        }

        if (order.HasValue)
        {
            return order.Value;
        }

        var maxOrder = _db.PlanningArtifacts
            .Where(a => a.FeatureId == featureId && a.Kind == PlanningArtifactKind.Task)
            .Max(a => (int?)a.Order);

        return (maxOrder ?? -1) + 1;
    }

    private async Task SyncAsync(Guid projectId, CancellationToken ct)
    {
        try
        {
            await _materializer.SyncAsync(projectId, force: false, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Planning materialization sync failed for project {ProjectId}", projectId);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true;
}
