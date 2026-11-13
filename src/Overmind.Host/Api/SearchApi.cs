using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Search;
using Overmind.Infrastructure.VectorSearch;

namespace Overmind.Host.Api;

public static class SearchApi
{
    private const string VectorStoreDownDetail =
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
        DateTime? since,
        DateTime? until,
        string? author,
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

        IReadOnlyList<SearchHitResponse> responses;
        if (effectiveMode == "fulltext")
        {
            var hits = await ftsSearch.SearchUnionAsync(projectId, q, effectiveLimit, ct);
            responses = hits.Select(SearchHitMapping.ToResponse).ToArray();
        }
        else if (effectiveMode == "semantic")
        {
            if (!await semanticSearch.HealthAsync(ct))
            {
                return SemanticUnavailable(VectorStoreDownDetail);
            }

            try
            {
                var hits = await semanticSearch.SearchAsync(projectId, q, effectiveLimit, ct);
                responses = hits
                    .Select(h => new EntryHitResponse(h.EntryId, h.Title, h.Type, h.Snippet, h.Rank))
                    .ToArray();
            }
            catch (EmbeddingFailedException ex)
            {
                return SemanticUnavailable(
                    $"Semantic search is unavailable because {ex.Message} " +
                    "Check the embedding settings or use mode=fulltext.");
            }
            catch (VectorDimensionMismatchException ex)
            {
                return SemanticUnavailable(
                    $"Semantic search is unavailable because the vector collection was recreated: {ex.Message} " +
                    "Run POST /api/search/reindex to repopulate it, or use mode=fulltext.");
            }
            catch (EmbeddingFingerprintMismatchException ex)
            {
                return HttpErrors.Conflict(
                    $"{ex.Message} Run POST /api/search/reindex to rebuild the collection with the current model, " +
                    "or use mode=fulltext.",
                    title: "EmbeddingFingerprintMismatch");
            }
            catch (Exception)
            {
                return SemanticUnavailable(VectorStoreDownDetail);
            }
        }
        else
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["mode"] = ["Mode must be 'fulltext' or 'semantic'."]
            });
        }

        var filtered = await ApplyEntryFiltersAsync(db, responses, since, until, author, ct);

        return Results.Ok(new SearchResponse(q, filtered));
    }

    private static async Task<IReadOnlyList<SearchHitResponse>> ApplyEntryFiltersAsync(
        OvermindDbContext db,
        IReadOnlyList<SearchHitResponse> hits,
        DateTime? since,
        DateTime? until,
        string? author,
        CancellationToken ct)
    {
        if (!since.HasValue && !until.HasValue && string.IsNullOrWhiteSpace(author))
        {
            return hits;
        }

        var entryIds = hits.OfType<EntryHitResponse>().Select(h => h.EntryId).ToArray();
        var matching = await EntryQueryFilters
            .Apply(db.ContextEntries.Where(e => entryIds.Contains(e.Id)), since, until, author)
            .Select(e => e.Id)
            .ToListAsync(ct);

        var matchingIds = matching.ToHashSet();
        return hits
            .Where(hit => hit switch
            {
                EntryHitResponse entry => matchingIds.Contains(entry.EntryId),
                _ => false
            })
            .ToArray();
    }

    private static async Task<IResult> Reindex(ReindexService reindexService, CancellationToken ct)
    {
        var report = await reindexService.RebuildAllAsync(ct);
        return Results.Ok(report);
    }

    private static IResult SemanticUnavailable(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Semantic search unavailable",
            detail: detail);
}
