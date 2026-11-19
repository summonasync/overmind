using System.Net;
using System.Text;
using System.Text.Json;

namespace Overmind.Infrastructure.Agent;

public sealed class AgentLoopbackClient(HttpClient http)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public const string DelegatedByHeader = "X-Overmind-Delegated-By";

    public async Task<JsonElement> GetAsync(string pathAndQuery, CancellationToken ct) =>
        await SendAsync(() => http.GetAsync(pathAndQuery, HttpCompletionOption.ResponseHeadersRead, ct), "GET " + pathAndQuery, ct);

    public async Task<JsonElement> GetAsync(string pathAndQuery, AgentCaller caller, CancellationToken ct) =>
        await SendAsync(
            () => http.SendAsync(Build(HttpMethod.Get, pathAndQuery, null, caller), HttpCompletionOption.ResponseHeadersRead, ct),
            "GET " + pathAndQuery, ct);

    public async Task<JsonElement> PostAsync(string path, object body, CancellationToken ct) =>
        await SendAsync(() => http.SendAsync(Build(HttpMethod.Post, path, body, null), ct), "POST " + path, ct);

    public async Task<JsonElement> PostAsync(
        string path, object body, IReadOnlyDictionary<string, string>? headers, CancellationToken ct) =>
        await SendAsync(() => http.SendAsync(Build(HttpMethod.Post, path, body, null, headers), ct), "POST " + path, ct);

    public async Task<JsonElement> PostAsync(string path, object body, AgentCaller caller, CancellationToken ct) =>
        await SendAsync(() => http.SendAsync(Build(HttpMethod.Post, path, body, caller), ct), "POST " + path, ct);

    public async Task<JsonElement> PostAsync(
        string path,
        object body,
        AgentCaller caller,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken ct) =>
        await SendAsync(() => http.SendAsync(Build(HttpMethod.Post, path, body, caller, headers), ct), "POST " + path, ct);

    public async Task<JsonElement> PutAsync(string path, object body, CancellationToken ct) =>
        await SendAsync(() => http.SendAsync(Build(HttpMethod.Put, path, body, null), ct), "PUT " + path, ct);

    public async Task<JsonElement> PutAsync(string path, object body, AgentCaller caller, CancellationToken ct) =>
        await SendAsync(() => http.SendAsync(Build(HttpMethod.Put, path, body, caller), ct), "PUT " + path, ct);

    public async Task<JsonElement> DeleteAsync(string path, CancellationToken ct) =>
        await SendAsync(() => http.SendAsync(Build(HttpMethod.Delete, path, null, null), ct), "DELETE " + path, ct);

    public async Task<JsonElement> DeleteAsync(string path, AgentCaller caller, CancellationToken ct) =>
        await SendAsync(() => http.SendAsync(Build(HttpMethod.Delete, path, null, caller), ct), "DELETE " + path, ct);

    public async Task<JsonElement> DeleteAsync(string path, object body, CancellationToken ct) =>
        await SendAsync(() => http.SendAsync(Build(HttpMethod.Delete, path, body, null), ct), "DELETE " + path, ct);

    public async Task<JsonElement> DeleteAsync(string path, object body, AgentCaller caller, CancellationToken ct) =>
        await SendAsync(() => http.SendAsync(Build(HttpMethod.Delete, path, body, caller), ct), "DELETE " + path, ct);

    private HttpRequestMessage Build(
        HttpMethod method,
        string path,
        object? body,
        AgentCaller? caller,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        }

        foreach (var (name, value) in AgentCallerHeaders.Pair(caller, headers))
        {
            request.Headers.Add(name, value);
        }

        return request;
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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new LoopbackApiException(
                LoopbackApiException.UnreachableStatusCode,
                $"overmind API is unreachable ({operation}): {DescribeTimeout(ex)}");
        }

        try
        {
            if (!response.IsSuccessStatusCode)
            {
                var detail = await ReadProblemDetailAsync(response, ct);
                throw new LoopbackApiException(
                    (int)response.StatusCode,
                    $"overmind API returned {(int)response.StatusCode} ({operation}): {detail ?? response.ReasonPhrase}");
            }

            var payload = await response.Content.ReadAsStringAsync(ct);
            if (payload.Length == 0)
            {
                return JsonDocument.Parse("{}").RootElement.Clone();
            }

            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        finally
        {
            response.Dispose();
        }
    }

    private static string DescribeTimeout(Exception ex) =>
        ex is TaskCanceledException ? "the request timed out" : ex.Message;

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
