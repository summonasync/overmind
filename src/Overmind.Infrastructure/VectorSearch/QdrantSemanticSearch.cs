using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Embeddings;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

namespace Overmind.Infrastructure.VectorSearch;

public sealed class QdrantSemanticSearch : ISemanticSearch
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);

    private readonly AppSettingsService _settings;
    private readonly EmbeddingProviderFactory _providerFactory;
    private readonly ILogger<QdrantSemanticSearch> _logger;

    private readonly object _gate = new();
    private QdrantClient? _client;
    private string? _clientEndpoint;

    public QdrantSemanticSearch(
        AppSettingsService settings,
        EmbeddingProviderFactory providerFactory,
        ILogger<QdrantSemanticSearch> logger)
    {
        _settings = settings;
        _providerFactory = providerFactory;
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
            _logger.LogDebug(ex, "Qdrant health check failed; semantic search is down");
            return false;
        }
    }

    public async Task<IReadOnlyList<EntrySearchHit>> SearchAsync(Guid? projectId, string query, int limit, CancellationToken ct)
    {
        var provider = await _providerFactory.ResolveAsync(ct);
        var client = await ClientAsync(ct);
        await EnsureCollectionAsync(client, provider.Dimensions, ct);

        var vector = await provider.EmbedAsync(query, ct);

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
        var client = await ClientAsync(ct);
        await EnsureCollectionAsync(client, provider.Dimensions, ct);

        var vector = await provider.EmbedAsync($"{entry.Title}\n\n{entry.Content}", ct);
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

    private async Task EnsureCollectionAsync(QdrantClient client, int dimensions, CancellationToken ct)
    {
        using var cts = LinkedTimeout(ct);
        if (!await client.CollectionExistsAsync(QdrantCollection.Name, cts.Token))
        {
            await client.CreateCollectionAsync(
                QdrantCollection.Name,
                QdrantCollection.CreateVectorParams(dimensions),
                cancellationToken: cts.Token);
            return;
        }

        using var infoCts = LinkedTimeout(ct);
        var info = await client.GetCollectionInfoAsync(QdrantCollection.Name, cancellationToken: infoCts.Token);
        if (QdrantCollection.HasMatchingDimensions(info.Config.Params.VectorsConfig, dimensions))
        {
            return;
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

        var client = new QdrantClient(uri, grpcTimeout: CallTimeout);
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
}
