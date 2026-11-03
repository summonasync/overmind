using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Http;

namespace Overmind.Infrastructure.Grimoire;

public sealed class GrimoireMemoryClient : IGrimoireMemoryClient
{
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan HealthProbeTimeout = TimeSpan.FromSeconds(3);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _http;

    public GrimoireMemoryClient(IServiceScopeFactory scopeFactory)
        : this(scopeFactory, new HttpClient(LoopbackHttp.CreateProxyBypassHandler()) { Timeout = RequestTimeout })
    {
    }

    public GrimoireMemoryClient(IServiceScopeFactory scopeFactory, HttpClient http)
    {
        _scopeFactory = scopeFactory;
        _http = http;
    }

    public async Task<GrimoireMemoryEndpoint> ResolveEndpointAsync(GrimoireTomeTarget target, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<AppSettingsService>();
        var overrideUrl = (await settings.GetOrDefaultAsync(
            SettingKeys.AgentGrimoireBaseUrl, SettingDefaults.AgentGrimoireBaseUrl, ct)).Trim();
        var tome = await ResolveTomeAsync(settings, target, ct);
        var alias = (await settings.GetOrDefaultAsync(SettingKeys.AgentAlias, SettingDefaults.AgentAlias, ct)).Trim();

        if (overrideUrl.Length > 0)
        {
            var baseUrl = overrideUrl.TrimEnd('/');
            if (!await IsHealthyAsync(baseUrl, ct))
            {
                throw new GrimoireMemoryException(
                    $"grimoire memory is unavailable: the override '{baseUrl}' failed its health probe{BootstrapHint(target, tome)}");
            }

            return new GrimoireMemoryEndpoint(baseUrl, tome, alias);
        }

        var detection = await scope.ServiceProvider.GetRequiredService<GrimoireDetectionService>().DetectAsync(ct);
        if (detection.Status == GrimoireStatus.Running && detection.Port is { } port)
        {
            return new GrimoireMemoryEndpoint($"http://127.0.0.1:{port}", tome, alias);
        }

        throw new GrimoireMemoryException(
            $"grimoire memory is unavailable: no running grimoire instance was detected " +
            $"(detection status: {detection.Status}) and '{SettingKeys.AgentGrimoireBaseUrl}' is not set{BootstrapHint(target, tome)}");
    }

    private async Task<string> ResolveTomeAsync(AppSettingsService settings, GrimoireTomeTarget target, CancellationToken ct)
    {
        if (target == GrimoireTomeTarget.Personal)
        {
            var personal = ((await settings.GetAsync(SettingKeys.AgentGrimoirePersonalTome, ct)) ?? string.Empty).Trim();
            if (personal.Length > 0)
            {
                return personal;
            }

            return (await settings.GetOrDefaultAsync(SettingKeys.AgentAlias, SettingDefaults.AgentAlias, ct)).Trim();
        }

        var crew = ((await settings.GetAsync(SettingKeys.AgentGrimoireCrewTome, ct)) ?? string.Empty).Trim();
        if (crew.Length > 0)
        {
            return crew;
        }

        var legacy = ((await settings.GetAsync(SettingKeys.AgentGrimoireTome, ct)) ?? string.Empty).Trim();
        return legacy.Length > 0 ? legacy : SettingDefaults.AgentGrimoireCrewTome;
    }

    private static string BootstrapHint(GrimoireTomeTarget target, string tome) => target == GrimoireTomeTarget.Personal
        ? $" (the personal tome '{tome}' is created by grimoire on the first write; " +
          $"'{SettingKeys.AgentGrimoirePersonalTome}' overrides the name, the default is agent.alias)"
        : string.Empty;

    public async Task<GrimoireMemoryWriteResult> RememberAsync(
        GrimoireMemoryEndpoint endpoint, GrimoireMemoryWriteRequest request, CancellationToken ct)
    {
        var provenance = new List<string>
        {
            $"overmind://actor/{endpoint.ActorAlias}",
            $"overmind://vendor/{EmbeddedAgentSessionRegistrar.Vendor}",
        };
        provenance.AddRange(request.Tags);

        var body = new Dictionary<string, object?>
        {
            ["content"] = request.Content,
            ["scope"] = request.Scope,
            ["kind"] = request.Kind,
            ["tome"] = endpoint.Tome,
            ["tags"] = provenance,
            ["importance"] = request.Importance,
        };
        if (request.Supersedes is { } supersedes)
        {
            body["supersedes"] = supersedes;
        }

        var response = await SendAsync(
            () => _http.PostAsync(
                $"{endpoint.BaseUrl}/api/memories",
                new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json"),
                ct),
            "POST /api/memories",
            ct);

        var tome = TextOf(response, "tome");
        return new GrimoireMemoryWriteResult(
            Guid.Parse(response.GetProperty("record").GetProperty("id").GetString()!),
            response.TryGetProperty("supersededId", out var superseded) && superseded.ValueKind == JsonValueKind.String
                ? Guid.Parse(superseded.GetString()!)
                : null,
            tome.Length > 0 ? tome : endpoint.Tome);
    }

