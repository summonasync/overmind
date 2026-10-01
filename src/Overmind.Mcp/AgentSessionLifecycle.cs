using System.Net.Http.Json;
using System.Text.Json;

namespace Overmind.Mcp;

public sealed class AgentSessionLifecycle(HttpClient http)
{
    public const string SessionsPath = "/api/agent/sessions";
    public static readonly TimeSpan EndTimeout = TimeSpan.FromSeconds(3);

    public async Task<Guid> RegisterAsync(string alias, string vendor, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync(SessionsPath, new { alias, vendor }, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return body.GetProperty("id").GetGuid();
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
