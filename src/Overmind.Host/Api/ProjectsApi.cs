using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Manifest;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

public static class ProjectsApi
{
    public static RouteGroupBuilder MapProjectsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/projects");

        group.MapGet("/", ListAll);
        group.MapPost("/", Create);
        group.MapGet("/{id:guid}", GetById);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/favorite", SetFavorite);
        group.MapPost("/{id:guid}/manifest/import", ImportManifest);

        return group;
    }

    private static async Task<IResult> ListAll(OvermindDbContext db, CancellationToken ct)
    {
        var projects = await db.Projects
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var openCounts = await db.PlanningFeatures
            .Where(f => f.Status != PlanningFeatureStatus.Done)
            .GroupBy(f => f.ProjectId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var countByProject = openCounts.ToDictionary(c => c.Key, c => c.Count);

        return Results.Ok(projects
            .Select(p => ProjectMapper.ToResponse(p, countByProject.GetValueOrDefault(p.Id)))
            .ToArray());
    }

    private static async Task<IResult> Create(
        CreateProjectRequest request,
        IValidator<CreateProjectRequest> validator,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var name = request.Name.Trim();
        var conflict = await db.Projects.AnyAsync(p => p.Name == name, ct);
        if (conflict)
        {
            return HttpErrors.Conflict($"A project named '{name}' already exists.");
        }

        var project = Project.Create(name, request.DiskPath, request.Description ?? string.Empty);
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/projects/{project.Id}", ProjectMapper.ToResponse(project));
    }

    private static async Task<IResult> GetById(Guid id, OvermindDbContext db, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        return project is null
            ? HttpErrors.NotFound($"Project '{id}' was not found.")
            : Results.Ok(ProjectMapper.ToResponse(project));
    }

    private static async Task<IResult> SetFavorite(
        Guid id,
        FavoriteRequest request,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{id}' was not found.");
        }

        project.SetFavorite(request.IsFavorite);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ProjectMapper.ToResponse(project));
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateProjectRequest request,
        IValidator<UpdateProjectRequest> validator,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{id}' was not found.");
        }

        var name = request.Name.Trim();
        var conflict = await db.Projects.AnyAsync(p => p.Name == name && p.Id != id, ct);
        if (conflict)
        {
            return HttpErrors.Conflict($"A project named '{name}' already exists.");
        }

        var diskPathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(project.DiskPath, request.DiskPath.Trim(), diskPathComparison))
        {
            var hasActiveKits = await db.ProjectKits.AnyAsync(pk => pk.ProjectId == id && pk.IsActive, ct);
            var hasMaterializedFiles = await db.MaterializedFiles.AnyAsync(m => m.ProjectId == id, ct);
            var hasPlanningFiles = await db.PlanningMaterializedFiles.AnyAsync(m => m.ProjectId == id, ct);
            if (hasActiveKits || hasMaterializedFiles || hasPlanningFiles)
            {
                return HttpErrors.Conflict(
                    $"DiskPath of project '{id}' cannot be changed while kits are active or files are materialized; deactivate kits first.");
            }
        }

        var tracked = db.Entry(project);
        tracked.Property(p => p.Name).CurrentValue = name;
        tracked.Property(p => p.DiskPath).CurrentValue = request.DiskPath.Trim();
        tracked.Property(p => p.Description).CurrentValue = request.Description?.Trim() ?? string.Empty;
        if (request.SetManifest)
        {
            project.SetManifest(request.Manifest);
        }

        if (request.NumberStyle is not null || request.NumberPrefix is not null)
        {
            if (!TryParseNumberStyle(request.NumberStyle, out var style))
            {
                return InvalidValue(nameof(request.NumberStyle),
                    $"Must be one of: {string.Join(", ", Enum.GetNames<ProjectNumberStyle>().Select(n => n.ToLowerInvariant()))}.");
            }

            var targetStyle = style ?? project.NumberStyle;
            if (request.NumberPrefix is not null && targetStyle != ProjectNumberStyle.Key)
            {
                return InvalidValue(nameof(request.NumberPrefix),
                    "A number prefix is only used with numberStyle 'key'.");
            }

            var targetPrefix = targetStyle == ProjectNumberStyle.Key
                ? request.NumberPrefix?.Trim() ?? project.NumberPrefix
                : null;
            if (targetStyle == ProjectNumberStyle.Key && targetPrefix is null)
            {
                return InvalidValue(nameof(request.NumberPrefix),
                    "A number prefix is required for numberStyle 'key'.");
            }

            project.SetNumbering(targetStyle, targetPrefix);
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(ProjectMapper.ToResponse(project));
    }

    private static bool TryParseNumberStyle(string? value, out ProjectNumberStyle? style)
    {
        if (value is null)
        {
            style = null;
            return true;
        }

        style = Enum.TryParse<ProjectNumberStyle>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
                ? parsed
                : null;
        return style is not null;
    }

    private static IResult InvalidValue(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static async Task<IResult> Delete(Guid id, OvermindDbContext db, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{id}' was not found.");
        }

        var kitLinkedEntries = await db.SkillKitEntries
            .CountAsync(link => link.Entry.ProjectId == id, ct);
        if (kitLinkedEntries > 0)
        {
            return HttpErrors.Conflict(
                $"Project '{project.Name}' owns {kitLinkedEntries} entries that are linked into skill kits; remove those entries from their kits before deleting the project.");
        }

        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ImportManifest(
        Guid id,
        ManifestImporter importer,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{id}' was not found.");
        }

        ProjectManifestImport? import;
        try
        {
            import = await importer.ImportAsync(project.DiskPath, ct);
        }
        catch (MaterializationException ex)
        {
            return MaterializationErrors.Map(ex);
        }

        if (import is null)
        {
            return HttpErrors.NotFound(
                $"No AGENTS.md, CLAUDE.md or QWEN.md file was found in '{project.DiskPath}'; there is nothing to import.");
        }

        if (import.Body.Length > Project.MaxManifestLength)
        {
            return HttpErrors.BadRequest(
                $"The imported manifest exceeds the {Project.MaxManifestLength} character limit; trim it before importing.");
        }

        project.SetManifest(import.Body);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new ProjectManifestImportResponse(import.SourceFile, project.Manifest!));
    }
}
