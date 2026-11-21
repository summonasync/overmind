using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningArtifactDiffService
{
    private readonly OvermindDbContext _db;

    public PlanningArtifactDiffService(OvermindDbContext db)
    {
        _db = db;
    }

    public async Task<PlanningArtifactDiffResult> DiffAsync(
        Guid artifactId, int? fromVersion, int? toVersion, CancellationToken ct = default,
        Guid? callerProjectId = null)
    {
        var load = _db.PlanningArtifacts
            .Include(a => a.Versions)
            .Where(a => a.Id == artifactId);

        if (callerProjectId is { } caller)
        {
            // Folded into the load that already runs: the accepted call costs the same single query.
            load = load.Where(a => _db.PlanningFeatures
                .Any(f => f.Id == a.FeatureId && f.ProjectId == caller));
        }

        var artifact = await load.FirstOrDefaultAsync(ct);

        if (artifact is null)
        {
            throw callerProjectId is null
                ? PlanningException.MissingArtifact(artifactId)
                : await RefuseForeignOrMissingAsync(artifactId, callerProjectId.Value, ct);
        }

        var from = fromVersion ?? artifact.Version - 1;
        var to = toVersion ?? artifact.Version;

        if (from == to || from > to)
        {
            throw PlanningException.InvalidVersionRange(from, to);
        }

        return new PlanningArtifactDiffResult(
            artifact.Id,
            from,
            to,
            PlanningUnifiedDiff.Build(
                VersionBody(artifact, from), VersionBody(artifact, to)));
    }

    private async Task<PlanningException> RefuseForeignOrMissingAsync(
        Guid artifactId, Guid callerProjectId, CancellationToken ct)
    {
        var owner = await _db.PlanningArtifacts
            .Where(a => a.Id == artifactId)
            .Select(a => _db.PlanningFeatures.Where(f => f.Id == a.FeatureId).Select(f => f.ProjectId).First())
            .FirstOrDefaultAsync(ct);

        // The refused load returns nothing to read the owner off, so naming the owning project — which the
        // refusal text owes the caller — costs this one scalar lookup on the refusal path only.
        return owner is { } owning && owning != Guid.Empty
            ? PlanningException.ArtifactOutsideCallerProject(artifactId, owning, callerProjectId)
            : PlanningException.MissingArtifact(artifactId);
    }

    private static string VersionBody(PlanningArtifact artifact, int version)
    {
        if (version == artifact.Version)
        {
            return artifact.Body;
        }

        var snapshot = artifact.Versions.FirstOrDefault(v => v.Version == version);
        if (snapshot is not null)
        {
            return snapshot.Body;
        }

        throw PlanningException.ArtifactVersionNotFound(artifact.Id, version, artifact.Version);
    }
}
