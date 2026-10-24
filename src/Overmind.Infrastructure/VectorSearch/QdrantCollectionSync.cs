using Microsoft.Extensions.Logging;
using Overmind.Core;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

namespace Overmind.Infrastructure.VectorSearch;

public sealed class QdrantCollectionSync(string collection, ILogger logger)
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);

    public async Task<CollectionSyncReport> EnsureAsync(
        QdrantClient client, EmbeddingFingerprint fingerprint, CancellationToken ct)
    {
        using var existsCts = LinkedTimeout(ct);
        if (!await client.CollectionExistsAsync(collection, existsCts.Token))
        {
            await CreateAsync(client, fingerprint, ct);
            return new CollectionSyncReport(Recreated: false, PreviousDimensions: null);
        }

        using var infoCts = LinkedTimeout(ct);
        var info = await client.GetCollectionInfoAsync(collection, cancellationToken: infoCts.Token);
        if (!QdrantCollection.HasMatchingDimensions(info.Config.Params.VectorsConfig, fingerprint.Dimensions))
        {
            return await RecreateAsync(client, info, fingerprint, ct);
        }

        await VerifyAsync(client, fingerprint, info.HasPointsCount && info.PointsCount > 0, ct);
        return new CollectionSyncReport(Recreated: false, PreviousDimensions: null);
    }

    public async Task<EmbeddingFingerprint?> ReadAsync(QdrantClient client, CancellationToken ct)
    {
        using var cts = LinkedTimeout(ct);
        return await ReadMarkerAsync(client, cts.Token);
    }

    public async Task<CollectionRebuildReport> RebuildAsync(
        QdrantClient client, EmbeddingFingerprint fingerprint, CancellationToken ct)
    {
        using var existsCts = LinkedTimeout(ct);
        var existed = await client.CollectionExistsAsync(collection, existsCts.Token);

        EmbeddingFingerprint? previous = null;
        ulong dropped = 0;
        if (existed)
        {
            using var readCts = LinkedTimeout(ct);
            previous = await ReadMarkerAsync(client, readCts.Token);
            dropped = await CountEntryPointsAsync(client, ct);

            using var deleteCts = LinkedTimeout(ct);
            await client.DeleteCollectionAsync(collection, cancellationToken: deleteCts.Token);
        }

        await CreateAsync(client, fingerprint, ct);
        logger.LogWarning(
            "Rebuilt Qdrant collection {Collection}: discarded {DroppedPoints} entry points, fingerprint {OldFingerprint} -> {NewFingerprint}",
            collection,
            dropped,
            previous?.Display ?? "none",
            fingerprint.Display);

        return new CollectionRebuildReport(previous, fingerprint, dropped, existed);
    }

    private async Task<CollectionSyncReport> RecreateAsync(
        QdrantClient client, CollectionInfo info, EmbeddingFingerprint fingerprint, CancellationToken ct)
    {
        var actualSize = info.Config.Params.VectorsConfig.Params?.Size;
        logger.LogWarning(
            "Recreating Qdrant collection {Collection} because vector dimensions changed from {OldDimensions} to {NewDimensions}; " +
            "run POST /api/search/reindex to repopulate it",
            collection,
            actualSize?.ToString() ?? "unknown",
            fingerprint.Dimensions);

        using var deleteCts = LinkedTimeout(ct);
        await client.DeleteCollectionAsync(collection, cancellationToken: deleteCts.Token);

        await CreateAsync(client, fingerprint, ct);
        return new CollectionSyncReport(Recreated: true, PreviousDimensions: actualSize);
    }

    private async Task CreateAsync(QdrantClient client, EmbeddingFingerprint fingerprint, CancellationToken ct)
    {
        using var cts = LinkedTimeout(ct);
        await client.CreateCollectionAsync(
            collection,
            QdrantCollection.CreateVectorParams(fingerprint.Dimensions),
            cancellationToken: cts.Token);
        await SeedAsync(client, fingerprint, cts.Token);
    }

    private async Task VerifyAsync(
        QdrantClient client, EmbeddingFingerprint expected, bool hasStoredPoints, CancellationToken ct)
    {
        var stored = await ReadMarkerAsync(client, ct);
        if (stored is null)
        {
            if (hasStoredPoints)
            {
                throw new EmbeddingFingerprintMismatchException(expected.Display, null);
            }

            await SeedAsync(client, expected, ct);
            return;
        }

        if (stored.Value != expected.Value)
        {
            throw new EmbeddingFingerprintMismatchException(expected.Display, stored.Display);
        }
    }

    private async Task<ulong> CountEntryPointsAsync(QdrantClient client, CancellationToken ct)
    {
        var filter = new Filter();
        filter.MustNot.Add(HasId(EmbeddingFingerprint.MarkerPointId));

        using var cts = LinkedTimeout(ct);
        return await client.CountAsync(collection, filter, exact: true, cancellationToken: cts.Token);
    }

    private async Task<EmbeddingFingerprint?> ReadMarkerAsync(QdrantClient client, CancellationToken ct)
    {
        var points = await client.RetrieveAsync(
            collection,
            [EmbeddingFingerprint.MarkerPointId],
            withPayload: true,
            withVectors: false,
            cancellationToken: ct);

        return points.Count == 0 ? null : EmbeddingFingerprint.FromPayload(points[0].Payload);
    }

    private async Task SeedAsync(QdrantClient client, EmbeddingFingerprint fingerprint, CancellationToken ct)
    {
        var marker = new PointStruct
        {
            Id = EmbeddingFingerprint.MarkerPointId,
            Vectors = new float[fingerprint.Dimensions],
        };

        foreach (var pair in fingerprint.ToPayload())
        {
            marker.Payload.Add(pair.Key, pair.Value);
        }

        await client.UpsertAsync(collection, [marker], cancellationToken: ct);
    }

    private static CancellationTokenSource LinkedTimeout(CancellationToken ct)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(CallTimeout);
        return cts;
    }
}
