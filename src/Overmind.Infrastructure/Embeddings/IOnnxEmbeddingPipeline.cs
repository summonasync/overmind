namespace Overmind.Infrastructure.Embeddings;

public interface IOnnxEmbeddingPipeline : IDisposable
{
    int Dimensions { get; }

    float[] Embed(string text);
}
