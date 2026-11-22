using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Overmind.Host.Api;

// 266/T3, the anti-pattern row (spec table 5): a JSON field the endpoint never reads used to vanish
// silently — an update_feature `lane` once swallowed a whole marshal's intent. The write stays
// accepted and every field the request DOES carry behaves byte-identically, but each unreferenced
// top-level property now surfaces in the existing warnings[] channel, named and marked not applied.
// One probe for the whole agent surface: it compares the buffered body against the request types the
// endpoint actually binds, so no endpoint carries any of this code. Registration is one group line.
public sealed class UnknownPropertyProbeFilter : IEndpointFilter
{
    private const int BodyBudget = 256 * 1024;

    public static readonly UnknownPropertyProbeFilter Instance = new();

    private UnknownPropertyProbeFilter()
    {
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var unknown = Probe(context.HttpContext, context.Arguments);
        var produced = await next(context);
        return unknown.Count == 0 || produced is not IResult result
            ? produced
            : new WarningBearingResult(result, unknown);
    }

    private static IReadOnlyList<string> Probe(HttpContext http, IList<object?> arguments)
    {
        var request = http.Request;
        if (HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || !request.HasJsonContentType()
            || !request.Body.CanSeek
            || request.ContentLength is null or 0 or > BodyBudget)
        {
            return [];
        }

        // The bound body objects are what the endpoint reads; anything else in the arguments is a
        // route value, a validator, a service or the HttpRequest, and names no wire field.
        var contracts = arguments
            .Where(argument => IsRequestContract(argument))
            .Select(argument => argument!.GetType())
            .ToArray();
        if (contracts.Length == 0)
        {
            return [];
        }

        List<string>? unknown = null;
        try
        {
            request.Body.Position = 0;
            using var document = JsonDocument.Parse(ReadBody(request));
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (contracts.All(type => !RequestFields.Known(type).Contains(property.Name)))
                    {
                        unknown ??= [];
                        unknown.Add(property.Name);
                    }
                }
            }
        }
        catch (JsonException)
        {
            return [];
        }
        finally
        {
            request.Body.Position = 0;
        }

        return unknown ?? [];
    }

    private static bool IsRequestContract(object? argument) =>
        argument is not null
        && argument.GetType().Namespace == "Overmind.Host.Api.Contracts"
        && argument.GetType().Name.EndsWith("Request", StringComparison.Ordinal);

    private static string ReadBody(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var text = reader.ReadToEnd();
        request.Body.Position = 0;
        return text;
    }

    // The endpoint's own result runs untouched; only on the way out, when the request really did
    // carry a field nothing reads, the warnings[] array grows. Anything that is not an accepted
    // JSON object (a problem body, a 204, a stream) leaves byte-for-byte as the handler wrote it.
    private sealed class WarningBearingResult(IResult inner, IReadOnlyList<string> unknown) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var original = httpContext.Response.Body;
            await using var buffer = new MemoryStream();
            httpContext.Response.Body = buffer;
            try
            {
                await inner.ExecuteAsync(httpContext);
            }
            finally
            {
                httpContext.Response.Body = original;
            }

            if (!httpContext.Response.HasStarted
                && httpContext.Response.StatusCode is >= 200 and < 300
                && IsJson(httpContext.Response.ContentType)
                && buffer.Length > 0)
            {
                var amended = TryAmend(buffer, unknown);
                if (amended is not null)
                {
                    if (httpContext.Response.ContentLength.HasValue)
                    {
                        httpContext.Response.ContentLength = amended.Length;
                    }

                    await original.WriteAsync(amended, httpContext.RequestAborted);
                    return;
                }
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(original, httpContext.RequestAborted);
        }
    }

    private static bool IsJson(string? contentType) =>
        contentType is not null && contentType.Contains("json", StringComparison.OrdinalIgnoreCase);

    private static byte[]? TryAmend(MemoryStream captured, IReadOnlyList<string> unknown)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(captured.GetBuffer().AsMemory(0, (int)captured.Length));
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var payload = JsonSerializer.Deserialize<JsonObject>(document.RootElement);
            if (payload is null)
            {
                return null;
            }

            JsonArray warnings;
            if (payload.TryGetPropertyValue("warnings", out var existing))
            {
                if (existing is not JsonArray existingArray)
                {
                    return null;
                }

                warnings = existingArray;
            }
            else
            {
                warnings = new JsonArray();
                payload["warnings"] = warnings;
            }

            foreach (var name in unknown)
            {
                warnings.Add(JsonSerializer.SerializeToNode(new UnknownPropertyWarning(name)));
            }

            return Encoding.UTF8.GetBytes(payload.ToJsonString());
        }
    }
}

// The warnings[] payload for a swallowed field. Deliberately not the frozen cap shape: a cap
// warning says what was kept, this one says what was NOT applied.
public sealed record UnknownPropertyWarning(
    [property: JsonPropertyName("property")] string Property)
{
    [JsonPropertyName("applied")]
    public bool Applied { get; init; }

    [JsonPropertyName("note")]
    public string Note { get; init; } =
        "no field named this is read by the call — nothing was applied under this name; " +
        "the result above carries only the fields this tool declares";
}

// The wire names one request type accepts, computed once per type: public readable instance
// properties, honouring JsonPropertyName and a flat JsonIgnore. Case-insensitive because the
// binder itself is — "Lane" binds to lane and is therefore NOT an unknown field.
internal static class RequestFields
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, HashSet<string>> Cache = new();

    public static HashSet<string> Known(Type type) =>
        Cache.GetOrAdd(type, static target =>
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in target.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetCustomAttribute<JsonIgnoreAttribute>()
                    is { Condition: JsonIgnoreCondition.Always })
                {
                    continue;
                }

                names.Add(property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name);
            }

            return names;
        });
}

// The one registration point for the probe: every /api/agent group is born through this helper, so
// the whole agent surface is covered without a single endpoint knowing the probe exists.
public static class AgentRouteGroups
{
    public static RouteGroupBuilder MapAgentGroup(this IEndpointRouteBuilder routes) =>
        routes.MapGroup("/api/agent").AddEndpointFilter(UnknownPropertyProbeFilter.Instance);
}
