using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
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

        return group;
    }

    private static async Task<IResult> ListAll(OvermindDbContext db, CancellationToken ct)
    {
        var projects = await db.Projects
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return Results.Ok(projects.Select(ProjectMapper.ToResponse).ToArray());
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
            if (hasActiveKits || hasMaterializedFiles)
            {
                return HttpErrors.Conflict(
                    $"DiskPath of project '{id}' cannot be changed while kits are active or files are materialized; deactivate kits first.");
            }
        }

        var tracked = db.Entry(project);
        tracked.Property(p => p.Name).CurrentValue = name;
        tracked.Property(p => p.DiskPath).CurrentValue = request.DiskPath.Trim();
        tracked.Property(p => p.Description).CurrentValue = request.Description?.Trim() ?? string.Empty;

        await db.SaveChangesAsync(ct);
        return Results.Ok(ProjectMapper.ToResponse(project));
    }

    private static async Task<IResult> Delete(Guid id, OvermindDbContext db, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null)
        {
            return HttpErrors.NotFound($"Project '{id}' was not found.");
        }

        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
