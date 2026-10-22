using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningOperationStore(OvermindDbContext db)
{
    public async Task<Guid?> ResolveProjectByArtifactAsync(Guid artifactId, CancellationToken ct) =>
        await db.PlanningArtifacts
            .Where(a => a.Id == artifactId)
            .Join(db.PlanningFeatures, a => a.FeatureId, f => f.Id, (_, f) => f.ProjectId)
            .Select(p => (Guid?)p)
            .FirstOrDefaultAsync(ct);

    public async Task<PlanningOperationRecord?> FindAsync(Guid projectId, Guid operationId, CancellationToken ct) =>
        await db.PlanningOperationRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.OperationId == operationId, ct);

    public async Task SaveAsync(
        Guid projectId,
        Guid operationId,
        string requestHash,
        int statusCode,
        string responseBody,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await db.PlanningOperationRecords
            .Where(r => r.ProjectId == projectId && r.CreatedAt < now.AddHours(-PlanningOperationRecord.RetentionHours))
            .ExecuteDeleteAsync(ct);

        var record = PlanningOperationRecord.Create(
            projectId, operationId, requestHash, statusCode, responseBody, now);
        db.PlanningOperationRecords.Add(record);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (PlanningServiceGuard.IsUniqueViolation(ex))
        {
            db.Entry(record).State = EntityState.Detached;
        }
    }
}
