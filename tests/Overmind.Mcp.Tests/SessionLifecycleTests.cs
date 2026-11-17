using System.Net;
using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(ProcessEnvironment))]
public sealed class SessionLifecycleTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly List<(string Method, string Uri, string? SessionHeader)> Requests = [];

    public void Dispose()
    {
        AdapterSession.Id = null;
        Environment.SetEnvironmentVariable("OVERMIND_ALIAS", null);
        Environment.SetEnvironmentVariable("OVERMIND_VENDOR", null);
    }

    private HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        return new HttpClient(new AdapterSessionHandler(new RecordingHandler(Requests, respond)))
        {
            BaseAddress = new Uri("http://127.0.0.1:4761"),
        };
    }

    private static HttpResponseMessage JsonBody(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
        };

    [Fact]
    public async Task Register_PostsAliasVendor_ReturnsSessionId()
    {
        var sessionId = Guid.NewGuid();
        var http = Client(_ => JsonBody(new { id = sessionId, alias = "Dan", vendor = "qwen" }));
        var lifecycle = new AgentSessionLifecycle(http);

        var returned = await lifecycle.RegisterAsync("Dan", "qwen", CancellationToken.None);

        returned.ShouldBe(sessionId);
        var (method, uri, header) = Requests.Single();
        method.ShouldBe("POST");
        uri.ShouldBe("http://127.0.0.1:4761/api/agent/sessions");
        header.ShouldBeNull();
    }

    [Fact]
    public async Task Register_ApiError_Throws()
    {
        var http = Client(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var lifecycle = new AgentSessionLifecycle(http);

        await Should.ThrowAsync<HttpRequestException>(() =>
            lifecycle.RegisterAsync("Dan", "qwen", CancellationToken.None));
    }

    [Fact]
    public void MarkEnded_PostsToEndEndpoint()
    {
        var sessionId = Guid.NewGuid();
        AdapterSession.Id = sessionId;
        var http = Client(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var lifecycle = new AgentSessionLifecycle(http);

        lifecycle.MarkEnded(sessionId);

        var (method, uri, header) = Requests.Single();
        method.ShouldBe("POST");
        uri.ShouldBe($"http://127.0.0.1:4761/api/agent/sessions/{sessionId}/end");
        header.ShouldBe(sessionId.ToString());
    }

    [Fact]
    public void MarkEnded_NullSession_SkipsCall()
    {
        var http = Client(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var lifecycle = new AgentSessionLifecycle(http);

        lifecycle.MarkEnded(null);

        Requests.ShouldBeEmpty();
    }

    [Fact]
    public void MarkEnded_DeadApi_ReturnsBounded()
    {
        var slowHandler = new SlowHandler();
        var http = new HttpClient(slowHandler)
        {
            BaseAddress = new Uri("http://127.0.0.1:4761"),
        };
        var lifecycle = new AgentSessionLifecycle(http);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        lifecycle.MarkEnded(Guid.NewGuid());
        watch.Stop();

        watch.Elapsed.ShouldBeLessThan(AgentSessionLifecycle.EndTimeout + TimeSpan.FromSeconds(5));
    }

    private sealed class SlowHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(30_000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }

    [Fact]
    public async Task SessionHandler_StampsHeader_WhenAdapterSessionSet()
    {
        AdapterSession.Id = Guid.NewGuid();
        var http = Client(_ => JsonBody(new { ok = true }));

        await http.GetAsync("/api/health");

        Requests.Single().SessionHeader.ShouldBe(AdapterSession.Id!.Value.ToString());
    }

    [Fact]
    public async Task SessionHandler_NoAdapterSession_Headerless()
    {
        AdapterSession.Id = null;
        var http = Client(_ => JsonBody(new { ok = true }));

        await http.GetAsync("/api/health");

        Requests.Single().SessionHeader.ShouldBeNull();
    }

    [Fact]
    public void AgentIdentity_EnvOverride_FallsBackToDefaults()
    {
        AgentIdentity.ResolveAlias().ShouldBe("agent");
        AgentIdentity.ResolveVendor().ShouldBe("unknown");

        Environment.SetEnvironmentVariable("OVERMIND_ALIAS", "  Dan  ");
        Environment.SetEnvironmentVariable("OVERMIND_VENDOR", "qwen");

        AgentIdentity.ResolveAlias().ShouldBe("Dan");
        AgentIdentity.ResolveVendor().ShouldBe("qwen");
    }

    private sealed class RecordingHandler(
        List<(string Method, string Uri, string? SessionHeader)> requests,
        Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Add((
                request.Method.ToString(),
                request.RequestUri!.ToString(),
                request.Headers.TryGetValues(AdapterSession.HeaderName, out var values)
                    ? values.Single()
                    : null));

            return Task.FromResult(respond(request));
        }
    }
}
