using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Kits;

// The activation seam: every write to project kit activation (API, bundled migration, any future
// door) goes through here, so the mandatory-global refusal holds regardless of entry point.
public sealed class KitActivationService(OvermindDbContext db, IMaterializer materializer)
{
    public async Task<MaterializationReport> ActivateAsync(
        Guid projectId, Guid kitId, bool force, CancellationToken ct)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            throw MaterializationException.MissingProject(projectId);
        }

        var kit = await db.SkillKits.FirstOrDefaultAsync(k => k.Id == kitId, ct)
            ?? throw MaterializationException.MissingKit(kitId);

        kit.EnsureProjectActivatable();

        var report = await materializer.MaterializeAsync(projectId, kitId, force, ct);

        var link = await db.ProjectKits.FirstOrDefaultAsync(
            pk => pk.ProjectId == projectId && pk.KitId == kitId, ct);
        if (link is null)
        {
            db.ProjectKits.Add(ProjectKit.Create(projectId, kitId));
        }
        else
        {
            link.Activate();
        }

        await db.SaveChangesAsync(ct);
        return report;
    }

    public async Task<MaterializationReport> DeactivateAsync(Guid projectId, Guid kitId, CancellationToken ct)
    {
        var link = await db.ProjectKits.FirstOrDefaultAsync(
            pk => pk.ProjectId == projectId && pk.KitId == kitId, ct)
            ?? throw KitActivationException.NotLinked(projectId, kitId);

        var report = await materializer.DematerializeAsync(projectId, kitId, ct);
        link.Deactivate();
        await db.SaveChangesAsync(ct);
        return report;
    }
}
