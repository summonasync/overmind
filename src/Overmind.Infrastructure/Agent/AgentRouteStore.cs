using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Agent;

public sealed class AgentRouteStore(OvermindDbContext db)
{
    public const int MaxHotHeadSize = 64;
    public const int MaxListSize = 500;

    public async Task<AgentRoute> RecordAsync(
        string pattern,
        string domain,
        string targetAgent,
        string missReason,
        AgentRouteSource source,
        CancellationToken ct = default)
    {
        var normalized = RoutePatternNormalizer.Normalize(pattern);
        var existing = await db.AgentRoutes
            .FirstOrDefaultAsync(r => r.Pattern == normalized, ct);

        if (existing is { } route)
        {
            route.RegisterHit();
            route.RefreshAnalysis(domain, targetAgent, missReason);
            await db.SaveChangesAsync(ct);
            return route;
        }

        var created = AgentRoute.Create(normalized, domain, targetAgent, missReason, source);
        db.AgentRoutes.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    public async Task<IReadOnlyList<AgentRoute>> HotHeadAsync(int limit, CancellationToken ct = default)
    {
        var capped = Math.Clamp(limit, 1, MaxHotHeadSize);
        return await db.AgentRoutes
            .AsNoTracking()
            .OrderByDescending(r => r.HitCount)
            .ThenByDescending(r => r.LastHitAt)
            .Take(capped)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AgentRoute>> ListAsync(CancellationToken ct = default) =>
        await db.AgentRoutes
            .AsNoTracking()
            .OrderByDescending(r => r.HitCount)
            .ThenByDescending(r => r.LastHitAt)
            .Take(MaxListSize)
            .ToListAsync(ct);

    public async Task<bool> PromoteAsync(Guid id, CancellationToken ct = default)
    {
        var route = await db.AgentRoutes.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (route is null)
        {
            return false;
        }

        route.Promote();
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DemoteAsync(Guid id, CancellationToken ct = default)
    {
        var route = await db.AgentRoutes.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (route is null)
        {
            return false;
        }

        route.Demote();
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> ClearAsync(CancellationToken ct = default) =>
        await db.AgentRoutes.ExecuteDeleteAsync(ct);
}
