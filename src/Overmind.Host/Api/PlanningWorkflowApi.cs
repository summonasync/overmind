using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class PlanningWorkflowApi
{
    public static RouteGroupBuilder MapPlanningWorkflowApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapGet("/projects/{projectId:guid}/planning/workflow", GetWorkflow);
        group.MapPut("/projects/{projectId:guid}/planning/workflow", UpdateWorkflow);
        return group;
    }

    internal static async Task<IResult> GetWorkflow(
        Guid projectId, OvermindDbContext db, PlanningPolicy policy, CancellationToken ct)
    {
        var project = await db.Projects.FindAsync([projectId], ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        return Results.Ok(await BuildResponseAsync(project, policy, ct));
    }

    internal static async Task<IResult> UpdateWorkflow(
        Guid projectId,
        UpdatePlanningWorkflowRequest request,
        OvermindDbContext db,
        PlanningPolicy policy,
        CancellationToken ct)
    {
        PlanningMaterialization? materialization = null;
        if (request.Materialization is not null)
        {
            if (!TryParseEnum(request.Materialization, out PlanningMaterialization parsed))
            {
                return InvalidValue(
                    nameof(request.Materialization), Enum.GetNames<PlanningMaterialization>());
            }

            materialization = parsed;
        }

        HumanValidationOverride? validation = null;
        if (request.RequireHumanValidation is not null)
        {
            if (!TryParseEnum(request.RequireHumanValidation, out HumanValidationOverride parsed))
            {
                return InvalidValue(
                    nameof(request.RequireHumanValidation), Enum.GetNames<HumanValidationOverride>());
            }

            validation = parsed;
        }

        var project = await db.Projects.FindAsync([projectId], ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        project.SetPlanningWorkflow(
            materialization ?? project.PlanningMaterialization,
            validation ?? project.RequireHumanValidation);
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

    private static async Task<PlanningWorkflowResponse> BuildResponseAsync(
        Project project, PlanningPolicy policy, CancellationToken ct)
    {
        return new PlanningWorkflowResponse(
            project.PlanningMaterialization,
            project.RequireHumanValidation,
            await policy.ResolveMaterializationAsync(project.Id, ct),
            await policy.ResolveValidationGateAsync(project.Id, ct));
    }
}
