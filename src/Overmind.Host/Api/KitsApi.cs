using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Kits;
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
        group.MapPost("/kits/{kitId:guid}/global/{agent}", GlobalDeploy);
        group.MapDelete("/kits/{kitId:guid}/global/{agent}", GlobalRemove);
        group.MapGet("/kits/global/state", GlobalState);

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

    private static async Task<IResult> Delete(
        Guid id,
        string? deployedFiles,
        OvermindDbContext db,
        HomeKitDeployer deployer,
        IReadOnlyList<AgentHomeSpec> agentHomes,
        CancellationToken ct)
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

        var fileRows = await db.GlobalMaterializedFiles.Where(f => f.KitId == id).ToListAsync(ct);
        var markerRows = await db.GlobalMarkerDeployments.Where(m => m.KitId == id).ToListAsync(ct);
        if (fileRows.Count == 0 && markerRows.Count == 0)
        {
            return await DeleteKitRow(db, kit, ct);
        }

        if (kit.Mandatory)
        {
            return HttpErrors.Conflict(
                $"Mandatory kit '{kit.Name}' cannot be deleted while globally deployed.",
                title: "MandatoryGlobalKit");
        }

        if (deployedFiles is null)
        {
            return HttpErrors.Conflict(
                $"Kit '{id}' is globally deployed; pass deployedFiles=keep or deployedFiles=remove.",
                title: "DeployedFilesChoiceRequired");
        }

        if (deployedFiles != "keep" && deployedFiles != "remove")
        {
            return HttpErrors.BadRequest(
                "deployedFiles must be 'keep' or 'remove'.", title: "InvalidDeployedFiles");
        }

        if (deployedFiles == "remove")
        {
            try
            {
                foreach (var platform in fileRows.Select(r => r.Platform)
                             .Concat(markerRows.Select(r => r.Platform))
                             .Distinct())
                {
                    var agentName = agentHomes.FirstOrDefault(s => s.Platform == platform)?.AgentName
                        ?? platform.ToString().ToLowerInvariant();
                    await deployer.DematerializeAsync(id, agentName, ct);
                }
            }
            catch (MaterializationException ex)
            {
                return MaterializationErrors.Map(ex);
            }
            catch (KitActivationException ex)
            {
                return KitActivationErrors.Map(ex);
            }

            fileRows = await db.GlobalMaterializedFiles.Where(f => f.KitId == id).ToListAsync(ct);
            markerRows = await db.GlobalMarkerDeployments.Where(m => m.KitId == id).ToListAsync(ct);
        }

        db.GlobalMaterializedFiles.RemoveRange(fileRows);
        db.GlobalMarkerDeployments.RemoveRange(markerRows);
        return await DeleteKitRow(db, kit, ct);
    }

    private static async Task<IResult> DeleteKitRow(OvermindDbContext db, SkillKit kit, CancellationToken ct)
    {
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
        bool? force,
        KitActivationService activation,
        CancellationToken ct)
    {
        try
        {
            var report = await activation.ActivateAsync(projectId, kitId, force == true, ct);
            return Results.Ok(KitMapper.ToResponse(report));
        }
        catch (MaterializationException ex)
        {
            return MaterializationErrors.Map(ex);
        }
        catch (KitActivationException ex)
        {
            return KitActivationErrors.Map(ex);
        }
    }

    private static async Task<IResult> Deactivate(
        Guid projectId,
        Guid kitId,
        KitActivationService activation,
        CancellationToken ct)
    {
        try
        {
            var report = await activation.DeactivateAsync(projectId, kitId, ct);
            return Results.Ok(KitMapper.ToResponse(report));
        }
        catch (MaterializationException ex)
        {
            return MaterializationErrors.Map(ex);
        }
        catch (KitActivationException ex)
        {
            return KitActivationErrors.Map(ex);
        }
    }

    private static async Task<IResult> GlobalDeploy(
        Guid kitId,
        string agent,
        bool? force,
        HomeKitDeployer deployer,
        CancellationToken ct)
    {
        try
        {
            var report = await deployer.DeployAsync(kitId, agent, force == true, ct);
            return Results.Ok(KitMapper.ToResponse(report));
        }
        catch (MaterializationException ex)
        {
            return MaterializationErrors.Map(ex);
        }
        catch (KitActivationException ex)
        {
            return KitActivationErrors.Map(ex);
        }
    }

    private static async Task<IResult> GlobalRemove(
        Guid kitId,
        string agent,
        HomeKitDeployer deployer,
        CancellationToken ct)
    {
        try
        {
            var report = await deployer.DematerializeAsync(kitId, agent, ct);
            return Results.Ok(KitMapper.ToResponse(report));
        }
        catch (MaterializationException ex)
        {
            return MaterializationErrors.Map(ex);
        }
        catch (KitActivationException ex)
        {
            return KitActivationErrors.Map(ex);
        }
    }

    private static async Task<IResult> GlobalState(
        HomeKitDeployer deployer,
        IReadOnlyList<AgentHomeSpec> agentHomes,
        CancellationToken ct)
    {
        var snapshot = await deployer.GetGlobalStateAsync(ct);
        var names = agentHomes
            .GroupBy(s => s.Platform)
            .ToDictionary(g => g.Key, g => g.First().AgentName);
        return Results.Ok(snapshot.Select(s => KitMapper.ToResponse(s, names)).ToArray());
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
            return MaterializationErrors.Map(ex);
        }
    }

    private static async Task<SkillKit?> LoadKitAsync(OvermindDbContext db, Guid id, CancellationToken ct)
    {
        return await db.SkillKits
            .Include(k => k.SkillKitEntries)
            .FirstOrDefaultAsync(k => k.Id == id, ct);
    }
}
