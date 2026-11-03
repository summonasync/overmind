using Microsoft.EntityFrameworkCore;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Agent.Routes;

public sealed class AgentRouteEntityStore(OvermindDbContext db) : IAgentRouteStore
{
    public const int MaxHotHeadSize = 500;

    public async Task<AgentRouteRecord> RecordAsync(AgentRouteRecording recording, CancellationToken ct = default)
    {
        var pattern = AgentRouteText.Normalize(recording.Pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        var domain = AgentRouteText.Clamp(recording.Domain, AgentRouteText.MaxDomainLength);
        var missReason = AgentRouteText.Clamp(recording.MissReason, AgentRouteText.MaxMissReasonLength);

        var existing = await db.AgentRoutes
            .FirstOrDefaultAsync(row => row.Pattern == pattern, ct);
        if (existing is { } row)
        {
            row.RegisterHit();
            row.Touch(DateTimeOffset.UtcNow);
            if (domain.Length > 0)
            {
                row.RefreshDomain(domain);
            }

            if (missReason.Length > 0)
            {
                row.RefreshMissReason(missReason);
            }

            if (row.HitCount >= AgentRouteMemoryStore.PromotionHits)
            {
                row.Promote();
            }

            await db.SaveChangesAsync(ct);
            return ToRecord(row);
        }

        var created = AgentRouteRow.Create(pattern, domain, missReason, AgentRouteSource.Learned, DateTimeOffset.UtcNow);
        db.AgentRoutes.Add(created);
        await db.SaveChangesAsync(ct);
        return ToRecord(created);
    }

    public async Task<IReadOnlyList<AgentRouteRecord>> HotHeadAsync(int maxResults, CancellationToken ct = default)
    {
        var capped = Math.Max(1, Math.Min(maxResults, MaxHotHeadSize));
        var rows = await db.AgentRoutes
            .AsNoTracking()
            .OrderByDescending(row => row.HitCount)
            .ThenByDescending(row => row.LastHitAt)
            .Take(capped)
            .ToListAsync(ct);
        return [.. rows.Select(ToRecord)];
    }

    public async Task<bool> PromoteAsync(Guid id, CancellationToken ct = default) =>
        await SetStatusAsync(id, AgentRouteStatus.Confirmed, ct);

    public async Task<bool> DemoteAsync(Guid id, CancellationToken ct = default) =>
        await SetStatusAsync(id, AgentRouteStatus.Provisional, ct);

    public async Task<int> ClearAsync(CancellationToken ct = default) =>
        await db.AgentRoutes.ExecuteDeleteAsync(ct);

    private async Task<bool> SetStatusAsync(Guid id, AgentRouteStatus status, CancellationToken ct)
    {
        var row = await db.AgentRoutes.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (row is null)
        {
            return false;
        }

        if (status == AgentRouteStatus.Confirmed)
        {
            row.Promote();
        }
        else
        {
            row.Demote();
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    private static AgentRouteRecord ToRecord(AgentRouteRow row) =>
        new(row.Id, row.Pattern, row.Domain, row.TargetAgent, row.MissReason,
            row.Status, row.HitCount, row.Source, row.LastHitAt, row.CreatedAt);
}
