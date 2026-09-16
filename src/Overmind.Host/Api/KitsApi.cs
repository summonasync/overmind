using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

public static class KitsApi
{
    public static RouteGroupBuilder MapKitsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapGet("/kits", ListAll);
        group.MapPost("/kits", Create);
        group.MapGet("/kits/{id:guid}", GetById);
        group.MapPut("/kits/{id:guid}", Update);
        group.MapDelete("/kits/{id:guid}", Delete);
        group.MapPost("/kits/{id:guid}/entries/{entryId:guid}", AddEntry);
        group.MapDelete("/kits/{id:guid}/entries/{entryId:guid}", RemoveEntry);
        group.MapPost("/projects/{projectId:guid}/kits/{kitId:guid}/activate", Activate);
        group.MapPost("/projects/{projectId:guid}/kits/{kitId:guid}/deactivate", Deactivate);
        group.MapGet("/projects/{projectId:guid}/drift", Drift);

        return group;
    }

    private static async Task<IResult> ListAll(OvermindDbContext db, CancellationToken ct)
    {
        var kits = await db.SkillKits
            .Include(k => k.SkillKitEntries)
            .OrderBy(k => k.Name)
            .ThenBy(k => k.Version)
            .ToListAsync(ct);

        return Results.Ok(kits.Select(KitMapper.ToResponse).ToArray());
    }

    private static async Task<IResult> Create(
        CreateKitRequest request,
        IValidator<CreateKitRequest> validator,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var kit = SkillKit.Create(request.Name, request.Version, request.Description ?? string.Empty);
        db.SkillKits.Add(kit);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (UniqueViolation.IsOn(ex, "SkillKits.Name"))
        {
            return HttpErrors.Conflict($"Kit name '{kit.Name}' is already taken.", title: "DuplicateName");
        }

        return Results.Created($"/api/kits/{kit.Id}", KitMapper.ToResponse(kit));
    }

    private static async Task<IResult> GetById(Guid id, OvermindDbContext db, CancellationToken ct)
    {
        var kit = await LoadKitAsync(db, id, ct);
        return kit is null
            ? HttpErrors.NotFound($"Kit '{id}' was not found.")
            : Results.Ok(KitMapper.ToResponse(kit));
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateKitRequest request,
        IValidator<UpdateKitRequest> validator,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var kit = await db.SkillKits.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kit is null)
        {
            return HttpErrors.NotFound($"Kit '{id}' was not found.");
        }

        var name = request.Name.Trim();
        var tracked = db.Entry(kit);
        tracked.Property(k => k.Name).CurrentValue = name;
        tracked.Property(k => k.Version).CurrentValue = request.Version;
        tracked.Property(k => k.Description).CurrentValue = request.Description ?? string.Empty;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (UniqueViolation.IsOn(ex, "SkillKits.Name"))
        {
            return HttpErrors.Conflict($"Kit name '{name}' is already taken.", title: "DuplicateName");
        }

        return Results.Ok(KitMapper.ToResponse(kit));
    }

    private static async Task<IResult> Delete(Guid id, OvermindDbContext db, CancellationToken ct)
    {
        var kit = await db.SkillKits.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kit is null)
        {
            return HttpErrors.NotFound($"Kit '{id}' was not found.");
        }

        var activeLinks = await db.ProjectKits.CountAsync(pk => pk.KitId == id && pk.IsActive, ct);
        if (activeLinks > 0)
        {
            return HttpErrors.Conflict($"Kit '{id}' is active for {activeLinks} project(s); deactivate it first.");
        }

        db.SkillKits.Remove(kit);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AddEntry(Guid id, Guid entryId, OvermindDbContext db, CancellationToken ct)
    {
        var kit = await db.SkillKits
            .Include(k => k.SkillKitEntries)
            .FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kit is null)
        {
            return HttpErrors.NotFound($"Kit '{id}' was not found.");
        }

        var entryExists = await db.ContextEntries.AnyAsync(e => e.Id == entryId, ct);
        if (!entryExists)
        {
            return HttpErrors.NotFound($"Entry '{entryId}' was not found.");
        }

        try
        {
            kit.AddEntry(entryId);
        }
        catch (InvalidOperationException ex)
        {
            return HttpErrors.Conflict(ex.Message);
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(KitMapper.ToResponse(kit));
    }

    private static async Task<IResult> RemoveEntry(Guid id, Guid entryId, OvermindDbContext db, CancellationToken ct)
    {
        var kit = await db.SkillKits
            .Include(k => k.SkillKitEntries)
            .FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kit is null)
        {
            return HttpErrors.NotFound($"Kit '{id}' was not found.");
        }

        var link = kit.SkillKitEntries.FirstOrDefault(e => e.EntryId == entryId);
        if (link is null)
        {
            return HttpErrors.NotFound($"Entry '{entryId}' is not part of kit '{id}'.");
        }

        db.SkillKitEntries.Remove(link);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Activate(
        Guid projectId,
        Guid kitId,
        OvermindDbContext db,
        IMaterializer materializer,
        CancellationToken ct)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var kitExists = await db.SkillKits.AnyAsync(k => k.Id == kitId, ct);
        if (!kitExists)
        {
            return HttpErrors.NotFound($"Kit '{kitId}' was not found.");
        }

        var otherActiveLink = await db.ProjectKits
            .FirstOrDefaultAsync(pk => pk.ProjectId == projectId && pk.IsActive && pk.KitId != kitId, ct);
        if (otherActiveLink is not null)
        {
            var activeKit = await db.SkillKits.FirstOrDefaultAsync(k => k.Id == otherActiveLink.KitId, ct);
            var activeKitLabel = activeKit is null
                ? $"'{otherActiveLink.KitId}'"
                : $"'{activeKit.Name}' ('{otherActiveLink.KitId}')";
            return HttpErrors.Conflict(
                $"Cannot activate kit '{kitId}': kit {activeKitLabel} is already active for project '{projectId}'. Deactivate it first.");
        }

        try
        {
            var report = await materializer.MaterializeAsync(projectId, kitId, ct);

            var link = await db.ProjectKits
                .FirstOrDefaultAsync(pk => pk.ProjectId == projectId && pk.KitId == kitId, ct);
            if (link is null)
            {
                db.ProjectKits.Add(ProjectKit.Create(projectId, kitId));
            }
            else
            {
                link.Activate();
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(KitMapper.ToResponse(report));
        }
        catch (MaterializationException ex)
        {
            return MaterializationError(ex);
        }
    }

    private static async Task<IResult> Deactivate(
        Guid projectId,
        Guid kitId,
        OvermindDbContext db,
        IMaterializer materializer,
        CancellationToken ct)
    {
        var link = await db.ProjectKits
            .FirstOrDefaultAsync(pk => pk.ProjectId == projectId && pk.KitId == kitId, ct);
        if (link is null)
        {
            return HttpErrors.NotFound($"Kit '{kitId}' is not linked to project '{projectId}'.");
        }

        try
        {
            var report = await materializer.DematerializeAsync(projectId, kitId, ct);
            link.Deactivate();
            await db.SaveChangesAsync(ct);
            return Results.Ok(KitMapper.ToResponse(report));
        }
        catch (MaterializationException ex)
        {
            return MaterializationError(ex);
        }
    }

    private static async Task<IResult> Drift(
        Guid projectId,
        OvermindDbContext db,
        IMaterializer materializer,
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
            return Results.Ok(items.Select(KitMapper.ToResponse).ToArray());
        }
        catch (MaterializationException ex)
        {
            return MaterializationError(ex);
        }
    }

    private static async Task<SkillKit?> LoadKitAsync(OvermindDbContext db, Guid id, CancellationToken ct)
    {
        return await db.SkillKits
            .Include(k => k.SkillKitEntries)
            .FirstOrDefaultAsync(k => k.Id == id, ct);
    }

    private static IResult MaterializationError(MaterializationException ex)
    {
        return ex.Kind switch
        {
            MaterializationFailureKind.NotFound => HttpErrors.NotFound(ex.Message),
            MaterializationFailureKind.PathTraversal or MaterializationFailureKind.InvalidTarget =>
                Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid materialization target", detail: ex.Message),
            MaterializationFailureKind.OverwriteBlocked => HttpErrors.Conflict(ex.Message),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Materialization failed", detail: ex.Message),
        };
    }
}
