using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class PlanningFeaturesApi
{
    public static RouteGroupBuilder MapPlanningFeaturesApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapGet("/projects/{projectId:guid}/planning/features", Board);
        group.MapPost("/projects/{projectId:guid}/planning/features", CreateFeature);
        group.MapGet("/projects/{projectId:guid}/planning/features/{slug}", GetFeature);
        group.MapPut("/projects/{projectId:guid}/planning/features/{slug}", UpdateFeature);
        group.MapDelete("/projects/{projectId:guid}/planning/features/{slug}", DeleteFeature);
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/current", SetCurrent);
        group.MapPost("/projects/{projectId:guid}/planning/queue/reorder", PlanningQueueApi.Reorder);

        return group;
    }

    internal static async Task<IResult> Board(
        Guid projectId, OvermindDbContext db, CancellationToken ct)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var features = await db.PlanningFeatures
            .Include(f => f.Artifacts)
            .Include(f => f.BlockedBy)
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.Slug)
            .ToListAsync(ct);

        var projectFeatures = features.ToDictionary(f => f.Id);
        return Results.Ok(features
            .Select(f => PlanningMapper.ToBoardItem(f, PlanningMapper.HasOpenBlocker(f, projectFeatures)))
            .ToArray());
    }

    internal static async Task<IResult> CreateFeature(
        Guid projectId,
        CreatePlanningFeatureRequest request,
        IValidator<CreatePlanningFeatureRequest> validator,
        PlanningFeatureService service,
        FeatureDependencyService dependencies,
        TaskDependencyService taskDependencies,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var priority = FeaturePriority.P2;
        if (request.Priority is not null)
        {
            if (!TryParseEnum(request.Priority, out FeaturePriority parsed))
            {
                return InvalidValue(nameof(request.Priority), Enum.GetNames<FeaturePriority>());
            }

            priority = parsed;
        }

        try
        {
            await service.CreateAsync(projectId, request.Slug, request.Title, request.Body ?? string.Empty, priority, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        var feature = await LoadFeatureWithArtifacts(db, projectId, request.Slug, ct);
        return Results.Created(
            $"/api/projects/{projectId}/planning/features/{request.Slug}",
            PlanningMapper.ToFeatureResponse(
                feature!, await dependencies.ListDependenciesAsync(projectId, request.Slug, ct),
                await BlockedStatesAsync(taskDependencies, feature!, ct)));
    }

    internal static async Task<IResult> GetFeature(
        Guid projectId,
        string slug,
        bool? includeArtifacts,
        OvermindDbContext db,
        FeatureDependencyService dependencies,
        TaskDependencyService taskDependencies,
        CancellationToken ct)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var feature = await LoadFeatureWithArtifacts(db, projectId, slug, ct);
        if (feature is null)
        {
            return HttpErrors.NotFound($"Feature '{slug}' was not found.");
        }

        if (includeArtifacts == false)
        {
            return Results.Ok(PlanningMapper.ToFeatureRosterResponse(
                feature, await dependencies.ListDependenciesAsync(projectId, slug, ct),
                await BlockedStatesAsync(taskDependencies, feature, ct)));
        }

        return Results.Ok(PlanningMapper.ToFeatureResponse(
            feature, await dependencies.ListDependenciesAsync(projectId, slug, ct),
            await BlockedStatesAsync(taskDependencies, feature, ct)));
    }

    internal static async Task<IResult> ListArtifacts(
        Guid projectId,
        string slug,
        string? kind,
        OvermindDbContext db,
        TaskDependencyService taskDependencies,
        CancellationToken ct)
    {
        PlanningArtifactKind? kindFilter = null;
        if (kind is not null)
        {
            if (!TryParseEnum(kind, out PlanningArtifactKind parsed))
            {
                return HttpErrors.BadRequest(
                    $"Unknown kind '{kind}'. Allowed values: spec, plan, task, result.");
            }

            kindFilter = parsed;
        }

        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var feature = await LoadFeatureWithArtifacts(db, projectId, slug, ct);
        if (feature is null)
        {
            return HttpErrors.NotFound($"Feature '{slug}' was not found.");
        }

        var blockedStates = await BlockedStatesAsync(taskDependencies, feature, ct);
        var artifacts = feature.Artifacts
            .Where(a => kindFilter is null || a.Kind == kindFilter)
            .OrderBy(a => a.Kind == PlanningArtifactKind.Task)
            .ThenBy(a => a.Kind)
            .ThenBy(a => a.Order)
            .Select(a => PlanningMapper.ToRosterItem(a, blockedStates))
            .ToArray();

        return Results.Ok(new PlanningArtifactRosterResponse(artifacts));
    }

    internal static async Task<IResult> UpdateFeature(
        Guid projectId,
        string slug,
        UpdatePlanningFeatureRequest request,
        IValidator<UpdatePlanningFeatureRequest> validator,
        PlanningFeatureService service,
        FeatureDependencyService dependencies,
        TaskDependencyService taskDependencies,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        FeaturePriority? priority = null;
        if (request.Priority is not null)
        {
            if (!TryParseEnum(request.Priority, out FeaturePriority parsed))
            {
                return InvalidValue(nameof(request.Priority), Enum.GetNames<FeaturePriority>());
            }

            priority = parsed;
        }

        try
        {
            await service.UpdateAsync(projectId, slug, request.Title, request.Body, request.Status, priority, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        var feature = await LoadFeatureWithArtifacts(db, projectId, slug, ct);
        return Results.Ok(PlanningMapper.ToFeatureResponse(
            feature!, await dependencies.ListDependenciesAsync(projectId, slug, ct),
            await BlockedStatesAsync(taskDependencies, feature!, ct)));
    }

    internal static async Task<IResult> DeleteFeature(
        Guid projectId, string slug, PlanningFeatureService service, CancellationToken ct)
    {
        try
        {
            await service.DeleteAsync(projectId, slug, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        return Results.NoContent();
    }

    internal static async Task<IResult> SetCurrent(
        Guid projectId,
        string slug,
        SetPlanningFeatureCurrentRequest request,
        PlanningFeatureService service,
        OvermindDbContext db,
        CancellationToken ct)
    {
        try
        {
            await service.SetCurrentAsync(projectId, slug, request.IsCurrent, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        var feature = await db.PlanningFeatures
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct);
        return Results.Ok(PlanningMapper.ToMutationResponse(feature!));
    }

    private static async Task<PlanningFeature?> LoadFeatureWithArtifacts(
        OvermindDbContext db, Guid projectId, string slug, CancellationToken ct)
    {
        return await db.PlanningFeatures
            .Include(f => f.Artifacts)
            .ThenInclude(a => a.Versions)
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct);
    }

    private static async Task<IReadOnlyDictionary<int, TaskBlockedState>> BlockedStatesAsync(
        TaskDependencyService taskDependencies, PlanningFeature feature, CancellationToken ct) =>
        (await taskDependencies.BlockedOrdersAsync(feature.Id, ct)).ToDictionary(s => s.Order);

    internal static bool TryParseEnum<TEnum>(string value, out TEnum parsed) where TEnum : struct, Enum
    {
        return Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);
    }

    private static IResult InvalidValue(string field, string[] allowed) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = [$"Must be one of: {string.Join(", ", allowed.Select(n => n.ToLowerInvariant()))}."],
        });
}
