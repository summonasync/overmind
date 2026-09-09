using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed partial class PlanningFeatureService
{
    private readonly OvermindDbContext _db;
    private readonly IPlanningMaterializer _materializer;
    private readonly ILogger<PlanningFeatureService> _logger;

    public PlanningFeatureService(
        OvermindDbContext db,
        IPlanningMaterializer materializer,
        ILogger<PlanningFeatureService> logger)
    {
        _db = db;
        _materializer = materializer;
        _logger = logger;
    }

    public async Task<PlanningFeature> CreateAsync(
        Guid projectId, string slug, string title, string body, CancellationToken ct = default)
    {
        if (!IsValidSlug(slug))
        {
            throw PlanningException.InvalidSlug(slug);
        }

        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            throw PlanningException.MissingProject(projectId);
        }

        var feature = PlanningFeature.Create(projectId, slug, title, body);
        _db.PlanningFeatures.Add(feature);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw PlanningException.DuplicateSlug(slug);
        }

        await SyncAsync(projectId, ct);
        return feature;
    }

    public async Task<PlanningFeature> UpdateAsync(
        Guid projectId, string slug, string? title, string? body,
        PlanningFeatureStatus? status, CancellationToken ct = default)
    {
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            throw PlanningException.MissingProject(projectId);
        }

        var feature = await _db.PlanningFeatures
            .Include(f => f.Artifacts)
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct);

        if (feature is null)
        {
            throw PlanningException.MissingFeature(slug);
        }

        feature.Update(title, body);

        if (status.HasValue && status.Value != feature.Status)
        {
            feature.SetStatus(status.Value);
        }

        await _db.SaveChangesAsync(ct);
        await SyncAsync(projectId, ct);
        return feature;
    }

    public async Task DeleteAsync(Guid projectId, string slug, CancellationToken ct = default)
    {
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            throw PlanningException.MissingProject(projectId);
        }

        var feature = await _db.PlanningFeatures
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct);

        if (feature is null)
        {
            throw PlanningException.MissingFeature(slug);
        }

        var featureId = feature.Id;
        await ClearAsync(projectId, featureId, ct);

        _db.PlanningFeatures.Remove(feature);
        await _db.SaveChangesAsync(ct);
        await SyncAsync(projectId, ct);
    }

    private async Task ClearAsync(Guid projectId, Guid featureId, CancellationToken ct)
    {
        try
        {
            await _materializer.ClearAsync(projectId, featureId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "Planning materialization clear failed for feature {FeatureId} in project {ProjectId}",
                featureId, projectId);
        }
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

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.None, 2000)]
    private static partial Regex SlugPattern();

    private static bool IsValidSlug(string slug) =>
        slug.Length > 0 && slug.Length <= 64 && SlugPattern().IsMatch(slug);
}
