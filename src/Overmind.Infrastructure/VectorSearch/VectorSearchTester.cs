using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Embeddings;

namespace Overmind.Infrastructure.VectorSearch;

public sealed class VectorSearchTester
{
    private readonly AppSettingsService _settings;
    private readonly EmbeddingProviderFactory _providerFactory;

    public VectorSearchTester(AppSettingsService settings, EmbeddingProviderFactory providerFactory)
    {
        _settings = settings;
        _providerFactory = providerFactory;
    }

    public async Task<VectorSearchHealthReport> TestAsync(VectorSearchCandidate candidate, CancellationToken ct)
    {
        var qdrantEndpoint = candidate.QdrantEndpoint?.Trim();
        if (string.IsNullOrEmpty(qdrantEndpoint))
        {
            qdrantEndpoint = await _settings.GetOrDefaultAsync(
                SettingKeys.QdrantEndpoint, SettingDefaults.QdrantEndpoint, ct);
        }

        var qdrant = await ProbeQdrantAsync(qdrantEndpoint, ct);

        var provider = await _providerFactory.CreateAsync(
            candidate.Provider, candidate.OllamaBaseUrl, candidate.OllamaModel, candidate.OpenAiBaseUrl, ct);
        var embedding = await SemanticSearchProbes.ProbeEmbeddingAsync(provider, ct);

        return new VectorSearchHealthReport(qdrant.Up && embedding.Up, qdrant, embedding);
    }

    private static async Task<QdrantHealthLeg> ProbeQdrantAsync(string endpoint, CancellationToken ct)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new QdrantHealthLeg(false, endpoint, "Must be a valid http(s) URL.");
        }

        var client = QdrantSemanticSearch.CreateClient(uri);
        return await SemanticSearchProbes.ProbeQdrantAsync(client, endpoint, ct);
    }
}
