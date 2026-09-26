using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class PlanningArtifactsApi
{
    public static RouteGroupBuilder MapPlanningArtifactsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/artifacts", CreateArtifact);
        group.MapGet("/planning/artifacts/{id:guid}", GetArtifact);
        group.MapGet("/planning/artifacts/{id:guid}/versions", GetVersions);
        group.MapPut("/planning/artifacts/{id:guid}", UpdateArtifact);
        group.MapDelete("/planning/artifacts/{id:guid}", DeleteArtifact);
        group.MapPost("/planning/artifacts/{id:guid}/status", SetStatus);
        group.MapPost("/planning/artifacts/{id:guid}/log", AppendLog);
        group.MapPost("/planning/artifacts/{id:guid}/validate", Validate);

        return group;
    }

    internal static Task<IResult> CreateArtifact(
        Guid projectId,
        string slug,
        CreatePlanningArtifactRequest request,
        IValidator<CreatePlanningArtifactRequest> validator,
        PlanningFeatureService featureService,
        PlanningArtifactService artifactService,
        OvermindDbContext db,
        CancellationToken ct) =>
        CreateArtifactForDoor(
            projectId, slug, request, validator, featureService, artifactService, db,
            PlanningLogSource.Ui, ct);

    internal static async Task<IResult> CreateArtifactForDoor(
        Guid projectId,
        string slug,
        CreatePlanningArtifactRequest request,
        IValidator<CreatePlanningArtifactRequest> validator,
        PlanningFeatureService featureService,
        PlanningArtifactService artifactService,
        OvermindDbContext db,
        PlanningLogSource fallbackSource,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        PlanningFeature feature;
        try
        {
            feature = await ResolveFeatureOrThrow(db, projectId, slug, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        try
        {
            var artifact = await artifactService.UpsertAsync(
                feature.Id, request.Kind, request.Body, request.Order, request.Review, request.LogNote, ct,
                brief: request.Brief,
                source: PlanningDoorSource.Resolve(request.Source, fallbackSource));

            var response = PlanningMapper.ToArtifactResponse(artifact);
            var isNew = artifact.Version == 1;
            return isNew
                ? Results.Created($"/api/planning/artifacts/{artifact.Id}", response)
                : Results.Ok(response);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> CreateTasks(
        Guid projectId,
        string slug,
        CreatePlanningTasksRequest request,
        IValidator<CreatePlanningTasksRequest> validator,
        PlanningArtifactService artifactService,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        PlanningFeature feature;
        try
        {
            feature = await ResolveFeatureOrThrow(db, projectId, slug, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        try
        {
            var batch = await artifactService.UpsertTasksAsync(
                feature.Id,
                request.Tasks.Select(t => new PlanningTaskUpsert(t.Body, t.Order, t.Review, t.LogNote, t.Brief))
                    .ToArray(),
                ct,
                PlanningDoorSource.Resolve(request.Source, PlanningLogSource.Agent));

            return Results.Ok(new CreatePlanningTasksResponse(
                batch.Created,
                batch.Updated,
                batch.Artifacts.Select(PlanningMapper.ToArtifactResponse).ToArray()));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> GetArtifact(
        Guid id, OvermindDbContext db, CancellationToken ct)
    {
        var artifact = await db.PlanningArtifacts
            .Include(a => a.Versions)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (artifact is null)
        {
            return HttpErrors.NotFound($"Artifact '{id}' was not found.");
        }

        return Results.Ok(PlanningMapper.ToArtifactResponse(artifact));
    }

    internal static async Task<IResult> GetVersions(
        Guid id, OvermindDbContext db, CancellationToken ct)
    {
        var exists = await db.PlanningArtifacts.AnyAsync(a => a.Id == id, ct);
        if (!exists)
        {
            return HttpErrors.NotFound($"Artifact '{id}' was not found.");
        }

        var versions = await db.PlanningArtifactVersions
            .Where(v => v.ArtifactId == id)
            .OrderBy(v => v.Version)
            .ToListAsync(ct);

        return Results.Ok(versions.Select(PlanningMapper.ToVersionResponse).ToArray());
    }

    internal static async Task<IResult> UpdateArtifact(
        Guid id,
        UpdatePlanningArtifactRequest request,
        IValidator<UpdatePlanningArtifactRequest> validator,
        PlanningArtifactService service,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var artifact = await db.PlanningArtifacts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (artifact is null)
        {
            return HttpErrors.NotFound($"Artifact '{id}' was not found.");
        }

        try
        {
            await service.UpsertAsync(
                artifact.FeatureId, artifact.Kind, request.Body, artifact.Order,
                null, request.LogNote, ct, brief: request.Brief,
                source: PlanningDoorSource.Resolve(request.Source, PlanningLogSource.Ui));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        await db.Entry(artifact).ReloadAsync(ct);
        return Results.Ok(PlanningMapper.ToArtifactResponse(artifact));
    }

    internal static async Task<IResult> DeleteArtifact(
        Guid id, PlanningArtifactService service, CancellationToken ct)
    {
        try
        {
            await service.DeleteAsync(id, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        return Results.NoContent();
    }

    internal static async Task<IResult> SetStatus(
        Guid id,
        SetPlanningArtifactStatusRequest request,
        IValidator<SetPlanningArtifactStatusRequest> validator,
        PlanningArtifactService service,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        try
        {
            var result = await service.SetStatusAsync(
                id, request.Status, PlanningLogSource.Ui, request.LogNote, ct);
            return Results.Ok(new SetPlanningArtifactStatusResponse(
                result.ArtifactId,
                result.Status,
                result.Version,
                result.LastLogLine,
                result.Warnings,
                result.FeatureStatusChanged));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static Task<IResult> AppendLog(
        Guid id,
        AppendPlanningLogRequest request,
        IValidator<AppendPlanningLogRequest> validator,
        PlanningArtifactService service,
        CancellationToken ct) =>
        AppendLogForDoor(id, request, validator, service, PlanningLogSource.Ui, ct);

    internal static async Task<IResult> AppendLogForDoor(
        Guid id,
        AppendPlanningLogRequest request,
        IValidator<AppendPlanningLogRequest> validator,
        PlanningArtifactService service,
        PlanningLogSource fallbackSource,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        try
        {
            var artifact = await service.AppendLogAsync(
                id, request.Note, ct,
                PlanningDoorSource.Resolve(request.Source, fallbackSource));
            return Results.Ok(PlanningMapper.ToMutationResponse(artifact));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static async Task<IResult> Validate(
        Guid id, PlanningArtifactService service, CancellationToken ct)
    {
        try
        {
            var artifact = await service.ValidateAsync(id, "validated via UI", PlanningLogSource.Ui, ct: ct);
            return Results.Ok(PlanningMapper.ToMutationResponse(artifact));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static async Task<PlanningFeature> ResolveFeatureOrThrow(
        OvermindDbContext db, Guid projectId, string slug, CancellationToken ct)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            throw PlanningException.MissingProject(projectId);
        }

        var feature = await db.PlanningFeatures
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct);

        if (feature is null)
        {
            throw PlanningException.MissingFeature(slug);
        }

        return feature;
    }
}
