using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

public static class ApprovalSettingsApi
{
    public static RouteGroupBuilder MapApprovalSettingsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapGet("/projects/{projectId:guid}/approval-settings", GetSettings);
        group.MapPut("/projects/{projectId:guid}/approval-settings", UpdateSettings);
        return group;
    }

    private static async Task<IResult> GetSettings(Guid projectId, OvermindDbContext db, CancellationToken ct)
    {
        var project = await db.Projects.FindAsync([projectId], ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        return Results.Ok(BuildResponse(project));
    }

    private static async Task<IResult> UpdateSettings(
        Guid projectId,
        UpdateApprovalSettingsRequest request,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var project = await db.Projects.FindAsync([projectId], ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        project.SetDelegateApprovals(request.DelegateApprovals);
        await db.SaveChangesAsync(ct);

        return Results.Ok(BuildResponse(project));
    }

    private static ApprovalSettingsResponse BuildResponse(Project project) =>
        new(project.DelegateApprovals);
}
