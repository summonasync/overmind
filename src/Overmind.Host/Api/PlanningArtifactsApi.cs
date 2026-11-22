using FluentValidation;
using Microsoft.AspNetCore.Mvc;
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
        group.MapGet("/planning/artifacts/{id:guid}/diff", Diff);
        group.MapPut("/planning/artifacts/{id:guid}", UpdateArtifact);
        group.MapDelete("/planning/artifacts/{id:guid}", DeleteArtifact);
        group.MapPost("/planning/artifacts/{id:guid}/status", SetStatus);
        group.MapPost("/planning/artifacts/{id:guid}/log", AppendLog);
        group.MapPut("/planning/artifacts/{id:guid}/log", EditLog);
        group.MapPost("/planning/artifacts/{id:guid}/validate", Validate);
        group.MapPost("/planning/artifacts/{id:guid}/decline", Decline);

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

        var soft = new List<object>();
        var brief = SoftCapSoften.Cap(request.Brief, PlanningArtifact.BriefMaxLength, soft);
        var logNote = SoftCapSoften.Cap(request.LogNote, SoftCapLimits.LogLine, soft);

        try
        {
            var artifact = await artifactService.UpsertAsync(
                feature.Id, request.Kind, request.Body, request.Order, request.Review, logNote, ct,
                brief: brief,
                source: PlanningDoorSource.Resolve(request.Source, fallbackSource),
                blockedBy: request.BlockedBy,
                dryRun: request.DryRun);

            var response = PlanningMapper.ToArtifactResponse(artifact);
            if (request.DryRun)
            {
                return Results.Ok(response with { DryRun = true });
            }

            response = response with { Warnings = SoftCapSoften.Merge(soft) };
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
        TaskDependencyService taskDependencies,
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

        var soft = new List<object>();
        var items = new PlanningTaskUpsert[request.Tasks.Length];
        for (var i = 0; i < request.Tasks.Length; i++)
        {
            var task = request.Tasks[i];
            items[i] = new PlanningTaskUpsert(
                task.Body,
                task.Order,
                task.Review,
                SoftCapSoften.Cap(task.LogNote, SoftCapLimits.LogLine, soft),
                SoftCapSoften.Cap(task.Brief, PlanningArtifact.BriefMaxLength, soft),
                task.BlockedBy);
        }

        try
        {
            var batch = await artifactService.UpsertTasksAsync(
                feature.Id,
                items,
                ct,
                PlanningDoorSource.Resolve(request.Source, PlanningLogSource.Agent),
                request.DryRun);

            var blockedStates = (await taskDependencies.BlockedOrdersAsync(feature.Id, ct))
                .ToDictionary(s => s.Order);

            var artifactItems = batch.Artifacts
                .Zip(batch.BlockedOrders)
                .Select(pair => ToBatchItem(pair.First, blockedStates, pair.Second))
                .ToArray();

            return Results.Ok(new CreatePlanningTasksResponse(
                batch.Created,
                batch.Updated,
                artifactItems,
                request.DryRun ? true : null,
                SoftCapSoften.Merge(soft)));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static PlanningArtifactResponse ToBatchItem(
        PlanningArtifact artifact,
        IReadOnlyDictionary<int, TaskBlockedState> blockedStates,
        IReadOnlyList<int> blockedOrders)
    {
        var response = PlanningMapper.ToArtifactResponse(artifact, blockedStates);
        return response with { BlockedBy = [.. blockedOrders] };
    }

    internal static async Task<IResult> GetArtifact(
        Guid id, OvermindDbContext db, CancellationToken ct)
    {
        var artifact = await db.PlanningArtifacts
            .Include(a => a.Versions)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (artifact is null)
        {
            return ArtifactNotFound(id);
        }

        return Results.Ok(PlanningMapper.ToArtifactResponse(artifact));
    }

    internal static async Task<IResult> GetVersions(
        Guid id, OvermindDbContext db, CancellationToken ct)
    {
        var exists = await db.PlanningArtifacts.AnyAsync(a => a.Id == id, ct);
        if (!exists)
        {
            return ArtifactNotFound(id);
        }

        var versions = await db.PlanningArtifactVersions
            .Where(v => v.ArtifactId == id)
            .OrderBy(v => v.Version)
            .ToListAsync(ct);

        return Results.Ok(versions.Select(PlanningMapper.ToVersionResponse).ToArray());
    }

    internal static async Task<IResult> Diff(
        Guid id,
        int? from,
        int? to,
        PlanningArtifactDiffService diffService,
        HttpRequest http,
        CancellationToken ct)
    {
        if (!AgentCallerScope.TryReadProjectId(http, out var callerProjectId, out var callerRefusal))
        {
            return callerRefusal!;
        }

        try
        {
            var result = await diffService.DiffAsync(id, from, to, ct, callerProjectId);
            return Results.Ok(new PlanningArtifactDiffResponse(
                result.ArtifactId, result.FromVersion, result.ToVersion, result.Diff));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
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
            return ArtifactNotFound(id);
        }

        var soft = new List<object>();
        var brief = SoftCapSoften.Cap(request.Brief, PlanningArtifact.BriefMaxLength, soft);
        var logNote = SoftCapSoften.Cap(request.LogNote, SoftCapLimits.LogLine, soft);

        try
        {
            await service.UpsertAsync(
                artifact.FeatureId, artifact.Kind, request.Body, artifact.Order,
                null, logNote, ct, brief: brief,
                source: PlanningDoorSource.Resolve(request.Source, PlanningLogSource.Ui));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        await db.Entry(artifact).ReloadAsync(ct);
        return Results.Ok(PlanningMapper.ToArtifactResponse(artifact) with { Warnings = SoftCapSoften.Merge(soft) });
    }

    internal static async Task<IResult> DeleteArtifact(
        Guid id,
        [FromBody] DeletePlanningArtifactRequest? request,
        PlanningArtifactService service,
        CancellationToken ct)
    {
        try
        {
            await service.DeleteAsync(
                id,
                byHuman: true,
                request?.LogNote,
                PlanningDoorSource.Resolve(request?.Source, PlanningLogSource.Ui),
                ct);
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

        var soft = new List<object>();
        var logNote = SoftCapSoften.Cap(request.LogNote, SoftCapLimits.LogLine, soft);

        try
        {
            var result = await service.SetStatusAsync(
                id, request.Status, PlanningLogSource.Ui, logNote!, ct);
            return Results.Ok(new SetPlanningArtifactStatusResponse(
                result.ArtifactId,
                result.Status,
                result.Version,
                result.LastLogLine,
                SoftCapSoften.Merge(soft, result.Warnings),
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
        HttpRequest http,
        CancellationToken ct) =>
        AppendLogForDoor(id, request, validator, service, PlanningLogSource.Ui, http, ct);

    internal static async Task<IResult> AppendLogForDoor(
        Guid id,
        AppendPlanningLogRequest request,
        IValidator<AppendPlanningLogRequest> validator,
        PlanningArtifactService service,
        PlanningLogSource fallbackSource,
        HttpRequest http,
        CancellationToken ct)
    {
        if (!AgentCallerScope.TryReadProjectId(http, out var callerProjectId, out var callerRefusal))
        {
            return callerRefusal!;
        }

        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var soft = new List<object>();
        var note = SoftCapSoften.Cap(request.Note, SoftCapLimits.LogLine, soft);

        try
        {
            var artifact = await service.AppendLogAsync(
                id, note!, ct,
                PlanningDoorSource.Resolve(request.Source, fallbackSource), callerProjectId);
            return Results.Ok(PlanningMapper.ToMutationResponse(artifact) with { Warnings = SoftCapSoften.Merge(soft) });
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    internal static Task<IResult> EditLog(
        Guid id,
        EditPlanningLogRequest request,
        IValidator<EditPlanningLogRequest> validator,
        PlanningArtifactService service,
        HttpRequest http,
        CancellationToken ct) =>
        EditLogForDoor(id, request, validator, service, PlanningLogSource.Ui, http, ct);

    internal static async Task<IResult> EditLogForDoor(
        Guid id,
        EditPlanningLogRequest request,
        IValidator<EditPlanningLogRequest> validator,
        PlanningArtifactService service,
        PlanningLogSource actor,
        HttpRequest http,
        CancellationToken ct)
    {
        if (!AgentCallerScope.TryReadProjectId(http, out var callerProjectId, out var callerRefusal))
        {
            return callerRefusal!;
        }

        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var soft = new List<object>();
        var text = SoftCapSoften.Cap(request.Text, SoftCapLimits.LogLine, soft);

        try
        {
            var artifact = await service.EditNewestLogAsync(id, text!, actor, ct, callerProjectId);
            return Results.Ok(PlanningMapper.ToMutationResponse(artifact) with { Warnings = SoftCapSoften.Merge(soft) });
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

    internal static async Task<IResult> Decline(
        Guid id,
        [FromBody] DeclinePlanningArtifactRequest? request,
        IValidator<DeclinePlanningArtifactRequest> validator,
        PlanningArtifactService service,
        CancellationToken ct)
    {
        var body = request ?? new DeclinePlanningArtifactRequest();

        var validation = await validator.ValidateAsync(body, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var soft = new List<object>();
        var reason = SoftCapSoften.Cap(body.Reason, SoftCapLimits.Reason, soft);

        try
        {
            var artifact = await service.DeclineAsync(id, reason, ct);
            return Results.Ok(PlanningMapper.ToMutationResponse(artifact) with { Warnings = SoftCapSoften.Merge(soft) });
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static IResult ArtifactNotFound(Guid id) =>
        HttpErrors.NotFound($"Artifact '{id}' was not found. {PlanningException.ArtifactNotFoundHint}");

    internal static async Task<PlanningFeature> ResolveFeatureOrThrow(
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
