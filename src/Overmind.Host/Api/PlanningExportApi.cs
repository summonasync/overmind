using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class PlanningExportApi
{
    public static RouteGroupBuilder MapPlanningExportApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapPost("/projects/{projectId:guid}/planning/export", Export);

        return group;
    }

    internal static async Task<IResult> Export(
        Guid projectId,
        PlanningExportRequest request,
        IValidator<PlanningExportRequest> validator,
        PlanningExportService export,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        try
        {
            var result = await export.ExportAsync(projectId, request.TargetPath, ct);
            return Results.Ok(new PlanningExportResponse(
                result.ProjectId, result.TargetDirectory, result.FileCount));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
        catch (MaterializationException ex)
        {
            return MaterializationErrors.Map(ex);
        }
    }
}
