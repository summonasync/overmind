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
        Guid artifactId, int? fromVersion, int? toVersion, CancellationToken ct = default)
    {
        var artifact = await _db.PlanningArtifacts
            .Include(a => a.Versions)
            .FirstOrDefaultAsync(a => a.Id == artifactId, ct);

        if (artifact is null)
        {
            throw PlanningException.MissingArtifact(artifactId);
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
