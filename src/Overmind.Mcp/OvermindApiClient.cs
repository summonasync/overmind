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

    // Vocabulary reads for the near-miss hints: same call, no hint enrichment, so a failed hint
    // read can never recurse into another one.
    internal Task<JsonElement> GetQuietAsync(string pathAndQuery, CancellationToken ct) =>
        SendAsync(
            () => _http.GetAsync(pathAndQuery, HttpCompletionOption.ResponseHeadersRead, ct),
            "GET " + pathAndQuery, ct, enrich: false);

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

    private async Task<JsonElement> SendAsync(
        Func<Task<HttpResponseMessage>> send,
        string operation,
        CancellationToken ct,
        bool enrich = true)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (HttpRequestException ex)
        {
            throw new ApiUnreachableException($"{RetryableMarker} overmind API is unreachable ({operation}): {ex.Message}");
        }
        catch (Exception ex) when (ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            // A client timeout is a different failure class from an unreachable host: the server
            // may have committed the mutation after the budget elapsed (2d5912f5 measured a
            // successful 201 at ~10.03s against this 10s budget). The message must never invite
            // a blind retry — that turned one review into five duplicate rows.
            throw new ApiUnreachableException(
                $"client timeout: the request did not come back within the adapter budget ({operation}). " +
                "The server MAY have committed — read the state back before retrying; a blind retry can duplicate the row.");
        }

        try
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                var problem = await ProblemDetailsReader.ReadAsync(response, ct);
                var missing = problem?.Detail ?? problem?.Title;
                if (missing is null)
                {
                    throw new ApiNotFoundException($"overmind API resource not found ({operation})");
                }

                throw new ApiNotFoundException(enrich && problem?.Title is not null
                    ? await NearMissHints.AppendAsync(this, missing, problem, operation, ct)
                    : missing);
            }

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                var problem = await ProblemDetailsReader.ReadAsync(response, ct);
                var unavailable = problem?.Detail ?? problem?.Title;
                throw new SemanticSearchUnavailableException(
                    $"vector search unavailable: {unavailable ?? "semantic search is down; use mode=fulltext"}");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiStatusException((int)response.StatusCode, await DescribeAsync(response, operation, ct));
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

    private static async Task<string> DescribeAsync(HttpResponseMessage response, string operation, CancellationToken ct)
    {
        var problem = await ProblemDetailsReader.ReadAsync(response, ct);
        var detail = problem?.Detail ?? problem?.Title ?? response.ReasonPhrase;
        var message = $"overmind API returned {(int)response.StatusCode} ({operation}): {detail}";

        if (response.StatusCode == HttpStatusCode.BadRequest && problem is not null)
        {
            var errors = ProblemDetailsReader.FormatErrors(problem);
            if (errors.Length > 0)
            {
                // host details usually end with a period of their own — never stack two
                message = EnsurePeriod(message) + $" Errors: {errors}";
            }
        }

        if (!string.IsNullOrWhiteSpace(problem?.Fix))
        {
            message = EnsurePeriod(message) + $" Fix: {problem.Fix}";
        }

        return message;
    }

    private static string EnsurePeriod(string message) =>
        message.EndsWith(".", StringComparison.Ordinal) ? message : message + ".";
}
