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
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.Slug)
            .ToListAsync(ct);

        return Results.Ok(features.Select(PlanningMapper.ToBoardItem).ToArray());
    }

    internal static async Task<IResult> CreateFeature(
        Guid projectId,
        CreatePlanningFeatureRequest request,
        IValidator<CreatePlanningFeatureRequest> validator,
        PlanningFeatureService service,
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
            PlanningMapper.ToFeatureResponse(feature!));
    }

    internal static async Task<IResult> GetFeature(
        Guid projectId, string slug, OvermindDbContext db, CancellationToken ct)
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

        return Results.Ok(PlanningMapper.ToFeatureResponse(feature));
    }

    internal static async Task<IResult> UpdateFeature(
        Guid projectId,
        string slug,
        UpdatePlanningFeatureRequest request,
        IValidator<UpdatePlanningFeatureRequest> validator,
        PlanningFeatureService service,
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
        return Results.Ok(PlanningMapper.ToFeatureResponse(feature!));
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

        var feature = await LoadFeatureWithArtifacts(db, projectId, slug, ct);
        return Results.Ok(PlanningMapper.ToFeatureResponse(feature!));
    }

    private static async Task<PlanningFeature?> LoadFeatureWithArtifacts(
        OvermindDbContext db, Guid projectId, string slug, CancellationToken ct)
    {
        return await db.PlanningFeatures
            .Include(f => f.Artifacts)
            .ThenInclude(a => a.Versions)
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct);
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
}
