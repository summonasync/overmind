using Microsoft.Extensions.Logging;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Embeddings;

namespace Overmind.Infrastructure.VectorSearch;

public sealed class QdrantRouteVectorIndex(
    QdrantSemanticSearch search,
    EmbeddingProviderFactory providers,
    VectorSearchHealthCache health,
    ILogger<QdrantRouteVectorIndex> logger) : IRouteVectorIndex
{
    private const string FingerprintProbeText = "overmind route index fingerprint probe";

    private readonly QdrantCollectionSync _collectionSync = new(RouteVectorCollection.Name, logger);

    public async Task<RouteVectorMatch?> MatchTopAsync(string text, CancellationToken ct = default)
    {
        if (health.Read() is { Up: false })
        {
            return null;
        }

        try
        {
            var provider = await providers.ResolveAsync(ct);
            var vector = await provider.EmbedAsync(text, ct);

            var client = await search.ClientAsync(ct);
            await _collectionSync.EnsureAsync(
                client, EmbeddingFingerprint.From(provider.Name, provider.Model, vector.Length), ct);

            using var cts = LinkedTimeout(ct);
            var results = await client.QueryAsync(
                RouteVectorCollection.Name,
                vector,
                filter: RouteVectorCollection.AllRoutesFilter(),
                limit: RouteVectorCollection.MatchTopK,
                cancellationToken: cts.Token);

            foreach (var point in results)
            {
                if (RouteVectorCollection.ToMatch(point) is { } match)
                {
                    return match;
                }
            }

            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Route vector match fell back to keyword-only matching: {Reason}", ex.Message);
            return null;
        }
    }

    public async Task UpsertAsync(
        Guid routeId, string patternText, RouteVectorPayload payload, CancellationToken ct = default)
    {
        var provider = await providers.ResolveAsync(ct);
        var vector = await provider.EmbedAsync(patternText, ct);

        var client = await search.ClientAsync(ct);
        await _collectionSync.EnsureAsync(
            client, EmbeddingFingerprint.From(provider.Name, provider.Model, vector.Length), ct);

        var point = RouteVectorCollection.ToPoint(routeId, patternText, payload, vector);
        using var cts = LinkedTimeout(ct);
        await client.UpsertAsync(RouteVectorCollection.Name, [point], cancellationToken: cts.Token);
    }

    public async Task DeleteAsync(Guid routeId, CancellationToken ct = default)
    {
        var client = await search.ClientAsync(ct);
        using var cts = LinkedTimeout(ct);
        await client.DeleteAsync(RouteVectorCollection.Name, routeId, cancellationToken: cts.Token);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        var provider = await providers.ResolveAsync(ct);
        var probe = await provider.EmbedAsync(FingerprintProbeText, ct);

        var client = await search.ClientAsync(ct);
        await _collectionSync.RebuildAsync(
            client, EmbeddingFingerprint.From(provider.Name, provider.Model, probe.Length), ct);
    }

    private static CancellationTokenSource LinkedTimeout(CancellationToken ct)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        return cts;
    }
}
