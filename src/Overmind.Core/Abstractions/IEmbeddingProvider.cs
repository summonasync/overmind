namespace Overmind.Core;

public interface IEmbeddingProvider
{
    int Dimensions { get; }
    string Name { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}
