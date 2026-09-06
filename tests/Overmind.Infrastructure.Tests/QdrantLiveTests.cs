using Overmind.Core;
using Overmind.Infrastructure.Embeddings;
using Overmind.Infrastructure.VectorSearch;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Overmind.Infrastructure.Tests;

public sealed class QdrantLiveTests
{
    private readonly ITestOutputHelper _output;

    public QdrantLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CollectionConfigAndPayload_RoundTripAgainstLiveQdrant()
    {
        if (!await IsQdrantAvailable())
        {
            _output.WriteLine("Qdrant is not reachable; skipping live test.");
            return;
        }

        var client = new QdrantClient(Endpoint, grpcTimeout: TimeSpan.FromSeconds(10));
        var collection = $"overmind_live_{Guid.NewGuid():N}";
        var dimensions = OnnxEmbeddingProvider.OutputDimensions;

        await client.CreateCollectionAsync(collection, QdrantCollection.CreateVectorParams(dimensions));

        try
        {
            var entry = ContextEntry.Create(Guid.NewGuid(), EntryType.Note, "live roundtrip", "verified against a real qdrant");
            var vector = Enumerable.Repeat(0.25f, dimensions).ToArray();
            await client.UpsertAsync(collection, [EntryPayload.ToPoint(entry, vector)]);

            var filter = Conditions.Filter(
                Conditions.MatchKeyword(EntryPayload.ProjectId, entry.ProjectId.ToString()));
            var results = await client.QueryAsync(collection, vector, filter: filter, limit: 5);

            results.Count.ShouldBe(1);
            var hit = EntryPayload.ToHit(results[0]);
            hit.EntryId.ShouldBe(entry.Id);
            hit.Title.ShouldBe("live roundtrip");
            hit.Type.ShouldBe(EntryType.Note);
        }
        finally
        {
            await client.DeleteCollectionAsync(collection);
        }
    }

    private static Uri Endpoint =>
        new(Environment.GetEnvironmentVariable("OVERMIND_QDRANT_ENDPOINT") ?? "http://127.0.0.1:6334");

    private static async Task<bool> IsQdrantAvailable()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await new QdrantClient(Endpoint, grpcTimeout: TimeSpan.FromSeconds(2)).HealthAsync(cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
