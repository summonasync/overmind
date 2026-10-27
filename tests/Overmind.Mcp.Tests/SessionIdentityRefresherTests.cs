using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(AdapterSessionState))]
public sealed class SessionIdentityRefresherTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly List<(string Method, string Uri, string Body)> Requests = [];

    public void Dispose()
    {
        AdapterSession.Id = null;
        Environment.SetEnvironmentVariable("OVERMIND_ALIAS", null);
        Environment.SetEnvironmentVariable("OVERMIND_VENDOR", null);
    }

    private SessionIdentityRefresher Refresher(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var http = new HttpClient(new RecordingHandler(Requests, respond))
        {
            BaseAddress = new Uri("http://127.0.0.1:4761"),
        };
        return new SessionIdentityRefresher(new AgentSessionLifecycle(http));
    }

    private static HttpResponseMessage JsonBody(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
        };

    private static HttpResponseMessage SessionBody() => JsonBody(new { id = Guid.NewGuid() });

    [Fact]
    public async Task FirstHandshake_LiveSession_SkipsRegistration()
    {
        AdapterSession.Id = Guid.NewGuid();
        var refresher = Refresher(_ => SessionBody());

        await refresher.RefreshAsync(CancellationToken.None);

        Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task FirstHandshake_MissingSession_RegistersWithCurrentEnv()
    {
        AdapterSession.Id = null;
        Environment.SetEnvironmentVariable("OVERMIND_ALIAS", "van-darkholme");
        Environment.SetEnvironmentVariable("OVERMIND_VENDOR", "claude");
        var refresher = Refresher(_ => SessionBody());

        await refresher.RefreshAsync(CancellationToken.None);

        AdapterSession.Id.ShouldNotBeNull();
        var (method, uri, body) = Requests.Single();
        method.ShouldBe("POST");
        uri.ShouldBe("http://127.0.0.1:4761/api/agent/sessions");
        var payload = JsonNode.Parse(body)!;
        payload["alias"]!.GetValue<string>().ShouldBe("van-darkholme");
        payload["vendor"]!.GetValue<string>().ShouldBe("claude");
    }

    [Fact]
    public async Task Reconnect_ReRegistersWithCurrentEnv_AndEndsPreviousSession()
    {
        AdapterSession.Id = null;
        Environment.SetEnvironmentVariable("OVERMIND_ALIAS", "alpha");
        var refresher = Refresher(_ => SessionBody());

        await refresher.RefreshAsync(CancellationToken.None);
        var afterFirst = AdapterSession.Id!.Value;

        Environment.SetEnvironmentVariable("OVERMIND_ALIAS", "beta");
        await refresher.RefreshAsync(CancellationToken.None);
        var afterSecond = AdapterSession.Id!.Value;

        afterSecond.ShouldNotBe(afterFirst);

        var registrations = Requests
            .Where(r => r.Uri == "http://127.0.0.1:4761/api/agent/sessions")
            .Select(r => JsonNode.Parse(r.Body)!["alias"]!.GetValue<string>())
            .ToArray();
        registrations.ShouldBe(["alpha", "beta"]);

        var endDeadline = DateTime.UtcNow.AddSeconds(5);
        while (!Requests.Any(r => r.Uri.EndsWith("/end", StringComparison.Ordinal)) && DateTime.UtcNow < endDeadline)
        {
            await Task.Delay(25);
        }

        var ended = Requests.Single(r => r.Uri.EndsWith("/end", StringComparison.Ordinal));
        ended.Method.ShouldBe("POST");
        ended.Uri.ShouldBe($"http://127.0.0.1:4761/api/agent/sessions/{afterFirst}/end");
    }

    [Fact]
    public async Task RegistrationFailure_KeepsPreviousSession()
    {
        AdapterSession.Id = null;
        var calls = 0;
        var refresher = Refresher(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                return SessionBody();
            }

            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });

        await refresher.RefreshAsync(CancellationToken.None);
        var registered = AdapterSession.Id!.Value;

        await refresher.RefreshAsync(CancellationToken.None);

        AdapterSession.Id.ShouldBe(registered);
        Requests.ShouldNotContain(r => r.Uri.EndsWith("/end", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HangingHost_RefreshIsBounded()
    {
        AdapterSession.Id = null;
        var http = new HttpClient(new SlowHandler())
        {
            BaseAddress = new Uri("http://127.0.0.1:4761"),
        };
        var refresher = new SessionIdentityRefresher(new AgentSessionLifecycle(http));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        await refresher.RefreshAsync(CancellationToken.None);
        watch.Stop();

        watch.Elapsed.ShouldBeLessThan(SessionIdentityRefresher.Budget + TimeSpan.FromSeconds(3));
        AdapterSession.Id.ShouldBeNull();
    }

    private sealed class RecordingHandler(
        List<(string Method, string Uri, string Body)> requests,
        Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            requests.Add((request.Method.ToString(), request.RequestUri!.ToString(), body));
            return respond(request);
        }
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
}
