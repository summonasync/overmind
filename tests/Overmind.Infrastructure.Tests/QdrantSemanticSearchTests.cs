using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Embeddings;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.VectorSearch;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class QdrantSemanticSearchTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    [Fact]
    public void CreateClient_LoopbackEndpoint_ConstructsClient()
    {
        var client = QdrantSemanticSearch.CreateClient(new Uri("http://127.0.0.1:6334"));

        client.ShouldNotBeNull();
    }

    [Fact]
    public void CreateClient_RemoteEndpoint_ConstructsClient()
    {
        var client = QdrantSemanticSearch.CreateClient(new Uri("https://qdrant.example.com:6334"));

        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task SearchAsync_DeadEmbeddingEndpoint_ThrowsTypedFailure()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.EmbeddingProvider, "ollama", CancellationToken.None);
        await settings.SetAsync(SettingKeys.EmbeddingOllamaBaseUrl, "http://127.0.0.1:1", CancellationToken.None);
        await settings.SetAsync(SettingKeys.EmbeddingOllamaModel, "nomic-embed-text", CancellationToken.None);

        var search = new QdrantSemanticSearch(
            settings,
            new EmbeddingProviderFactory(context, NullLoggerFactory.Instance),
            new VectorSearchHealthCache(),
            NullLogger<QdrantSemanticSearch>.Instance);

        var failure = await Should.ThrowAsync<EmbeddingFailedException>(
            () => search.SearchAsync(null, "anything", 5, CancellationToken.None));

        failure.ProviderName.ShouldBe("ollama");
        failure.Model.ShouldBe("nomic-embed-text");
        failure.Message.ShouldContain("nomic-embed-text");
    }

    [Fact]
    public async Task HealthReportAsync_DeadEndpoints_ReportsBothLegsDown()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.EmbeddingProvider, "ollama", CancellationToken.None);
        await settings.SetAsync(SettingKeys.EmbeddingOllamaBaseUrl, "http://127.0.0.1:1", CancellationToken.None);
        await settings.SetAsync(SettingKeys.EmbeddingOllamaModel, "nomic-embed-text", CancellationToken.None);
        await settings.SetAsync(SettingKeys.QdrantEndpoint, "http://127.0.0.1:1", CancellationToken.None);

        var search = new QdrantSemanticSearch(
            settings,
            new EmbeddingProviderFactory(context, NullLoggerFactory.Instance),
            new VectorSearchHealthCache(),
            NullLogger<QdrantSemanticSearch>.Instance);

        var report = await search.HealthReportAsync(CancellationToken.None);

        report.Up.ShouldBeFalse();
        report.Qdrant.Up.ShouldBeFalse();
        report.Qdrant.Endpoint.ShouldBe("http://127.0.0.1:1");
        report.Qdrant.Error.ShouldNotBeNullOrWhiteSpace();
        report.Embedding.Up.ShouldBeFalse();
        report.Embedding.Provider.ShouldBe("ollama");
        report.Embedding.Model.ShouldBe("nomic-embed-text");
        report.Embedding.Dimensions.ShouldBeNull();
        report.Embedding.Error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task HealthReportAsync_SecondReadWithinTtl_ServesCachedReportToFreshScopedInstance()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.EmbeddingProvider, "ollama", CancellationToken.None);
        await settings.SetAsync(SettingKeys.EmbeddingOllamaBaseUrl, "http://127.0.0.1:1", CancellationToken.None);
        await settings.SetAsync(SettingKeys.EmbeddingOllamaModel, "nomic-embed-text", CancellationToken.None);
        await settings.SetAsync(SettingKeys.QdrantEndpoint, "http://127.0.0.1:1", CancellationToken.None);

        var cache = new VectorSearchHealthCache();
        var firstSearch = NewSearch(context, cache);
        var secondSearch = NewSearch(context, cache);

        var first = await firstSearch.HealthReportAsync(CancellationToken.None);
        var second = await secondSearch.HealthReportAsync(CancellationToken.None);

        first.Up.ShouldBeFalse();
        second.ShouldBeSameAs(first);
    }

    [Fact]
    public async Task HealthReportAsync_AfterTtlExpiry_ProbesBothLegsAgain()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.EmbeddingProvider, "ollama", CancellationToken.None);
        await settings.SetAsync(SettingKeys.EmbeddingOllamaBaseUrl, "http://127.0.0.1:1", CancellationToken.None);
        await settings.SetAsync(SettingKeys.EmbeddingOllamaModel, "nomic-embed-text", CancellationToken.None);
        await settings.SetAsync(SettingKeys.QdrantEndpoint, "http://127.0.0.1:1", CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        var cache = new VectorSearchHealthCache { UtcNow = () => now };
        var search = NewSearch(context, cache);

        var first = await search.HealthReportAsync(CancellationToken.None);
        now += cache.Ttl + TimeSpan.FromSeconds(1);
        var second = await search.HealthReportAsync(CancellationToken.None);

        second.ShouldNotBeSameAs(first);
        second.Up.ShouldBeFalse();
        second.Qdrant.Endpoint.ShouldBe("http://127.0.0.1:1");
        second.Embedding.Error.ShouldNotBeNullOrWhiteSpace();
    }

    private static QdrantSemanticSearch NewSearch(
        OvermindDbContext context, VectorSearchHealthCache cache) =>
        new(
            new AppSettingsService(context),
            new EmbeddingProviderFactory(context, NullLoggerFactory.Instance),
            cache,
            NullLogger<QdrantSemanticSearch>.Instance);

    public void Dispose() => _temp.Dispose();
}
