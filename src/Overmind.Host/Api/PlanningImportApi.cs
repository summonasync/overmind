using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Manifest;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class PlanningImportApi
{
    public static RouteGroupBuilder MapPlanningImportApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapPost("/projects/{projectId:guid}/planning/import/scan", Scan);
        group.MapPost("/projects/{projectId:guid}/planning/import", Import);

        return group;
    }

    internal static async Task<IResult> Scan(
        Guid projectId,
        PlanningImportScanRequest request,
        BacklogFeatureScanner scanner,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var importedSlugs = await db.PlanningFeatures
            .Where(f => f.ProjectId == projectId)
            .Select(f => f.Slug)
            .ToListAsync(ct);

        BacklogFeatureScanReport report;
        try
        {
            report = await scanner.ScanAsync(projectId, project.DiskPath, request.SubPath, importedSlugs, ct);
        }
        catch (MaterializationException ex)
        {
            return MaterializationErrors.Map(ex);
        }

        return Results.Ok(PlanningMapper.ToScanResponse(report) with
        {
            ManifestHint = ManifestHint(project.DiskPath)
        });
    }

    private static string? ManifestHint(string diskPath)
    {
        var root = Path.GetFullPath(diskPath);
        foreach (var candidate in ManifestImporter.Candidates)
        {
            // Same empty-file skip as the importer — a hint naming an empty AGENTS.md would lie
            // about what an import actually picks up.
            if (File.Exists(Path.Combine(root, candidate)) && new FileInfo(Path.Combine(root, candidate)).Length > 0)
            {
                return $"{candidate} found in the project root — import it as the project manifest from the config page (readable by agents via read_project_manifest).";
            }
        }

        return null;
    }

    internal static async Task<IResult> Import(
        Guid projectId,
        PlanningImportFolderRequest request,
        IValidator<PlanningImportFolderRequest> validator,
        BacklogImporter importer,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var folder = Path.Combine(project.DiskPath, "BACKLOG", request.Folder.Trim());
        try
        {
            var report = await importer.ImportFeatureAsync(projectId, project.DiskPath, folder, ct);
            return Results.Ok(PlanningMapper.ToImportResponse(report));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }
}
