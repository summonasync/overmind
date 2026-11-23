using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

public static class TagsApi
{
    public static RouteGroupBuilder MapTagsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapGet("/tags", List);
        group.MapGet("/tags/counts", Counts);
        group.MapPut("/tags/{id:guid}", Update);

        return group;
    }

    private static async Task<IResult> List(OvermindDbContext db, CancellationToken ct)
    {
        var tags = await db.Tags
            .OrderBy(t => t.Name)
            .Select(t => new TagResponse(t.Id, t.Name, t.Color))
            .ToListAsync(ct);

        return Results.Ok(tags);
    }

    // Per-tag counts for the inbox's tag rail, scoped to one project's living journal notes —
    // the same population the stream endpoint answers for, so a rail number is the row count
    // the click on that row will actually produce.
    private static async Task<IResult> Counts(Guid projectId, OvermindDbContext db, CancellationToken ct)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return HttpErrors.NotFound($"Project '{projectId}' was not found.");
        }

        var counts = await db.Tags
            .OrderBy(t => t.Name)
            .Select(t => new TagCountResponse(
                t.Id,
                t.Name,
                t.EntryTags.Count(et =>
                    et.Entry.ProjectId == projectId
                    && et.Entry.State == ContextEntry.ActiveState
                    && et.Entry.Type == EntryType.Journal)))
            .ToListAsync(ct);

        return Results.Ok(counts);
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateTagRequest request,
        IValidator<UpdateTagRequest> validator,
        OvermindDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag is null)
        {
            return HttpErrors.NotFound($"Tag '{id}' was not found.");
        }

        if (request.Color is not null)
        {
            tag.SetColor(request.Color);
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(new TagResponse(tag.Id, tag.Name, tag.Color));
    }
}
