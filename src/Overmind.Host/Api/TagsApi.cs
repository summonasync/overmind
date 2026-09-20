using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

public static class TagsApi
{
    public static RouteGroupBuilder MapTagsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapGet("/tags", List);
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
