using FluentValidation;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class PlanningDependenciesApi
{
    public static RouteGroupBuilder MapPlanningDependenciesApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapGet("/projects/{projectId:guid}/planning/features/{slug}/dependencies", List);
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/dependencies", Add);
        group.MapDelete("/projects/{projectId:guid}/planning/features/{slug}/dependencies/{blockedBySlug}", Remove);

        return group;
    }

    internal static async Task<IResult> List(
        Guid projectId, string slug, FeatureDependencyService service, CancellationToken ct)
    {
        try
        {
            var dependencies = await service.ListDependenciesAsync(projectId, slug, ct);
            return Results.Ok(PlanningMapper.ToDependenciesResponse(dependencies));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> Add(
        Guid projectId,
        string slug,
        AddPlanningFeatureDependencyRequest request,
        IValidator<AddPlanningFeatureDependencyRequest> validator,
        FeatureDependencyService service,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        try
        {
            var dependencies = await service.AddDependencyAsync(projectId, slug, request.BlockedBy.Trim(), ct);
            return Results.Ok(PlanningMapper.ToDependenciesResponse(dependencies));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> Remove(
        Guid projectId, string slug, string blockedBySlug, FeatureDependencyService service, CancellationToken ct)
    {
        try
        {
            var dependencies = await service.RemoveDependencyAsync(projectId, slug, blockedBySlug, ct);
            return Results.Ok(PlanningMapper.ToDependenciesResponse(dependencies));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }
}
