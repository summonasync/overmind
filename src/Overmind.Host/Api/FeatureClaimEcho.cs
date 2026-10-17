using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class FeatureClaimEcho
{
    public static async Task<PlanningFeatureClaimResponse> ForAsync(
        FeatureClaimService claims, Guid projectId, PlanningFeature feature,
        bool overrideLaneCheck, CancellationToken ct) =>
        new(feature.Slug,
            feature.ClaimedByAlias,
            feature.ClaimedByVendor,
            feature.ClaimedAt,
            feature.ClaimSessionId is null
                ? null
                : (await claims.ClaimLivenessAsync(projectId, [feature], ct))
                    .GetValueOrDefault(feature.Id),
            feature.ClaimedByHuman,
            feature.Lane,
            overrideLaneCheck);
}
