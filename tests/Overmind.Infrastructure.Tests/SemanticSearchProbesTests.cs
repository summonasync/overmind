using Overmind.Core;
using Overmind.Infrastructure.VectorSearch;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class SemanticSearchProbesTests
{
    [Fact]
    public async Task ProbeEmbeddingAsync_HealthyProvider_ReportsProbedDimensionsNotCached()
    {
        var provider = new StubEmbeddingProvider(
            "ollama", "nomic-embed-text", cachedDimensions: 384, vectorLength: 768);

        var leg = await SemanticSearchProbes.ProbeEmbeddingAsync(provider, CancellationToken.None);

        leg.Up.ShouldBeTrue();
        leg.Provider.ShouldBe("ollama");
        leg.Model.ShouldBe("nomic-embed-text");
        leg.Dimensions.ShouldBe(768);
        leg.Error.ShouldBeNull();
    }

    [Fact]
    public async Task ProbeEmbeddingAsync_FailingProvider_ReportsErrorAndUnknownDimensions()
    {
        var provider = new StubEmbeddingProvider(
            "ollama", "missing-model", cachedDimensions: 384, vectorLength: 768,
            () => throw new InvalidOperationException("model 'missing-model' not found"));

        var leg = await SemanticSearchProbes.ProbeEmbeddingAsync(provider, CancellationToken.None);

        leg.Up.ShouldBeFalse();
        leg.Provider.ShouldBe("ollama");
        leg.Model.ShouldBe("missing-model");
        leg.Dimensions.ShouldBeNull();
        leg.Error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProbeEmbeddingAsync_CallerCancelled_Throws()
    {
        var provider = new StubEmbeddingProvider("ollama", "m", 384, 768);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => SemanticSearchProbes.ProbeEmbeddingAsync(provider, cts.Token));
    }

    [Fact]
    public async Task ProbeQdrantAsync_DeadEndpoint_ReportsDownWithEndpointAndError()
    {
        var client = QdrantSemanticSearch.CreateClient(new Uri("http://127.0.0.1:1"));

        var leg = await SemanticSearchProbes.ProbeQdrantAsync(client, "http://127.0.0.1:1", CancellationToken.None);

        leg.Up.ShouldBeFalse();
        leg.Endpoint.ShouldBe("http://127.0.0.1:1");
        leg.Error.ShouldNotBeNullOrWhiteSpace();
    }

    private sealed class StubEmbeddingProvider(
        string name,
        string model,
        int cachedDimensions,
        int vectorLength,
        Action? embed = null) : IEmbeddingProvider
    {
        public string Name => name;

        public string Model => model;

        public int Dimensions => cachedDimensions;

        public Task<float[]> EmbedAsync(string text, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            embed?.Invoke();
            return Task.FromResult(Enumerable.Repeat(0.1f, vectorLength).ToArray());
        }
    }
}
