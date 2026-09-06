using Overmind.Core;
using Overmind.Infrastructure.Embeddings;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class OnnxEmbeddingProviderTests
{
    [Fact]
    public void Provider_ExposesNameAndMiniLmDimensions()
    {
        var provider = new Embeddings.OnnxEmbeddingProvider(new StubPipeline());

        provider.Name.ShouldBe(Embeddings.OnnxEmbeddingProvider.ProviderName);
        provider.Dimensions.ShouldBe(Embeddings.OnnxEmbeddingProvider.OutputDimensions);
        provider.Dimensions.ShouldBe(384);
    }

    [Fact]
    public async Task EmbedAsync_DelegatesToPipeline()
    {
        var pipeline = new StubPipeline();
        var provider = new Embeddings.OnnxEmbeddingProvider(pipeline);

        var vector = await provider.EmbedAsync("any text", CancellationToken.None);

        vector.ShouldBe([1f, 2f, 3f]);
        pipeline.LastText.ShouldBe("any text");
    }

    private sealed class StubPipeline : IOnnxEmbeddingPipeline
    {
        public int Dimensions => 3;

        public string? LastText { get; private set; }

        public float[] Embed(string text)
        {
            LastText = text;
            return [1f, 2f, 3f];
        }

        public void Dispose()
        {
        }
    }
}
