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

    // The ledger is append-only, so a mid-hold rename closes the window that carried the old pair
    // and opens the current one under the new pair. The hold start rides along untouched: the
    // rescue digest reads the open span's ClaimedAt as "held since", and a rename is not a new hold.
    public static async Task ReattributeOpenAsync(
        OvermindDbContext db,
        PlanningFeature feature,
        string alias,
        string vendor,
        DateTime now,
        CancellationToken ct)
    {
        var open = await db.FeatureClaimSpans
            .FirstOrDefaultAsync(s => s.FeatureId == feature.Id && s.ReleasedAt == null, ct);

        // The session-id skip is an invariant guard, not a reachable branch: the claim door closes
        // the open span whenever the claim leaves the session, so a surviving open span whose
        // session differs from the feature's current claim session cannot be produced by any
        // write path (review-235-05 NOTE-1). The skip keeps the stale-alias span visible instead
        // of renaming it forward under the new alias — firing this branch means ledger surgery,
        // which must be deliberate, not a side effect of a rename.
        if (open is null || open.SessionId != feature.ClaimSessionId)
        {
            return;
        }

        open.Close(now);
        db.FeatureClaimSpans.Add(FeatureClaimSpan.Open(
            feature.ProjectId, feature.Id, alias, vendor, open.SessionId, open.ClaimedAt,
            feature.ClaimedByHuman));
    }
}
