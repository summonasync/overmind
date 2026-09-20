using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Embeddings;
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
            NullLogger<QdrantSemanticSearch>.Instance);

        var failure = await Should.ThrowAsync<EmbeddingFailedException>(
            () => search.SearchAsync(null, "anything", 5, CancellationToken.None));

        failure.ProviderName.ShouldBe("ollama");
        failure.Model.ShouldBe("nomic-embed-text");
        failure.Message.ShouldContain("nomic-embed-text");
    }

    public void Dispose() => _temp.Dispose();
}
