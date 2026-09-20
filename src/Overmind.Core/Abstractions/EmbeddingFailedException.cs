namespace Overmind.Core;

public sealed class EmbeddingFailedException : Exception
{
    public string ProviderName { get; }
    public string Model { get; }

    public EmbeddingFailedException(string providerName, string model, Exception innerException)
        : base(
            $"Embedding provider '{providerName}' failed for model '{model}': {innerException.Message}",
            innerException)
    {
        ProviderName = providerName;
        Model = model;
    }
}
