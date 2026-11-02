using Microsoft.Extensions.AI;

namespace Overmind.Infrastructure.Llm;

public sealed class LlmChatClientCache : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CacheEntry? _current;

    public async Task<IChatClient> GetOrCreateAsync(string baseUrl, string apiKey, string model, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_current is { } entry && entry.Matches(baseUrl, apiKey, model))
            {
                return entry.Client;
            }

            var http = new HttpClient(LlmChatClientFactory.CreateChatHandler(baseUrl))
            {
                Timeout = LlmChatClientFactory.ChatTimeout
            };
            var previous = _current;
            _current = new CacheEntry(baseUrl, apiKey, model, LlmChatClientFactory.CreateChatClient(http, baseUrl, apiKey, model), http);
            previous?.Dispose();
            return _current.Client;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _current?.Dispose();
        _gate.Dispose();
    }

    private sealed record CacheEntry(string BaseUrl, string ApiKey, string Model, IChatClient Client, HttpClient Http)
    {
        public bool Matches(string baseUrl, string apiKey, string model) =>
            BaseUrl == baseUrl && ApiKey == apiKey && Model == model;

        public void Dispose() => Http.Dispose();
    }
}
