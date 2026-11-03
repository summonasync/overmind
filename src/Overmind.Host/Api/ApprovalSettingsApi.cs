using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Agent;
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

    private static async Task<IResult> GetSettings(
        Guid projectId, OvermindDbContext db, ApprovalPolicy policy, CancellationToken ct)
    {
        var project = await db.Projects.FindAsync([projectId], ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        return Results.Ok(await BuildResponseAsync(project, policy, ct));
    }

    private static async Task<IResult> UpdateSettings(
        Guid projectId,
        UpdateApprovalSettingsRequest request,
        OvermindDbContext db,
        ApprovalPolicy policy,
        CancellationToken ct)
    {
        DelegateApprovalsOverride? mode = null;
        if (request.DelegateApprovals is not null)
        {
            if (!TryParseEnum(request.DelegateApprovals, out DelegateApprovalsOverride parsed))
            {
                return InvalidValue(
                    nameof(request.DelegateApprovals), Enum.GetNames<DelegateApprovalsOverride>());
            }

            mode = parsed;
        }

        var project = await db.Projects.FindAsync([projectId], ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        project.SetDelegateApprovals(mode ?? project.DelegateApprovals);
        await db.SaveChangesAsync(ct);

        return Results.Ok(await BuildResponseAsync(project, policy, ct));
    }

    private static bool TryParseEnum<TEnum>(string value, out TEnum parsed) where TEnum : struct, Enum
    {
        return Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);
    }

    private static IResult InvalidValue(string field, string[] allowed) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = [$"Must be one of: {string.Join(", ", allowed.Select(n => n.ToLowerInvariant()))}."],
        });

    private static async Task<ApprovalSettingsResponse> BuildResponseAsync(
        Project project, ApprovalPolicy policy, CancellationToken ct) =>
        new(project.DelegateApprovals, await policy.ResolveDelegateApprovalsAsync(project.Id, ct));
}
