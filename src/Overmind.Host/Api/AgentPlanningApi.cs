using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class AgentPlanningApi
{
    public static RouteGroupBuilder MapAgentPlanningApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/agent");

        group.MapGet("/projects/{projectId:guid}/planning/features", PlanningFeaturesApi.Board);
        group.MapPost("/projects/{projectId:guid}/planning/features", PlanningFeaturesApi.CreateFeature);
        group.MapGet("/projects/{projectId:guid}/planning/features/{slug}", PlanningFeaturesApi.GetFeature);
        group.MapGet("/projects/{projectId:guid}/planning/features/{slug}/current", Current);
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/current", PlanningFeaturesApi.SetCurrent);
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/status", SetFeatureStatus);
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/artifacts", PlanningArtifactsApi.CreateArtifact);
        group.MapPost("/planning/artifacts/{id:guid}/status", PlanningArtifactsApi.SetStatus);
        group.MapPost("/planning/artifacts/{id:guid}/log", PlanningArtifactsApi.AppendLog);
        group.MapPost("/planning/artifacts/{id:guid}/patch", Patch);
        group.MapPost("/planning/artifacts/{id:guid}/validate", Validate);

        return group;
    }

    internal static async Task<IResult> Validate(
        Guid id,
        PlanningArtifactService artifactService,
        PlanningPolicy policy,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var target = await db.PlanningArtifacts
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Kind,
                FeatureSlug = db.PlanningFeatures
                    .Where(f => f.Id == a.FeatureId)
                    .Select(f => f.Slug)
                    .First(),
                ProjectId = db.PlanningFeatures
                    .Where(f => f.Id == a.FeatureId)
                    .Select(f => f.ProjectId)
                    .First(),
            })
            .FirstOrDefaultAsync(ct);

        if (target is null)
        {
            return HttpErrors.NotFound($"Artifact '{id}' was not found.");
        }

        if (await policy.ResolveValidationGateAsync(target.ProjectId, ct))
        {
            return PlanningErrors.Map(PlanningException.HumanValidationRequired(target.Kind, target.FeatureSlug));
        }

        return await PlanningArtifactsApi.Validate(id, artifactService, ct);
    }

    internal static async Task<IResult> SetFeatureStatus(
        Guid projectId,
        string slug,
        SetPlanningFeatureStatusRequest request,
        IValidator<SetPlanningFeatureStatusRequest> validator,
        PlanningFeatureService service,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        if (request.Status is not (PlanningFeatureStatus.Done or PlanningFeatureStatus.InProgress))
        {
            return PlanningErrors.Map(PlanningException.FeatureStatusNotAllowedForAgent(request.Status));
        }

        try
        {
            await service.SetStatusAsync(projectId, slug, request.Status, request.LogNote, ct);
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }

        var feature = await db.PlanningFeatures
            .Include(f => f.Artifacts)
            .ThenInclude(a => a.Versions)
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct);
        return Results.Ok(PlanningMapper.ToFeatureResponse(feature!));
    }

    internal static async Task<IResult> Current(
        Guid projectId,
        string slug,
        PlanningPolicy policy,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var feature = await db.PlanningFeatures
            .Include(f => f.Artifacts)
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct);
        if (feature is null)
        {
            return HttpErrors.NotFound($"Feature '{slug}' was not found.");
        }

        var currentFeature = new PlanningCurrentFeature(feature.Slug, feature.Title, feature.Status);
        var gated = await policy.ResolveValidationGateAsync(projectId, ct);

        var spec = feature.Artifacts.FirstOrDefault(a => a.Kind == PlanningArtifactKind.Spec);
        var plan = feature.Artifacts.FirstOrDefault(a => a.Kind == PlanningArtifactKind.Plan);

        if (gated && (spec is null || spec.Status == PlanningArtifactStatus.Draft))
        {
            return Results.Ok(new PlanningCurrentResponse(
                "stop", currentFeature, null,
                "STOP: spec awaits human validation — it cannot be approved through MCP."));
        }

        if (gated && spec is { Status: PlanningArtifactStatus.Validated } &&
            (plan is null || plan.Status == PlanningArtifactStatus.Draft))
        {
            return Results.Ok(new PlanningCurrentResponse(
                "stop", currentFeature, null,
                "STOP: plan awaits human validation — it cannot be approved through MCP."));
        }

        var tasks = feature.Artifacts
            .Where(a => a.Kind == PlanningArtifactKind.Task)
            .OrderBy(a => a.Order)
            .ToList();

        if (tasks.Count == 0)
        {
            return Results.Ok(new PlanningCurrentResponse(
                "empty", currentFeature, null,
                "No tasks yet — create them from the validated plan."));
        }

        if (tasks.All(t => t.Status == PlanningArtifactStatus.Done))
        {
            return Results.Ok(new PlanningCurrentResponse(
                "empty", currentFeature, null,
                "All tasks are done — write the result artifact."));
        }

        var next = tasks.FirstOrDefault(
            t => t.Status is not PlanningArtifactStatus.Done and not PlanningArtifactStatus.Blocked)
            ?? tasks.First(t => t.Status != PlanningArtifactStatus.Done);

        var reason = next.Status == PlanningArtifactStatus.Blocked ? "The next task is blocked." : null;
        return Results.Ok(new PlanningCurrentResponse(
            "task", currentFeature, PlanningMapper.ToArtifactResponse(next), reason));
    }

    internal static async Task<IResult> Patch(
        Guid id,
        PatchPlanningArtifactRequest request,
        PlanningArtifactService service,
        CancellationToken ct)
    {
        if (!CarriesPatchShape(request))
        {
            return PlanningErrors.Map(PlanningException.InvalidPatchRequest());
        }

        try
        {
            var artifact = await service.PatchAsync(
                id,
                new PlanningArtifactPatch(
                    request.Section, request.Content, request.AcIndex, request.AcText, request.LogNote),
                ct);

            return Results.Ok(new PlanningArtifactPatchResponse(
                artifact.Id,
                artifact.Kind,
                request.Op!.Trim() == "tick_ac" ? "acceptance-criteria" : request.Section!,
                artifact.Version,
                artifact.Body.Length));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static bool CarriesPatchShape(PatchPlanningArtifactRequest request)
    {
        var op = request.Op?.Trim();

        if (op == "replace_section")
        {
            return !string.IsNullOrWhiteSpace(request.Section) &&
                   !string.IsNullOrWhiteSpace(request.Content) &&
                   request.AcIndex is null &&
                   request.AcText is null;
        }

        if (op == "tick_ac")
        {
            return request.Section is null &&
                   request.Content is null &&
                   (request.AcIndex is not null) != (request.AcText is not null) &&
                   (request.AcText is null || !string.IsNullOrWhiteSpace(request.AcText));
        }

        return false;
    }
}
