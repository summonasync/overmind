using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Infrastructure.Wave;

public sealed class WaveParticipantService(OvermindDbContext db)
{
    public async Task<IReadOnlyList<WaveSessionPresence>> ListClaimlessSessionsAsync(
        Guid projectId, IReadOnlyList<LaneRow> roster, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(roster);

        var projects = await db.Projects
            .AsNoTracking()
            .Select(p => new { p.Id, p.ClaimStaleThresholdMinutes })
            .ToListAsync(ct);
        _ = projects.FirstOrDefault(p => p.Id == projectId)
            ?? throw PlanningException.MissingProject(projectId);

        var threshold = projects.Max(p => (int?)p.ClaimStaleThresholdMinutes)
                        ?? Project.DefaultClaimStaleThresholdMinutes;

        var surfaced = roster
            .Where(l => l.Holder is not null)
            .Select(l => (l.Holder!.Alias, l.Holder!.Vendor))
            .ToHashSet();

        var now = DateTime.UtcNow;
        var sessions = await db.AgentSessions
            .AsNoTracking()
            .ToListAsync(ct);

        return [.. sessions
            .Where(s => s.Liveness(now, threshold) == AgentSessionLiveness.Alive)
            .Where(s => !surfaced.Contains((s.Alias, s.Vendor)))
            .Select(s => new WaveSessionPresence(s.Alias, s.Vendor, s.Role, s.LastSeenAt))];
    }
}
