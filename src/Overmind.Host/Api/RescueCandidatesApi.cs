using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class RescueCandidatesApi
{
    internal static async Task<IResult> Candidates(
        Guid projectId,
        RescueCandidateService rescue,
        CancellationToken ct)
    {
        try
        {
            var candidates = await rescue.ListAsync(projectId, ct);
            return Results.Ok(new RescueCandidatesResponse(
                projectId,
                DateTime.UtcNow,
                [.. candidates.Select(c => new RescueCandidateResponse(
                    c.Slug,
                    c.Title,
                    c.Status,
                    c.ClaimState,
                    c.ClaimedByAlias,
                    c.ClaimedByVendor,
                    c.ClaimedAt,
                    c.ClaimedByHuman,
                    c.PinnedBranch,
                    c.PinnedWorktreePath,
                    c.UpdatedAt))]));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }
}
