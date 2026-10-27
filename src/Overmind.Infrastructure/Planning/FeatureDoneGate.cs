using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

internal static class FeatureDoneGate
{
    public static async Task<DateTime> RequireResultAsync(
        OvermindDbContext db, PlanningFeature feature, CancellationToken ct)
    {
        var resultContentUpdatedAt = await db.PlanningArtifacts
            .Where(a => a.FeatureId == feature.Id && a.Kind == PlanningArtifactKind.Result)
            .OrderByDescending(a => a.ContentUpdatedAt)
            .Select(a => (DateTime?)a.ContentUpdatedAt)
            .FirstOrDefaultAsync(ct);

        return resultContentUpdatedAt
            ?? throw PlanningException.FeatureResultRequired(feature.Slug);
    }

    public static async Task<DateTime> RequireHumanTapAsync(
        OvermindDbContext db, PlanningFeature feature, DateTime resultContentUpdatedAt, CancellationToken ct)
    {
        var tappedAt = await db.AgentNotifications
            .Where(n => n.Kind == NotificationKind.Approval
                && n.ProjectId == feature.ProjectId
                && n.FeatureSlug == feature.Slug
                && n.ReplyText != null && n.ReplyText != string.Empty
                && n.RepliedAt >= resultContentUpdatedAt)
            .OrderByDescending(n => n.RepliedAt)
            .Select(n => n.RepliedAt)
            .FirstOrDefaultAsync(ct);

        return tappedAt
            ?? throw PlanningException.FeatureDoneTapRequired(feature.Slug);
    }

    public static string ProvenanceLine(PlanningFeature feature, DateTime tappedAt)
    {
        var reviewer = feature.ClaimedByAlias is { } alias
            ? $" by {alias} ({feature.ClaimedByVendor ?? "unknown"})"
            : string.Empty;

        return $"done via agent review{reviewer}, approved by human tap {tappedAt:yyyy-MM-dd}";
    }
}
