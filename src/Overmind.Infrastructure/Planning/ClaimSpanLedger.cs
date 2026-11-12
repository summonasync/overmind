using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

internal static class ClaimSpanLedger
{
    public static async Task OpenAsync(
        OvermindDbContext db,
        PlanningFeature feature,
        string alias,
        string vendor,
        Guid sessionId,
        DateTime now,
        CancellationToken ct)
    {
        var open = await db.FeatureClaimSpans
            .FirstOrDefaultAsync(s => s.FeatureId == feature.Id && s.ReleasedAt == null, ct);

        if (open is { } held && held.SessionId == sessionId)
        {
            return;
        }

        open?.Close(now);
        db.FeatureClaimSpans.Add(FeatureClaimSpan.Open(
            feature.ProjectId, feature.Id, alias, vendor, sessionId, now, feature.ClaimedByHuman));
    }

    public static async Task CloseOpenAsync(
        OvermindDbContext db, Guid featureId, DateTime now, CancellationToken ct)
    {
        var open = await db.FeatureClaimSpans
            .FirstOrDefaultAsync(s => s.FeatureId == featureId && s.ReleasedAt == null, ct);
        open?.Close(now);
    }
}
