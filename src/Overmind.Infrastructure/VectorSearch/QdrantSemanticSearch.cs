using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Embeddings;
using Overmind.Infrastructure.Http;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

namespace Overmind.Infrastructure.VectorSearch;

public sealed class QdrantSemanticSearch : ISemanticSearch
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);

    private readonly AppSettingsService _settings;
    private readonly EmbeddingProviderFactory _providerFactory;
    private readonly VectorSearchHealthCache _healthCache;
    private readonly ILogger<QdrantSemanticSearch> _logger;

    private readonly object _gate = new();
    private QdrantClient? _client;
    private string? _clientEndpoint;

    public QdrantSemanticSearch(
        AppSettingsService settings,
        EmbeddingProviderFactory providerFactory,
        VectorSearchHealthCache healthCache,
        ILogger<QdrantSemanticSearch> logger)
    {
        _settings = settings;
        _providerFactory = providerFactory;
        _healthCache = healthCache;
        _logger = logger;
    }

    public async Task<bool> HealthAsync(CancellationToken ct)
    {
        try
        {
            var client = await ClientAsync(ct);
            using var cts = LinkedTimeout(ct);
            await client.HealthAsync(cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Qdrant health check failed: {Reason}", ex.Message);
            return false;
        }
    }

    public async Task<VectorSearchHealthReport> HealthReportAsync(CancellationToken ct)
    {
        var cached = _healthCache.Read();
        if (cached is not null)
        {
            return cached;
        }

        var endpoint = await _settings.GetOrDefaultAsync(
            SettingKeys.QdrantEndpoint, SettingDefaults.QdrantEndpoint, ct);

        QdrantHealthLeg qdrant;
        try
        {
            var client = await ClientAsync(ct);
            qdrant = await SemanticSearchProbes.ProbeQdrantAsync(client, endpoint, ct);
        }
        catch (Exception ex)
        {
            qdrant = new QdrantHealthLeg(false, endpoint, ex.Message);
        }

        if (!qdrant.Up)
        {
            _logger.LogWarning("Qdrant health check failed for {Endpoint}: {Reason}", endpoint, qdrant.Error);
        }

        var provider = await _providerFactory.ResolveAsync(ct);
        var embedding = await SemanticSearchProbes.ProbeEmbeddingAsync(provider, ct);
        if (!embedding.Up)
        {
            _logger.LogWarning(
                "Embedding health check failed for {Provider}/{Model}: {Reason}",
                embedding.Provider, embedding.Model, embedding.Error);
        }

        var report = new VectorSearchHealthReport(qdrant.Up && embedding.Up, qdrant, embedding);
        _healthCache.Store(report);
        return report;
    }

    public async Task<IReadOnlyList<EntrySearchHit>> SearchAsync(Guid? projectId, string query, int limit, CancellationToken ct)
    {
        var provider = await _providerFactory.ResolveAsync(ct);
        var vector = await EmbedAsync(provider, query, ct);

        var client = await ClientAsync(ct);
        var sync = await EnsureCollectionAsync(client, vector.Length, ct);
        if (sync.Recreated)
        {
            throw new VectorDimensionMismatchException((int?)sync.PreviousDimensions, vector.Length);
        }

        using var cts = LinkedTimeout(ct);
        if (projectId.HasValue)
        {
            var filter = Filter(MatchKeyword(EntryPayload.ProjectId, projectId.Value.ToString()));
            var filtered = await client.QueryAsync(
                QdrantCollection.Name,
                vector,
                filter: filter,
                limit: (ulong)Math.Clamp(limit, 1, 100),
                cancellationToken: cts.Token);
            return filtered.Select(EntryPayload.ToHit).ToArray();
        }

        var results = await client.QueryAsync(
            QdrantCollection.Name,
            vector,
            limit: (ulong)Math.Clamp(limit, 1, 100),
            cancellationToken: cts.Token);

        return results.Select(EntryPayload.ToHit).ToArray();
    }

    public async Task UpsertEntryAsync(ContextEntry entry, CancellationToken ct)
    {
        var provider = await _providerFactory.ResolveAsync(ct);
        var vector = await EmbedAsync(provider, $"{entry.Title}\n\n{entry.Content}", ct);

        var client = await ClientAsync(ct);
        await EnsureCollectionAsync(client, vector.Length, ct);

        var point = EntryPayload.ToPoint(entry, vector);

        using var cts = LinkedTimeout(ct);
        await client.UpsertAsync(QdrantCollection.Name, [point], cancellationToken: cts.Token);
    }

    public async Task DeleteEntryAsync(Guid entryId, CancellationToken ct)
    {
        var client = await ClientAsync(ct);
        using var cts = LinkedTimeout(ct);
        await client.DeleteAsync(QdrantCollection.Name, entryId, cancellationToken: cts.Token);
    }

    internal static QdrantClient CreateClient(Uri endpoint) =>
        LoopbackHttp.IsLoopback(endpoint)
            ? new QdrantClient(
                new QdrantGrpcClient(GrpcChannel.ForAddress(
                    endpoint,
                    new Grpc.Net.Client.GrpcChannelOptions
                    {
                        HttpHandler = LoopbackHttp.CreateProxyBypassHandler()
                    })),
                grpcTimeout: CallTimeout)
            : new QdrantClient(endpoint, grpcTimeout: CallTimeout);

    private static async Task<float[]> EmbedAsync(IEmbeddingProvider provider, string text, CancellationToken ct)
    {
        try
        {
            return await provider.EmbedAsync(text, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new EmbeddingFailedException(provider.Name, provider.Model, ex);
        }
    }

    private async Task<CollectionSync> EnsureCollectionAsync(QdrantClient client, int dimensions, CancellationToken ct)
    {
        using var cts = LinkedTimeout(ct);
        if (!await client.CollectionExistsAsync(QdrantCollection.Name, cts.Token))
        {
            await client.CreateCollectionAsync(
                QdrantCollection.Name,
                QdrantCollection.CreateVectorParams(dimensions),
                cancellationToken: cts.Token);
            return new CollectionSync(Recreated: false, PreviousDimensions: null);
        }

        using var infoCts = LinkedTimeout(ct);
        var info = await client.GetCollectionInfoAsync(QdrantCollection.Name, cancellationToken: infoCts.Token);
        if (QdrantCollection.HasMatchingDimensions(info.Config.Params.VectorsConfig, dimensions))
        {
            return new CollectionSync(Recreated: false, PreviousDimensions: null);
        }

        var actualSize = info.Config.Params.VectorsConfig.Params?.Size;
        _logger.LogWarning(
            "Recreating Qdrant collection {Collection} because vector dimensions changed from {OldDimensions} to {NewDimensions}; " +
            "run POST /api/search/reindex to repopulate it",
            QdrantCollection.Name,
            actualSize?.ToString() ?? "unknown",
            dimensions);

        using var deleteCts = LinkedTimeout(ct);
        await client.DeleteCollectionAsync(QdrantCollection.Name, cancellationToken: deleteCts.Token);

        using var createCts = LinkedTimeout(ct);
        await client.CreateCollectionAsync(
            QdrantCollection.Name,
            QdrantCollection.CreateVectorParams(dimensions),
            cancellationToken: createCts.Token);

        return new CollectionSync(Recreated: true, PreviousDimensions: actualSize);
    }

    private async Task<QdrantClient> ClientAsync(CancellationToken ct)
    {
        var endpoint = await _settings.GetOrDefaultAsync(
            SettingKeys.QdrantEndpoint, SettingDefaults.QdrantEndpoint, ct);

        lock (_gate)
        {
            if (_client is not null && string.Equals(_clientEndpoint, endpoint, StringComparison.Ordinal))
            {
                return _client;
            }
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"Invalid Qdrant endpoint '{endpoint}'.");
        }

        var client = CreateClient(uri);
        lock (_gate)
        {
            _client = client;
            _clientEndpoint = endpoint;
        }

        return client;
    }

    private static CancellationTokenSource LinkedTimeout(CancellationToken ct)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(CallTimeout);
        return cts;
    }

    private sealed record CollectionSync(bool Recreated, ulong? PreviousDimensions);
}