    public async Task<IReadOnlyList<GrimoireMemoryRecord>> RecallAsync(
        GrimoireMemoryEndpoint endpoint, string query, string? kind, string? tag, int limit, CancellationToken ct) =>
        await ListAsync(endpoint, BuildListQuery(endpoint, kind, tag, limit) + $"&query={Uri.EscapeDataString(query)}", ct);

    public async Task<IReadOnlyList<GrimoireMemoryRecord>> ListTopFactsAsync(
        GrimoireMemoryEndpoint endpoint, int limit, CancellationToken ct) =>
        await ListAsync(endpoint, BuildListQuery(endpoint, "fact", null, limit), ct);

    public async Task<IReadOnlyList<GrimoireCompactionGroup>> GetCompactionCandidatesAsync(
        GrimoireMemoryEndpoint endpoint, CancellationToken ct)
    {
        var response = await SendAsync(
            () => _http.GetAsync($"{endpoint.BaseUrl}/api/compaction/candidates?tome={Uri.EscapeDataString(endpoint.Tome)}", ct),
            "GET /api/compaction/candidates",
            ct);

        return response.TryGetProperty("groups", out var groups) && groups.ValueKind == JsonValueKind.Array
            ? [.. groups.EnumerateArray().Select(ParseCompactionGroup)]
            : [];
    }

    public async Task ForgetAsync(Guid memoryId, string reason, GrimoireMemoryEndpoint endpoint, CancellationToken ct) =>
        await SendAsync(
            () => _http.PostAsync(
                $"{endpoint.BaseUrl}/api/memories/{memoryId}/forget",
                new StringContent(JsonSerializer.Serialize(new
                {
                    reason,
                    tome = endpoint.Tome,
                }, JsonOptions), Encoding.UTF8, "application/json"),
                ct),
            $"POST /api/memories/{memoryId}/forget",
            ct);

    private static string BuildListQuery(GrimoireMemoryEndpoint endpoint, string? kind, string? tag, int limit)
    {
        var pairs = new List<string>
        {
            $"tome={Uri.EscapeDataString(endpoint.Tome)}",
            $"limit={Math.Clamp(limit, 1, 50)}",
        };
        if (!string.IsNullOrWhiteSpace(kind))
        {
            pairs.Add($"kind={Uri.EscapeDataString(kind.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            pairs.Add($"tag={Uri.EscapeDataString(tag.Trim())}");
        }

        return string.Join("&", pairs);
    }

    private async Task<IReadOnlyList<GrimoireMemoryRecord>> ListAsync(
        GrimoireMemoryEndpoint endpoint, string query, CancellationToken ct)
    {
        var response = await SendAsync(
            () => _http.GetAsync($"{endpoint.BaseUrl}/api/memories?{query}", ct),
            "GET /api/memories",
            ct);

        return response.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? [.. items.EnumerateArray().Select(ParseRecord)]
            : [];
    }

    private async Task<bool> IsHealthyAsync(string baseUrl, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(HealthProbeTimeout);
            using var response = await _http.GetAsync($"{baseUrl}/health", timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<JsonElement> SendAsync(
        Func<Task<HttpResponseMessage>> send, string operation, CancellationToken ct)
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
            throw new GrimoireMemoryException(
                $"grimoire memory is unreachable ({operation}): {ex.Message}");
        }

        try
        {
            var payload = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new GrimoireMemoryException(
                    $"grimoire memory call failed with {(int)response.StatusCode} ({operation}): {DescribeError(payload, response.ReasonPhrase)}");
            }

            if (payload.Length == 0)
            {
                return JsonDocument.Parse("{}").RootElement.Clone();
            }

            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException)
        {
            throw new GrimoireMemoryException($"grimoire memory returned an unusable payload ({operation}): {ex.Message}");
        }
    }

    private static string DescribeError(string payload, string? reasonPhrase)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? reasonPhrase ?? "no detail";
            }
        }
        catch (JsonException)
        {
        }

        return string.IsNullOrWhiteSpace(payload) ? reasonPhrase ?? "no detail" : payload;
    }

    private static GrimoireMemoryRecord ParseRecord(JsonElement element) => new(
        Guid.Parse(TextOf(element, "id")),
        TextOf(element, "content"),
        TextOf(element, "kind"),
        element.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array
            ? [.. tags.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString()!)]
            : [],
        TryReadDate(element, "createdAt"),
        TryReadDate(element, "lastAccessedAt"),
        element.TryGetProperty("actor", out var actor) && actor.ValueKind == JsonValueKind.Object &&
            actor.TryGetProperty("alias", out var alias) && alias.ValueKind == JsonValueKind.String
            ? alias.GetString()
            : null);

    private static GrimoireCompactionGroup ParseCompactionGroup(JsonElement element) => new(
        element.TryGetProperty("memoryIds", out var ids) && ids.ValueKind == JsonValueKind.Array
            ? [.. ids.EnumerateArray().Where(id => Guid.TryParse(id.GetString(), out _)).Select(id => Guid.Parse(id.GetString()!))]
            : [],
        element.TryGetProperty("similarity", out var similarity) && similarity.ValueKind == JsonValueKind.Number
            ? similarity.GetDouble()
            : 0,
        element.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array
            ? [.. entries.EnumerateArray().Select(ParseRecord)]
            : []);

    private static string TextOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static DateTimeOffset TryReadDate(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;
}
