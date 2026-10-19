using System.Net.Http;
using Overmind.Infrastructure.Http;

namespace Overmind.Infrastructure.Grimoire;

public sealed class GrimoireHealthClient : IGrimoireHealthClient
{
    public static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(3);

    private readonly HttpClient _httpClient;

    public GrimoireHealthClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient(LoopbackHttp.CreateProxyBypassHandler()) { Timeout = HealthTimeout };
    }

    public async Task<bool> IsHealthyAsync(int port, CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                new Uri($"http://127.0.0.1:{port}/health"), ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
