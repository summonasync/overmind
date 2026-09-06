using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Search;
using Overmind.Infrastructure.VectorSearch;

namespace Overmind.Host.Api;

public static class SearchApi
{
    private const string SemanticUnavailableDetail =
        "Semantic search is unavailable because the vector store is down. Use mode=fulltext.";

    public static RouteGroupBuilder MapSearchApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapGet("/search", Search);
        group.MapPost("/search/reindex", Reindex);
        return group;
    }

    private static async Task<IResult> Search(
        string? q,
        Guid? projectId,
        int? limit,
        string? mode,
        FtsSearchService ftsSearch,
        ISemanticSearch semanticSearch,
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
        var effectiveMode = mode ?? "fulltext";

        IReadOnlyList<EntrySearchHit> hits;
        if (effectiveMode == "fulltext")
        {
            hits = await ftsSearch.SearchAsync(projectId, q, effectiveLimit, ct);
        }
        else if (effectiveMode == "semantic")
        {
            if (!await semanticSearch.HealthAsync(ct))
            {
                return SemanticUnavailable();
            }

            try
            {
                hits = await semanticSearch.SearchAsync(projectId, q, effectiveLimit, ct);
            }
            catch (Exception)
            {
                return SemanticUnavailable();
            }
        }
        else
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["mode"] = ["Mode must be 'fulltext' or 'semantic'."]
            });
        }

        var responses = hits
            .Select(h => new SearchHitResponse(h.EntryId, h.Title, h.Type, h.Snippet, h.Rank))
            .ToArray();

        return Results.Ok(new SearchResponse(q, responses));
    }

    private static async Task<IResult> Reindex(ReindexService reindexService, CancellationToken ct)
    {
        var report = await reindexService.ReindexAllAsync(ct);
        return Results.Ok(report);
    }

    private static IResult SemanticUnavailable() =>
        Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Semantic search unavailable",
            detail: SemanticUnavailableDetail);
}
