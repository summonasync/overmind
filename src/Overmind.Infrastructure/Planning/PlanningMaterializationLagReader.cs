using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningMaterializationLagReader(OvermindDbContext db, PlanningMaterializationQueue queue)
{
    public async Task<PlanningMaterializationLag> ReadAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);
        var pending = queue.PendingCountFor(projectId);

        return new PlanningMaterializationLag(
            pending,
            project?.LastMaterializationAttemptAt,
            project?.LastMaterializedAt,
            pending > 0 || AttemptBehindSuccess(project));
    }

    // An attempt newer than the last success also covers a pass killed mid-flight: the
    // mirror is not proven current, whatever the (absent) error field says.
    private static bool AttemptBehindSuccess(Project? project) =>
        project?.LastMaterializationAttemptAt is { } attempt &&
        (project.LastMaterializedAt is not { } success || attempt > success);
}
