using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Overmind.Mcp;

public sealed class ApiNotFoundException : Exception
{
    public ApiNotFoundException(string message)
        : base(message)
    {
    }
}

public sealed class SemanticSearchUnavailableException : Exception
{
    public SemanticSearchUnavailableException(string message)
        : base(message)
    {
    }
}

public sealed class ApiUnreachableException : Exception
{
    public ApiUnreachableException(string message)
        : base(message)
    {
    }
}

public sealed class ApiStatusException : Exception
{
    public ApiStatusException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

public sealed class OvermindApiClient
{
    public const string RetryableMarker = "retryable transport failure:";

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private const string OperationIdHeader = "X-Operation-Id";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public OvermindApiClient(HttpClient http)
    {
        _http = http;
    }

    // Loopback is the security boundary: a system proxy would carry Host traffic off-machine.
    // The session stamp lives on the shared pipeline so every client built from here (adapter,
    // project resolver) identifies as the same agent session, never as a headerless human.
    // The touch sits outside the stamp so the session reports liveness on every round-trip.
    public static HttpClient CreateHttpClient(string baseUrl) =>
        new(new SessionTouchHandler(new AdapterSessionHandler(new SocketsHttpHandler { UseProxy = false })))
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = Timeout
        };

    public static async Task CheckHealthAsync(HttpClient http, CancellationToken ct)
    {
        var response = await http.GetAsync("/api/health", ct);
        response.EnsureSuccessStatusCode();
    }

    public Task<JsonElement> GetAsync(string pathAndQuery, CancellationToken ct) =>
        SendAsync(() => _http.GetAsync(pathAndQuery, HttpCompletionOption.ResponseHeadersRead, ct), "GET " + pathAndQuery, ct);

    public Task<JsonElement> PostAsync(string path, object body, CancellationToken ct, Guid? operationId = null) =>
        SendAsync(() => SendJsonAsync(HttpMethod.Post, path, body, operationId, ct), "POST " + path, ct);

    public Task<JsonElement> DeleteAsync(string path, CancellationToken ct) =>
        SendAsync(() => _http.DeleteAsync(path, ct), "DELETE " + path, ct);

    public Task<JsonElement> DeleteAsync(string path, object body, CancellationToken ct) =>
        SendAsync(() => SendJsonAsync(HttpMethod.Delete, path, body, operationId: null, ct), "DELETE " + path, ct);

    public Task<JsonElement> PutAsync(string path, object body, CancellationToken ct, Guid? operationId = null) =>
        SendAsync(() => SendJsonAsync(HttpMethod.Put, path, body, operationId, ct), "PUT " + path, ct);

    private async Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method, string path, object body, Guid? operationId, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };

        if (operationId is not null)
        {
            request.Headers.Add(OperationIdHeader, operationId.Value.ToString("D"));
        }

        return await _http.SendAsync(request, ct);
    }

    private static async Task<JsonElement> SendAsync(
        Func<Task<HttpResponseMessage>> send,
        string operation,
        CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new ApiUnreachableException($"{RetryableMarker} overmind API is unreachable ({operation}): {ex.Message}");
        }

        try
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                var detail = await ReadProblemDetailAsync(response, ct);
                throw new ApiNotFoundException(detail ?? $"overmind API resource not found ({operation})");
            }

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                var detail = await ReadProblemDetailAsync(response, ct);
                throw new SemanticSearchUnavailableException(
                    $"vector search unavailable: {detail ?? "semantic search is down; use mode=fulltext"}");
            }

            if (!response.IsSuccessStatusCode)
            {
                var detail = await ReadProblemDetailAsync(response, ct);
                throw new ApiStatusException(
                    (int)response.StatusCode,
                    $"overmind API returned {(int)response.StatusCode} ({operation}): {detail ?? response.ReasonPhrase}");
            }

            var payload = await response.Content.ReadAsStringAsync(ct);
            if (payload.Length == 0)
            {
                return default;
            }

            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        finally
        {
            response.Dispose();
        }
    }

    private static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var payload = await response.Content.ReadAsStringAsync(ct);
            if (payload.Length == 0)
            {
                return null;
            }

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var detail = root.TryGetProperty("detail", out var d) ? d.GetString() : null;
            var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            return detail ?? title;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
