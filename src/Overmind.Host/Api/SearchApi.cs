using Microsoft.EntityFrameworkCore;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Search;

namespace Overmind.Host.Api;

public static class SearchApi
{
    public static RouteGroupBuilder MapSearchApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapGet("/search", Search);
        return group;
    }

    private static async Task<IResult> Search(
        string? q,
        Guid? projectId,
        int? limit,
        FtsSearchService search,
        OvermindDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["q"] = ["The q field is required."]
            });
        }

        if (projectId.HasValue &&
            !await db.Projects.AnyAsync(p => p.Id == projectId.Value, ct))
        {
            return HttpErrors.NotFound($"Project '{projectId.Value}' was not found.");
        }

        var effectiveLimit = Math.Clamp(limit ?? 20, 1, 100);
        var hits = await search.SearchAsync(projectId, q, effectiveLimit, ct);

        var responses = hits
            .Select(h => new SearchHitResponse(h.EntryId, h.Title, h.Type, h.Snippet, h.Rank))
            .ToArray();

        return Results.Ok(new SearchResponse(q, responses));
    }
}
