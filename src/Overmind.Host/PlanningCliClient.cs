using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using Overmind.Host.Api;

namespace Overmind.Host;

internal sealed class PlanningCliUnreachableException(string message)
    : Exception(message);

internal sealed class PlanningCliApiException(int statusCode, string message)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

internal sealed record PlanningCliResponse(int StatusCode, string Body);

// Loopback is the security boundary: a system proxy would carry host traffic off-machine,
// same as the MCP adapter. Session identity rides on every request once resolved.
internal sealed class PlanningCliClient(HttpClient http, string baseUrl)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public Guid? SessionId { get; set; }

    public string BaseUrl { get; } = baseUrl;

    public static HttpClient CreateHttpClient(string baseUrl) =>
        new(new SocketsHttpHandler { UseProxy = false })
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        };

    public Task<PlanningCliResponse> GetAsync(
        string path, IReadOnlyDictionary<string, string?>? query, CancellationToken ct) =>
        SendAsync(HttpMethod.Get, path, query, body: null, ct);

    public Task<PlanningCliResponse> PostAsync(string path, object? body, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, path, query: null, body, ct);

    public Task<PlanningCliResponse> PutAsync(string path, object body, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, path, query: null, body, ct);

    private async Task<PlanningCliResponse> SendAsync(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string?>? query,
        object? body,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path + QuerySuffix(query));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        if (SessionId is { } session)
        {
            request.Headers.Add(SessionHeader.Name, session.ToString());
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException
            || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new PlanningCliUnreachableException(
                $"Overmind host is not reachable at {BaseUrl} — start the host "
                + "(or point OVERMIND_API_URL at a running instance) and retry.");
        }

        using (response)
        {
            var payload = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                var detail = ProblemDetail(payload);
                throw new PlanningCliApiException(
                    (int)response.StatusCode,
                    detail ?? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            return new PlanningCliResponse((int)response.StatusCode, payload);
        }
    }

    private static string? QuerySuffix(IReadOnlyDictionary<string, string?>? query)
    {
        if (query is null || query.Count == 0)
        {
            return string.Empty;
        }

        var pairs = query
            .Where(pair => pair.Value is not null)
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}");
        return "?" + string.Join("&", pairs);
    }

    private static string? ProblemDetail(string payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString();
            }

            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString();
            }

            return null;
        }
        catch (JsonException)
        {
            return payload.Length > 400 ? payload[..400] : payload;
        }
    }
}
