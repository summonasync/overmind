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
        group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/artifacts", PlanningArtifactsApi.CreateArtifact);
        group.MapPost("/planning/artifacts/{id:guid}/status", PlanningArtifactsApi.SetStatus);
        group.MapPost("/planning/artifacts/{id:guid}/log", PlanningArtifactsApi.AppendLog);
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
}
