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
    private readonly TaskDependencyService _taskDependencies;
    private readonly PlanningMaterializationDispatcher _materialization;
    private readonly ILogger<PlanningFeatureService> _logger;

    public PlanningFeatureService(
        OvermindDbContext db,
        IPlanningMaterializer materializer,
        BacklogImportSeeder seeder,
        TaskDependencyService taskDependencies,
        PlanningMaterializationDispatcher materialization,
        ILogger<PlanningFeatureService> logger)
    {
        _db = db;
        _materializer = materializer;
        _seeder = seeder;
        _taskDependencies = taskDependencies;
        _materialization = materialization;
        _logger = logger;
    }

    public async Task<PlanningFeature> CreateAsync(
        Guid projectId, string? slug, string title, string body,
        FeaturePriority priority = FeaturePriority.P2, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(slug) && !IsValidSlug(slug))
        {
            throw PlanningException.InvalidSlug(slug);
        }

        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            throw PlanningException.MissingProject(projectId);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var existing = await _db.PlanningFeatures
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.Slug)
            .Select(f => new FeatureNumberEntry(f.Slug, f.Status))
            .ToListAsync(ct);

        string resolvedSlug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            resolvedSlug = FeatureSlugGenerator.Compose(
                existing.Select(f => f.Slug).ToArray(), title);
        }
        else
        {
            if (existing.Any(f => f.Slug == slug))
            {
                throw PlanningException.DuplicateSlug(slug);
            }

            if (FeatureNumberGuard.FindCollision(existing, slug) is { } collision)
            {
                throw PlanningException.NumberCollision(
                    slug, collision.CollidingSlug, collision.CollidingStatus, collision.SuggestedNumber);
            }

            resolvedSlug = slug;
        }

        var feature = PlanningFeature.Create(projectId, resolvedSlug, title, body, priority);
        _db.PlanningFeatures.Add(feature);
        await AssignQueueTailAsync(feature, ct);

        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (PlanningServiceGuard.IsUniqueViolation(ex))
        {
            throw PlanningException.DuplicateSlug(resolvedSlug);
        }

        await _materialization.DispatchAsync(projectId, ct);
        return feature;
    }

    public async Task<PlanningFeature> UpdateAsync(
        Guid projectId, string slug, string? title, string? body,
        PlanningFeatureStatus? status, FeaturePriority? priority = null,
        string? lane = null, IReadOnlyList<string>? codeSurfaces = null, CancellationToken ct = default)
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

        if (status.HasValue && status.Value == PlanningFeatureStatus.Done && status.Value != feature.Status)
        {
            await FeatureDoneGate.RequireResultAsync(_db, feature, ct);
        }

        var resolvedLane = lane is null ? null : await ResolveLaneAsync(projectId, lane, ct);

        var laneMoved = false;
        feature.Update(title, body);

        if (lane is not null)
        {
            laneMoved = feature.SetLane(resolvedLane);
        }

        if (codeSurfaces is not null)
        {
            feature.SetCodeSurfaces(CodeSurfacesJson.Serialize(codeSurfaces), PlanningLogSource.Ui);
        }

        var doneFlip = false;
        if (status.HasValue && status.Value != feature.Status)
        {
            if (!PlanningFeatureTransitions.IsAllowed(feature.Status, status.Value))
            {
                throw PlanningException.InvalidFeatureTransition(slug, feature.Status, status.Value);
            }

            feature.SetStatus(
                status.Value,
                $"status moved to '{StatusText(status.Value)}' via ui",
                PlanningLogSource.Ui);
            ReleaseClaimOnDone(feature, PlanningLogSource.Ui);
            doneFlip = status.Value == PlanningFeatureStatus.Done;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (priority is { } target && target != feature.Priority)
        {
            feature.SetPriority(target);
            await AssignQueueTailAsync(feature, ct);
        }

        await _db.SaveChangesAsync(ct);

        if (doneFlip || laneMoved)
        {
            await RetireEmptyLanesAsync(projectId, feature, ct);
        }

        await tx.CommitAsync(ct);
        await _materialization.DispatchAsync(projectId, ct);
        return feature;
    }

    public async Task<PlanningFeature> UpdateAsync(
        Guid projectId, string slug, string? title, string? body,
        FeaturePriority? priority, string logNote, PlanningLogSource source,
        string? lane = null, IReadOnlyList<string>? codeSurfaces = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(logNote))
        {
            throw PlanningException.FeatureLogNoteRequired();
        }

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

        var resolvedLane = lane is null ? null : await ResolveLaneAsync(projectId, lane, ct);

        var laneMoved = false;
        feature.Update(title, body, logNote, source);

        if (lane is not null)
        {
            laneMoved = feature.SetLane(resolvedLane);
        }

        if (codeSurfaces is not null)
        {
            feature.SetCodeSurfaces(CodeSurfacesJson.Serialize(codeSurfaces));
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (priority is { } target && target != feature.Priority)
        {
            feature.SetPriority(target);
            await AssignQueueTailAsync(feature, ct);
        }

        await _db.SaveChangesAsync(ct);

        if (laneMoved)
        {
            await RetireEmptyLanesAsync(projectId, feature, ct);
        }

        await tx.CommitAsync(ct);
        await _materialization.DispatchAsync(projectId, ct);
        return feature;
    }

    public async Task<PlanningFeature> SetStatusAsync(
        Guid projectId, string slug, PlanningFeatureStatus status, string logNote,
        bool requireHumanTap = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(logNote))
        {
            throw PlanningException.FeatureLogNoteRequired();
        }

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

        if (status != feature.Status)
        {
            if (!PlanningFeatureTransitions.IsAllowed(feature.Status, status))
            {
                throw PlanningException.InvalidFeatureTransition(slug, feature.Status, status);
            }

            DateTime? humanTap = null;
            if (status == PlanningFeatureStatus.Done)
            {
                var resultContentUpdatedAt = await FeatureDoneGate.RequireResultAsync(_db, feature, ct);
                if (requireHumanTap)
                {
                    humanTap = await FeatureDoneGate.RequireHumanTapAsync(_db, feature, resultContentUpdatedAt, ct);
                }
            }

            feature.SetStatus(status, logNote, PlanningLogSource.Agent);
            if (humanTap is { } tappedAt)
            {
                feature.AppendLog(FeatureDoneGate.ProvenanceLine(feature, tappedAt), PlanningLogSource.Agent);
            }

            ReleaseClaimOnDone(feature, PlanningLogSource.Agent);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        await _db.SaveChangesAsync(ct);

        if (status == PlanningFeatureStatus.Done && feature.Status == PlanningFeatureStatus.Done)
        {
            await RetireEmptyLanesAsync(projectId, feature, ct);
        }

        await tx.CommitAsync(ct);
        await _materialization.DispatchAsync(projectId, ct);
        return feature;
    }

    private async Task<string?> ResolveLaneAsync(Guid projectId, string lane, CancellationToken ct)
    {
        var trimmed = lane.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > PlanningLane.MaxNameLength)
        {
            throw new ArgumentException(
                $"Lane '{trimmed}' exceeds the {PlanningLane.MaxNameLength} character cap.");
        }

        var roster = await _db.PlanningLanes
            .AsNoTracking()
            .Where(l => l.ProjectId == projectId)
            .Select(l => l.Name)
            .ToListAsync(ct);

        var match = roster.FirstOrDefault(
            name => string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match;
        }

        return BirthLaneAsync(projectId, trimmed, roster);
    }

    private string BirthLaneAsync(Guid projectId, string name, IReadOnlyList<string> roster)
    {
        if (!PlanningLane.IsAddressableName(name))
        {
            throw PlanningException.InvalidLaneName(name);
        }

        if (roster.Count >= LaneService.MaxLanesPerProject)
        {
            throw PlanningException.LaneRosterTooLarge(roster.Count + 1, LaneService.MaxLanesPerProject);
        }

        var lane = PlanningLane.Create(projectId, name, contour: null, serialOrder: null);
        _db.PlanningLanes.Add(lane);
        return lane.Name;
    }

    private async Task RetireEmptyLanesAsync(Guid projectId, PlanningFeature? actor, CancellationToken ct)
    {
        var lanes = await _db.PlanningLanes
            .Where(l => l.ProjectId == projectId)
            .ToListAsync(ct);
        if (lanes.Count == 0)
        {
            return;
        }

        var open = (await _db.PlanningFeatures
                .AsNoTracking()
                .Where(f => f.ProjectId == projectId
                            && f.Lane != null
                            && f.Status != PlanningFeatureStatus.Done)
                .Select(f => f.Lane!)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var retired = lanes.Where(l => !open.Contains(l.Name)).ToList();
        if (retired.Count == 0)
        {
            return;
        }

        foreach (var lane in retired)
        {
            _db.PlanningLanes.Remove(lane);
        }

        if (actor?.Lane is { } name &&
            retired.Any(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            actor.AppendLog($"lane '{name}' retired: no open features remain", PlanningLogSource.Auto);
        }

        await _db.SaveChangesAsync(ct);
    }

    private static void ReleaseClaimOnDone(PlanningFeature feature, PlanningLogSource source)
    {
        if (feature.Status != PlanningFeatureStatus.Done || feature.ClaimSessionId is null)
        {
            return;
        }

        feature.Release();
        feature.AppendLog("claim released on done-close", source);
    }

    public async Task<PlanningFeature> SetCodePinAsync(
        Guid projectId, string slug, string? branch, string? worktreePath,
        PlanningLogSource source, CancellationToken ct = default)
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

        feature.SetCodePin(branch, worktreePath, source);

        await _db.SaveChangesAsync(ct);
        await _materialization.DispatchAsync(projectId, ct);
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
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        await RetireEmptyLanesAsync(projectId, null, ct);
        await tx.CommitAsync(ct);
        await _materialization.DispatchAsync(projectId, ct);
    }

    public async Task<BacklogFeatureImportResult> ImportAsync(
        Guid projectId, BacklogFeatureImport import, CancellationToken ct = default)
    {
        import = CanonicalizeInlineSections(import);

        foreach (var artifact in import.Artifacts)
        {
            PointerMarkerGrammar.EnsureValid(artifact.Body);
        }

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
            .Include(f => f.BlockedBy)
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == import.Slug, ct);

        bool featureCreated;
        var statusRestored = false;
        if (feature is null)
        {
            feature = PlanningFeature.Restore(
                projectId, import.Slug, import.Title, import.Body,
                import.Status, import.Version, import.CreatedAt, import.CreatedAt,
                priority: import.Priority);
            _db.PlanningFeatures.Add(feature);
            await AssignQueueTailAsync(feature, ct);
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
                feature.SetStatus(
                    import.Status, "status restored from imported file", PlanningLogSource.Import);
                statusRestored = true;
            }

            if (feature.Priority != import.Priority)
            {
                feature.SetPriority(import.Priority);
            }
        }

        var artifactsCreated = 0;
        var artifactsUpdated = 0;
        var kept = new List<string>();
        var imported = new HashSet<Guid>();
        var taskContent = new Dictionary<string, PlanningArtifact>(StringComparer.Ordinal);
        var taskDependencyImports = new List<TaskDependencyImport>();
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
                taskDependencyImports.Add(
                    new TaskDependencyImport(identical, artifact.SourcePath, artifact.DependsOn ?? []));
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
                    taskDependencyImports.Add(
                        new TaskDependencyImport(restored, artifact.SourcePath, artifact.DependsOn ?? []));
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
                existing.UpdateBody(artifact.Body, note, PlanningLogSource.Import);
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
                    existing.SetStatus(
                        artifact.Status, "status restored from imported file", PlanningLogSource.Import);
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

            if (artifact.Kind == PlanningArtifactKind.Task)
            {
                taskDependencyImports.Add(
                    new TaskDependencyImport(existing, artifact.SourcePath, artifact.DependsOn ?? []));
            }

            imported.Add(existing.Id);
        }

        foreach (var orphan in feature.Artifacts.Where(a => !imported.Contains(a.Id)))
        {
            kept.Add($"kept: {orphan.Kind.ToString().ToLowerInvariant()} {orphan.Order} not present on disk");
        }

        kept.AddRange(await RestoreDependenciesAsync(feature, import.BlockedBy, ct));

        var seed = await _seeder.SeedAsync(projectId, feature.Id, import.SourcePaths, ct);

        try
        {
            await _db.SaveChangesAsync(ct);
            kept.AddRange(await _taskDependencies.RestoreFromImportAsync(feature, taskDependencyImports, ct));

            if (statusRestored)
            {
                await RetireEmptyLanesAsync(projectId, feature, ct);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (PlanningServiceGuard.IsUniqueViolation(ex))
        {
            throw PlanningException.DuplicateSlug(import.Slug);
        }

        await _materialization.DispatchInlineAsync(projectId, ct);
        return new BacklogFeatureImportResult(
            featureCreated, artifactsCreated, artifactsUpdated, kept, seed.Tracked, seed.Strategy);
    }

    // Import is additive: links present on the board but gone from the file survive (like
    // artifacts not on disk), and re-importing the same file links nothing twice.
    private async Task<IReadOnlyList<string>> RestoreDependenciesAsync(
        PlanningFeature feature, IReadOnlyList<string> blockerSlugs, CancellationToken ct)
    {
        if (blockerSlugs.Count == 0)
        {
            return [];
        }

        var notes = new List<string>();
        var wanted = blockerSlugs.Distinct(StringComparer.Ordinal).ToList();
        var blockers = await _db.PlanningFeatures
            .AsNoTracking()
            .Where(f => f.ProjectId == feature.ProjectId && wanted.Contains(f.Slug))
            .ToDictionaryAsync(f => f.Slug, f => f, StringComparer.Ordinal, ct);

        foreach (var slug in wanted)
        {
            if (string.Equals(slug, feature.Slug, StringComparison.Ordinal))
            {
                notes.Add($"unlinked: {feature.Slug} depends on {slug} — a feature cannot block itself");
                continue;
            }

            if (!blockers.TryGetValue(slug, out var blocker))
            {
                notes.Add($"unlinked: {feature.Slug} depends on {slug} — feature not on the board");
                continue;
            }

            if (feature.BlockedBy.Any(d => d.BlockedByFeatureId == blocker.Id))
            {
                continue;
            }

            feature.AddBlockedBy(blocker);
            notes.Add($"linked: {feature.Slug} depends on {blocker.Slug} (restored from imported file)");
        }

        return notes;
    }

    private async Task AssignQueueTailAsync(PlanningFeature feature, CancellationToken ct)
    {
        var bandMax = await _db.PlanningFeatures
            .Where(f => f.ProjectId == feature.ProjectId
                        && f.Priority == feature.Priority
                        && f.Id != feature.Id)
            .MaxAsync(f => (int?)f.QueuePosition, ct);

        feature.SetQueuePosition((bandMax ?? 0) + 1);
    }

    private static BacklogFeatureImport CanonicalizeInlineSections(BacklogFeatureImport import) => import with
    {
        Body = PlanningSectionCanonicalizer.Canonicalize(import.Body),
        Artifacts =
        [
            .. import.Artifacts.Select(a => a with { Body = PlanningSectionCanonicalizer.Canonicalize(a.Body) })
        ],
    };

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

            var candidate = PlanningLog.Append(
                incomingBody, note, line.Date.Value.ToDateTime(TimeOnly.MinValue), line.Source);
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

    private static string StatusText(PlanningFeatureStatus status)
    {
        var name = status.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
