using System.Net.Http.Json;
using Overmind.Core;

namespace Overmind.Infrastructure.Embeddings;

public sealed class OllamaEmbeddingProvider : IEmbeddingProvider
{
    public const string ProviderName = "ollama";
    public const string DefaultModel = "all-minilm-l6-v2";
    public const int DefaultDimensions = 384;

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaEmbeddingProvider(HttpClient http, string baseUrl, string model = DefaultModel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
    }

    public string Name => ProviderName;

    public int Dimensions => DefaultDimensions;

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync(
            $"{_baseUrl}/api/embed",
            new { model = _model, input = text },
            ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Ollama returned an empty embeddings response.");
        if (payload.Embeddings.Length == 0)
        {
            throw new InvalidOperationException("Ollama returned no embedding vectors.");
        }

        return payload.Embeddings[0];
    }

    private sealed record OllamaEmbedResponse(float[][] Embeddings);
}
