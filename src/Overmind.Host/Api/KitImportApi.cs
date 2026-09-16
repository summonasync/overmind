using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Kits;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

public static class KitImportApi
{
    public static RouteGroupBuilder MapKitImportApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapPost("/kits/import/scan", Scan);
        group.MapPost("/kits/import", Import);
        group.MapPost("/kits/{id:guid}/reimport", Reimport);
        group.MapPost("/kits/{id:guid}/clear-source", ClearSource);

        return group;
    }

    private static async Task<IResult> Scan(
        KitScanRequest request,
        IValidator<KitScanRequest> validator,
        KitScanner scanner,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        try
        {
            var scan = await scanner.ScanAsync(request.Path, ct);
            return Results.Ok(KitImportMapper.ToResponse(scan));
        }
        catch (KitImportException ex)
        {
            return KitImportErrors.Map(ex);
        }
    }

    private static async Task<IResult> Import(
        KitImportRequest request,
        IValidator<KitImportRequest> validator,
        KitImporter importer,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        try
        {
            var selections = request.Kits
                .Select(k => new KitImportSelection(k.RelativePath, k.Name))
                .ToArray();
            var report = await importer.CommitAsync(request.Path, selections, bundled: false, ct);
            return Results.Ok(KitImportMapper.ToResponse(report));
        }
        catch (KitImportException ex)
        {
            return KitImportErrors.Map(ex);
        }
    }

    private static async Task<IResult> Reimport(Guid id, KitImporter importer, CancellationToken ct)
    {
        try
        {
            var report = await importer.ReimportAsync(id, bundled: false, ct);
            return Results.Ok(KitImportMapper.ToResponse(report));
        }
        catch (KitImportException ex)
        {
            return KitImportErrors.Map(ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Kit import failed",
                detail: "Import source files could not be read.");
        }
    }

    private static async Task<IResult> ClearSource(
        Guid id,
        OvermindDbContext db,
        KitImporter importer,
        CancellationToken ct)
    {
        try
        {
            await importer.ClearSourceAsync(id, ct);
            var kit = await db.SkillKits
                .Include(k => k.SkillKitEntries)
                .FirstAsync(k => k.Id == id, ct);
            return Results.Ok(KitMapper.ToResponse(kit));
        }
        catch (KitImportException ex)
        {
            return KitImportErrors.Map(ex);
        }
    }
}
