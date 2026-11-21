using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Overmind.Mcp;

public sealed class AgentSessionLifecycle(HttpClient http)
{
    public const string SessionsPath = "/api/agent/sessions";
    public static readonly TimeSpan EndTimeout = TimeSpan.FromSeconds(3);

    public async Task<Guid> RegisterAsync(string alias, string vendor, Guid? priorSessionId, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync(SessionsPath, new { alias, vendor, priorSessionId }, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return body.GetProperty("id").GetGuid();
    }

    // The host refuses an alias an alive session already holds with 409; the caller keeps its
    // registered identity in that case, so a restore attempt can never wedge startup. The host names
    // the holder in the problem body — that answer is what turns "wait" into "take over", so it is
    // handed back instead of dropped.
    public async Task<(bool Applied, string? Refusal)> TrySetIdentityAsync(
        Guid sessionId, string alias, string vendor, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync(
            $"{SessionsPath}/{sessionId}/identity", new { alias, vendor }, ct);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await ProblemDetailsReader.ReadAsync(response, ct);
            return (false, problem?.Detail ?? problem?.Title);
        }

        response.EnsureSuccessStatusCode();
        return (true, null);
    }

    // ApplicationStopping callbacks are synchronous, so the bounded call is sync-over-async on
    // purpose: a dead host API must never hang the adapter's shutdown past EndTimeout.
    public void MarkEnded(Guid? sessionId)
    {
        if (sessionId is not { } id || id == Guid.Empty)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(EndTimeout);
            http.PostAsync($"{SessionsPath}/{id}/end", new ByteArrayContent([]), timeout.Token)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"[overmind-mcp] WARN: session end call failed: {ex.Message}");
        }
    }
}
