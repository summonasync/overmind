using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

public static class ClaimSettingsApi
{
    public static RouteGroupBuilder MapClaimSettingsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapGet("/projects/{projectId:guid}/claim-settings", GetSettings);
        group.MapPut("/projects/{projectId:guid}/claim-settings", UpdateSettings);
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
        UpdateClaimSettingsRequest request,
        IValidator<UpdateClaimSettingsRequest> validator,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var project = await db.Projects.FindAsync([projectId], ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        project.SetClaimSettings(
            request.ClaimStaleThresholdMinutes,
            request.AgentNamingRules,
            request.RescuePolicy);
        await db.SaveChangesAsync(ct);

        return Results.Ok(BuildResponse(project));
    }

    private static ClaimSettingsResponse BuildResponse(Project project) =>
        new(project.ClaimStaleThresholdMinutes, project.AgentNamingRules, project.RescuePolicy);
}
