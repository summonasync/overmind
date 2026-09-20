using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed partial class PlanningFeatureService
{
    private readonly OvermindDbContext _db;
    private readonly IPlanningMaterializer _materializer;
    private readonly BacklogImportSeeder _seeder;
    private readonly ILogger<PlanningFeatureService> _logger;

    public PlanningFeatureService(
        OvermindDbContext db,
        IPlanningMaterializer materializer,
        BacklogImportSeeder seeder,
        ILogger<PlanningFeatureService> logger)
    {
        _db = db;
        _materializer = materializer;
        _seeder = seeder;
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
        catch (DbUpdateException ex) when (PlanningServiceGuard.IsUniqueViolation(ex))
        {
            throw PlanningException.DuplicateSlug(slug);
        }

        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, projectId, ct);
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
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, projectId, ct);
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
        await PlanningServiceGuard.ClearMaterializationAsync(_materializer, _logger, projectId, featureId, ct);

        _db.PlanningFeatures.Remove(feature);
        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, projectId, ct);
    }

    public async Task<PlanningFeature> SetCurrentAsync(
        Guid projectId, string slug, bool isCurrent, CancellationToken ct = default)
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

        if (isCurrent)
        {
            var previouslyCurrent = await _db.PlanningFeatures
                .Where(f => f.ProjectId == projectId && f.Id != feature.Id && f.IsCurrent)
                .ToListAsync(ct);

            foreach (var other in previouslyCurrent)
            {
                other.SetCurrent(false);
            }
        }

        feature.SetCurrent(isCurrent);

        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, projectId, ct);
        return feature;
    }

    public async Task<BacklogFeatureImportResult> ImportAsync(
        Guid projectId, BacklogFeatureImport import, CancellationToken ct = default)
    {
        if (!IsValidSlug(import.Slug))
        {
            throw PlanningException.InvalidSlug(import.Slug);
        }

        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            throw PlanningException.MissingProject(projectId);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var feature = await _db.PlanningFeatures
            .Include(f => f.Artifacts)
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == import.Slug, ct);

        bool featureCreated;
        if (feature is null)
        {
            feature = PlanningFeature.Restore(
                projectId, import.Slug, import.Title, import.Body,
                import.Status, import.Version, import.CreatedAt, import.CreatedAt, isCurrent: false);
            _db.PlanningFeatures.Add(feature);
            featureCreated = true;
        }
        else
        {
            featureCreated = false;
            if (!string.Equals(feature.Title, import.Title, StringComparison.Ordinal)
                || !string.Equals(feature.Body, import.Body, StringComparison.Ordinal))
            {
                feature.Update(import.Title, import.Body);
            }

            if (feature.Status != import.Status)
            {
                feature.SetStatus(import.Status);
            }
        }

        var artifactsCreated = 0;
        var artifactsUpdated = 0;
        var kept = new List<string>();
        var imported = new HashSet<Guid>();
        var taskContent = new Dictionary<string, PlanningArtifact>(StringComparer.Ordinal);
        foreach (var task in feature.Artifacts
                     .Where(a => a.Kind == PlanningArtifactKind.Task)
                     .OrderBy(a => a.Order))
        {
            taskContent.TryAdd(ContentKey(task.Body), task);
        }

        foreach (var artifact in import.Artifacts)
        {
            var existing = feature.Artifacts.FirstOrDefault(a =>
                a.Kind == artifact.Kind && (artifact.Kind != PlanningArtifactKind.Task || a.Order == artifact.Order));

            if (existing is null
                && artifact.Kind == PlanningArtifactKind.Task
                && artifact.ReassignedFrom.HasValue
                && taskContent.TryGetValue(ContentKey(artifact.Body), out var identical))
            {
                kept.Add(
                    $"matched: {artifact.SourcePath} (identical content to task order {identical.Order}; no new artifact)");
                imported.Add(identical.Id);
                continue;
            }

            if (existing is null)
            {
                var restored = PlanningArtifact.Restore(
                    feature.Id, artifact.Kind, artifact.Status, artifact.Review, artifact.Body,
                    artifact.Order, artifact.FrozenAt, version: 1, artifact.CreatedAt, artifact.CreatedAt,
                    preservedFrontmatter: PreservedFrontmatterJson.Serialize(artifact.Preserved),
                    brief: artifact.Brief);
                _db.PlanningArtifacts.Add(restored);
                if (artifact.Kind == PlanningArtifactKind.Task)
                {
                    taskContent.TryAdd(ContentHash.Normalize(restored.Body), restored);
                }

                if (artifact.ReassignedFrom.HasValue)
                {
                    kept.Add(
                        $"reassigned: {artifact.SourcePath} (order {artifact.ReassignedFrom.Value} taken; letter-suffixed task appended as order {artifact.Order})");
                }

                imported.Add(restored.Id);
                artifactsCreated++;
                continue;
            }

            var note = $"re-imported from {artifact.SourcePath}";
            var bodyChanged = !MatchesImportedBody(existing.Body, artifact.Body, note);
            if (bodyChanged)
            {
                existing.UpdateBody(artifact.Body, note);
            }

            var reviewChanged = existing.Review != artifact.Review;
            if (reviewChanged)
            {
                existing.SetReview(artifact.Review);
            }

            var statusChanged = existing.Status != artifact.Status;
            if (statusChanged)
            {
                if (artifact.Status == PlanningArtifactStatus.Validated)
                {
                    existing.Validate();
                }
                else
                {
                    existing.SetStatus(artifact.Status, bodyChanged ? null : note);
                }
            }

            var preservedJson = PreservedFrontmatterJson.Serialize(artifact.Preserved);
            var preservedChanged = !string.Equals(
                existing.PreservedFrontmatter, preservedJson, StringComparison.Ordinal);
            if (preservedChanged)
            {
                existing.SetPreservedFrontmatter(preservedJson);
            }

            var briefChanged = !string.Equals(existing.Brief, artifact.Brief, StringComparison.Ordinal);
            if (briefChanged)
            {
                existing.SetBrief(artifact.Brief);
            }

            if (bodyChanged || reviewChanged || statusChanged || preservedChanged || briefChanged)
            {
                artifactsUpdated++;
            }

            imported.Add(existing.Id);
        }

        foreach (var orphan in feature.Artifacts.Where(a => !imported.Contains(a.Id)))
        {
            kept.Add($"kept: {orphan.Kind.ToString().ToLowerInvariant()} {orphan.Order} not present on disk");
        }

        var seed = await _seeder.SeedAsync(projectId, feature.Id, import.SourcePaths, ct);

        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (PlanningServiceGuard.IsUniqueViolation(ex))
        {
            throw PlanningException.DuplicateSlug(import.Slug);
        }

        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, projectId, ct);
        return new BacklogFeatureImportResult(
            featureCreated, artifactsCreated, artifactsUpdated, kept, seed.Tracked, seed.Strategy);
    }

    private static string ContentKey(string body) =>
        ContentHash.Normalize(StripLogSections(body).TrimEnd());

    private static string StripLogSections(string body)
    {
        if (!body.Contains("<log>", StringComparison.Ordinal) || !body.Contains("</log>", StringComparison.Ordinal))
        {
            return body;
        }

        var lines = body.Split('\n');
        var kept = new List<string>(lines.Length);
        var skipping = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed == "<log>")
            {
                skipping = true;
                continue;
            }

            if (skipping)
            {
                if (trimmed == "</log>")
                {
                    skipping = false;
                }

                continue;
            }

            kept.Add(line);
        }

        return string.Join('\n', kept);
    }

    private static bool MatchesImportedBody(string existingBody, string incomingBody, string note)
    {
        if (string.Equals(existingBody, incomingBody, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var line in PlanningLog.Parse(existingBody))
        {
            if (line.Date is null || !string.Equals(line.Text, note, StringComparison.Ordinal))
            {
                continue;
            }

            var candidate = PlanningLog.Append(incomingBody, note, line.Date.Value.ToDateTime(TimeOnly.MinValue));
            if (string.Equals(candidate, existingBody, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.None, 2000)]
    private static partial Regex SlugPattern();

    private static bool IsValidSlug(string slug) =>
        slug.Length > 0 && slug.Length <= 64 && SlugPattern().IsMatch(slug);
}
