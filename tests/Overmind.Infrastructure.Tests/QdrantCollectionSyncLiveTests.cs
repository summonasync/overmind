using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.VectorSearch;
using Qdrant.Client;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Overmind.Infrastructure.Tests;

public sealed class QdrantCollectionSyncLiveTests
{
    private readonly ITestOutputHelper _output;

    public QdrantCollectionSyncLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task EnsureAsync_FreshCollection_SeedsFingerprint()
    {
        if (!await IsQdrantAvailable())
        {
            _output.WriteLine("Qdrant is not reachable; skipping live test.");
            return;
        }

        var (client, collection) = await NewScratchAsync();
        try
        {
            var sync = NewSync(collection);
            var fingerprint = EmbeddingFingerprint.From("ollama", "nomic-embed-text", 768);

            var report = await sync.EnsureAsync(client, fingerprint, CancellationToken.None);

            report.Recreated.ShouldBeFalse();
            (await sync.ReadAsync(client, CancellationToken.None)).ShouldBe(fingerprint);
        }
        finally
        {
            await client.DeleteCollectionAsync(collection);
        }
    }

    [Fact]
    public async Task EnsureAsync_MatchingFingerprint_PassesUntouched()
    {
        if (!await IsQdrantAvailable())
        {
            _output.WriteLine("Qdrant is not reachable; skipping live test.");
            return;
        }

        var (client, collection) = await NewScratchAsync();
        try
        {
            var sync = NewSync(collection);
            var fingerprint = EmbeddingFingerprint.From("ollama", "nomic-embed-text", 768);
            await sync.EnsureAsync(client, fingerprint, CancellationToken.None);

            var second = await sync.EnsureAsync(client, fingerprint, CancellationToken.None);

            second.Recreated.ShouldBeFalse();
            (await sync.ReadAsync(client, CancellationToken.None)).ShouldBe(fingerprint);
        }
        finally
        {
            await client.DeleteCollectionAsync(collection);
        }
    }

    [Fact]
    public async Task EnsureAsync_SameDimsDifferentFingerprint_Refuses()
    {
        if (!await IsQdrantAvailable())
        {
            _output.WriteLine("Qdrant is not reachable; skipping live test.");
            return;
        }

        var (client, collection) = await NewScratchAsync();
        try
        {
            var sync = NewSync(collection);
            var stored = EmbeddingFingerprint.From("ollama", "nomic-embed-text", 768);
            var refreshed = EmbeddingFingerprint.From("ollama", "all-minilm", 768);
            await sync.EnsureAsync(client, stored, CancellationToken.None);

            var failure = await Should.ThrowAsync<EmbeddingFingerprintMismatchException>(
                () => sync.EnsureAsync(client, refreshed, CancellationToken.None));

            failure.Expected.ShouldBe(refreshed.Display);
            failure.Found.ShouldBe(stored.Display);
            failure.Message.ShouldContain("nomic-embed-text");
            failure.Message.ShouldContain("all-minilm");
            failure.Message.ShouldContain("reindex");
        }
        finally
        {
            await client.DeleteCollectionAsync(collection);
        }
    }

    [Fact]
    public async Task EnsureAsync_DimsChange_RecreatesAndSeedsNewFingerprint()
    {
        if (!await IsQdrantAvailable())
        {
            _output.WriteLine("Qdrant is not reachable; skipping live test.");
            return;
        }

        var (client, collection) = await NewScratchAsync();
        try
        {
            var sync = NewSync(collection);
            var old = EmbeddingFingerprint.From("ollama", "nomic-embed-text", 768);
            var shrunk = EmbeddingFingerprint.From("onnx", "all-MiniLM-L6-v2", 384);
            await sync.EnsureAsync(client, old, CancellationToken.None);

            var report = await sync.EnsureAsync(client, shrunk, CancellationToken.None);

            report.Recreated.ShouldBeTrue();
            report.PreviousDimensions.ShouldBe((ulong)768);
            (await sync.ReadAsync(client, CancellationToken.None)).ShouldBe(shrunk);

            var settled = await sync.EnsureAsync(client, shrunk, CancellationToken.None);
            settled.Recreated.ShouldBeFalse();
        }
        finally
        {
            await client.DeleteCollectionAsync(collection);
        }
    }

    [Fact]
    public async Task EnsureAsync_EmptyCollectionWithoutMarker_SeedsFingerprint()
    {
        if (!await IsQdrantAvailable())
        {
            _output.WriteLine("Qdrant is not reachable; skipping live test.");
            return;
        }

        var collection = $"overmind_live_{Guid.NewGuid():N}";
        var client = NewClient();
        var fingerprint = EmbeddingFingerprint.From("onnx", "all-MiniLM-L6-v2", 384);
        await client.CreateCollectionAsync(collection, QdrantCollection.CreateVectorParams(384));

        try
        {
            var sync = NewSync(collection);

            await sync.EnsureAsync(client, fingerprint, CancellationToken.None);

            (await sync.ReadAsync(client, CancellationToken.None)).ShouldBe(fingerprint);
        }
        finally
        {
            await client.DeleteCollectionAsync(collection);
        }
    }

    [Fact]
    public async Task EnsureAsync_PointsWithoutMarker_RefusesToGuess()
    {
        if (!await IsQdrantAvailable())
        {
            _output.WriteLine("Qdrant is not reachable; skipping live test.");
            return;
        }

        var collection = $"overmind_live_{Guid.NewGuid():N}";
        var client = NewClient();
        await client.CreateCollectionAsync(collection, QdrantCollection.CreateVectorParams(384));
        var entry = ContextEntry.Create(Guid.NewGuid(), EntryType.Note, "legacy", "no marker anywhere");
        await client.UpsertAsync(
            collection, [EntryPayload.ToPoint(entry, Enumerable.Repeat(0.25f, 384).ToArray())]);

        try
        {
            var sync = NewSync(collection);

            var failure = await Should.ThrowAsync<EmbeddingFingerprintMismatchException>(
                () => sync.EnsureAsync(
                    client, EmbeddingFingerprint.From("onnx", "all-MiniLM-L6-v2", 384), CancellationToken.None));

            failure.Found.ShouldBeNull();
            failure.Message.ShouldContain("reindex");
        }
        finally
        {
            await client.DeleteCollectionAsync(collection);
        }
    }

    private static QdrantCollectionSync NewSync(string collection) =>
        new(collection, NullLogger.Instance);

    private static async Task<(QdrantClient Client, string Collection)> NewScratchAsync()
    {
        var collection = $"overmind_live_{Guid.NewGuid():N}";
        return (NewClient(), collection);
    }

    private static QdrantClient NewClient() => QdrantSemanticSearch.CreateClient(Endpoint);

    private static Uri Endpoint =>
        new(Environment.GetEnvironmentVariable("OVERMIND_QDRANT_ENDPOINT") ?? "http://127.0.0.1:6334");

    private static async Task<bool> IsQdrantAvailable()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await NewClient().HealthAsync(cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
