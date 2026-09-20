using System.Net.Http.Headers;
using System.Net.Http.Json;
using Overmind.Core;

namespace Overmind.Infrastructure.Embeddings;

public sealed class OpenAiCompatibleEmbeddingProvider : IEmbeddingProvider
{
    public const string ProviderName = "openai";
    public const string DefaultModel = "text-embedding-3-small";
    public const int DefaultDimensions = 1536;

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiCompatibleEmbeddingProvider(HttpClient http, string baseUrl, string apiKey, string model = DefaultModel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentNullException.ThrowIfNull(apiKey);

        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _model = model;
    }

    public string Name => ProviderName;

    public string Model => _model;

    public int Dimensions => DefaultDimensions;

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/embeddings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = JsonContent.Create(new { model = _model, input = text });

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OpenAiEmbedResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("The embedding endpoint returned an empty response.");
        if (payload.Data.Length == 0)
        {
            throw new InvalidOperationException("The embedding endpoint returned no data.");
        }

        return payload.Data[0].Embedding;
    }

    private sealed record OpenAiEmbedResponse(OpenAiEmbedData[] Data);

    private sealed record OpenAiEmbedData(float[] Embedding);
}
