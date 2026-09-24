using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

public static class PlanningMaterializationApi
{
    public static RouteGroupBuilder MapPlanningMaterializationApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapPost("/projects/{projectId:guid}/planning/materialize", Materialize);
        group.MapGet("/projects/{projectId:guid}/planning/drift", Drift);

        return group;
    }

    internal static async Task<IResult> Materialize(
        Guid projectId,
        MaterializePlanningRequest request,
        IPlanningMaterializer materializer,
        CancellationToken ct)
    {
        try
        {
            var report = await materializer.SyncAsync(projectId, request.Force, ct);
            return Results.Ok(ToResponse(report));
        }
        catch (MaterializationException ex)
        {
            return MaterializationError(ex);
        }
    }

    internal static async Task<IResult> Drift(
        Guid projectId,
        OvermindDbContext db,
        IPlanningMaterializer materializer,
        CancellationToken ct)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        try
        {
            var items = await materializer.DetectDriftAsync(projectId, ct);
            return Results.Ok(items.Select(i => new PlanningDriftResponse(
                i.ProjectId, i.FeatureSlug, i.RelativePath, i.Kind)).ToArray());
        }
        catch (MaterializationException ex)
        {
            return MaterializationError(ex);
        }
    }

    private static PlanningMaterializationReportResponse ToResponse(PlanningMaterializationReport report) =>
        new(
            report.ProjectId,
            report.Strategy,
            report.Files.Select(f => new PlanningMaterializedFileResponse(f.RelativePath, f.Sha256)).ToArray(),
            report.Warnings.ToArray(),
            report.Skipped.Select(s => new PlanningMaterializationSkipResponse(s.RelativePath, s.Reason)).ToArray());

    private static IResult MaterializationError(MaterializationException ex)
    {
        return ex.Kind switch
        {
            MaterializationFailureKind.NotFound => HttpErrors.NotFound(ex.Message),
            MaterializationFailureKind.PathTraversal or MaterializationFailureKind.InvalidTarget =>
                Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid materialization target",
                    detail: ex.Message),
            MaterializationFailureKind.OverwriteBlocked or MaterializationFailureKind.LocalEditsBlocked
                or MaterializationFailureKind.AllSkipped =>
                HttpErrors.Conflict(ex.Message),
            _ => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Materialization failed",
                detail: ex.Message),
        };
    }
}
