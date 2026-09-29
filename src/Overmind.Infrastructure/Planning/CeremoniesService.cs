using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class CeremoniesService(OvermindDbContext db)
{
    public async Task<IReadOnlyList<CeremonyFeature>> ListAsync(CancellationToken ct)
    {
        var features = await db.PlanningFeatures
            .AsNoTracking()
            .Include(f => f.Artifacts)
            .Include(f => f.BlockedBy)
            .ThenInclude(d => d.BlockedBy)
            .Where(f => f.Status != PlanningFeatureStatus.Done)
            .Where(f =>
                f.Status == PlanningFeatureStatus.Review
                || f.Artifacts.Any(a =>
                    a.Kind == PlanningArtifactKind.Spec
                    && a.Status == PlanningArtifactStatus.Draft
                    && a.Body != string.Empty)
                || (f.Artifacts.Any(a =>
                        a.Kind == PlanningArtifactKind.Plan
                        && a.Status == PlanningArtifactStatus.Draft)
                    && f.Artifacts.Any(a =>
                        a.Kind == PlanningArtifactKind.Spec
                        && a.Status == PlanningArtifactStatus.Validated))
                || f.BlockedBy.Any(d =>
                    d.BlockedBy.Status != PlanningFeatureStatus.Done
                    && d.BlockedBy.Status != PlanningFeatureStatus.Review))
            .ToListAsync(ct);

        var projectNames = await db.Projects
            .AsNoTracking()
            .Where(p => features.Select(f => f.ProjectId).Distinct().Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        return features
            .OrderBy(
                f => projectNames[f.ProjectId],
                StringComparer.Ordinal)
            .ThenBy(f => f.Slug, StringComparer.Ordinal)
            .Select(f => new CeremonyFeature(
                f.ProjectId,
                projectNames[f.ProjectId],
                f.Slug,
                f.Title,
                Reasons(f)))
            .ToArray();
    }

    private static string[] Reasons(PlanningFeature feature)
    {
        var reasons = new List<string>(4);

        if (feature.Artifacts.Any(a =>
                a.Kind == PlanningArtifactKind.Spec
                && a.Status == PlanningArtifactStatus.Draft
                && a.Body != string.Empty))
        {
            reasons.Add("spec awaiting validation");
        }

        if (feature.Artifacts.Any(a => a.Kind == PlanningArtifactKind.Plan && a.Status == PlanningArtifactStatus.Draft)
            && feature.Artifacts.Any(a =>
                a.Kind == PlanningArtifactKind.Spec
                && a.Status == PlanningArtifactStatus.Validated))
        {
            reasons.Add("plan awaiting validation");
        }

        if (feature.Status == PlanningFeatureStatus.Review)
        {
            reasons.Add("result awaiting validation");
        }

        var openBlockers = feature.BlockedBy
            .Select(d => d.BlockedBy)
            .Count(b => !FeatureDependencyEntry.IsClosed(b.Status));
        if (openBlockers > 0)
        {
            reasons.Add($"blocked by {openBlockers}");
        }

        return [.. reasons];
    }
}
