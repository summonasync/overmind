using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(ProcessEnvironment))]
public sealed class SessionIdentityRefresherTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly List<(string Method, string Uri, string Body)> Requests = [];
    private readonly string IdentityRoot = Path.Combine(
        Path.GetTempPath(), "overmind-identity-refresher", Guid.NewGuid().ToString("N"));
    private readonly string _repo;

    public SessionIdentityRefresherTests()
    {
        _repo = Path.Combine(IdentityRoot, "repo");
        Directory.CreateDirectory(Path.Combine(_repo, ".git"));
    }

    public void Dispose()
    {
        if (Directory.Exists(IdentityRoot))
        {
            Directory.Delete(IdentityRoot, recursive: true);
        }

        AdapterSession.Id = null;
        AdapterIdentityState.Reset();
        Environment.SetEnvironmentVariable("OVERMIND_ALIAS", null);
        Environment.SetEnvironmentVariable("OVERMIND_VENDOR", null);
    }

    private SessionIdentityRefresher Refresher(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        SessionIdentityFileWriter? identityFile = null,
        WorkspaceIdentityFile? workspaceIdentity = null)
    {
        var http = new HttpClient(new RecordingHandler(Requests, respond))
        {
            BaseAddress = new Uri("http://127.0.0.1:4761"),
        };
        return new SessionIdentityRefresher(
            new AgentSessionLifecycle(http),
            identityFile ?? IdentityFileWriter(null),
            workspaceIdentity ?? Workspace());
    }

    private SessionIdentityFileWriter IdentityFileWriter(int? parentPid) =>
        new(new FixedParentPid(parentPid), IdentityRoot, Workspace());

    private WorkspaceIdentityFile Workspace() => new(IdentityRoot, _repo);

    private static HttpResponseMessage JsonBody(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
        };

    private static HttpResponseMessage SessionBody() => JsonBody(new { id = Guid.NewGuid() });

    private static HttpResponseMessage IdentityEcho(Guid sessionId, string alias, string vendor) =>
        JsonBody(new
        {
            session = new { id = sessionId, alias, vendor },
            warnings = Array.Empty<string>(),
        });

    private static bool IsIdentityCall(HttpRequestMessage request) =>
        request.RequestUri!.AbsolutePath.EndsWith("/identity", StringComparison.Ordinal);

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
    public async Task Registration_PrefersSlotIdentity_OverEnv()
    {
        AdapterSession.Id = null;
        AdapterIdentityState.Set("runtime", "qwen");
        Environment.SetEnvironmentVariable("OVERMIND_ALIAS", "env-alias");
        var refresher = Refresher(_ => SessionBody());

        await refresher.RefreshAsync(CancellationToken.None);

        AdapterSession.Id.ShouldNotBeNull();
        var payload = JsonNode.Parse(Requests.Single().Body)!;
        payload["alias"]!.GetValue<string>().ShouldBe("runtime");
        payload["vendor"]!.GetValue<string>().ShouldBe("qwen");
    }

    [Fact]
    public async Task Reconnect_AfterRuntimeSet_RegistersUnderSlotIdentity()
    {
        AdapterSession.Id = null;
        Environment.SetEnvironmentVariable("OVERMIND_ALIAS", "env-alias");
        var refresher = Refresher(_ => SessionBody());

        await refresher.RefreshAsync(CancellationToken.None);
        var afterFirst = AdapterSession.Id!.Value;
        AdapterIdentityState.Set("runtime", "claude");

        await refresher.RefreshAsync(CancellationToken.None);
        var afterSecond = AdapterSession.Id!.Value;

        afterSecond.ShouldNotBe(afterFirst);
        var registrations = Requests
            .Where(r => r.Uri == "http://127.0.0.1:4761/api/agent/sessions")
            .Select(r => JsonNode.Parse(r.Body)!)
            .ToArray();
        registrations.Length.ShouldBe(2);
        registrations[0]["alias"]!.GetValue<string>().ShouldBe("env-alias");
        registrations[1]["alias"]!.GetValue<string>().ShouldBe("runtime");
        registrations[1]["vendor"]!.GetValue<string>().ShouldBe("claude");

        var endDeadline = DateTime.UtcNow.AddSeconds(5);
        while (!Requests.Any(r => r.Uri.EndsWith("/end", StringComparison.Ordinal)) && DateTime.UtcNow < endDeadline)
        {
            await Task.Delay(25);
        }

        Requests.ShouldContain(r => r.Uri.EndsWith($"/{afterFirst}/end", StringComparison.Ordinal));
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
        var refresher = new SessionIdentityRefresher(
            new AgentSessionLifecycle(http), IdentityFileWriter(null), Workspace());

        var watch = System.Diagnostics.Stopwatch.StartNew();
        await refresher.RefreshAsync(CancellationToken.None);
        watch.Stop();

        watch.Elapsed.ShouldBeLessThan(SessionIdentityRefresher.Budget + TimeSpan.FromSeconds(3));
        AdapterSession.Id.ShouldBeNull();
    }

    [Fact]
    public async Task HandshakeRegistration_WritesIdentityFileWithRegisteredIdentity()
    {
        AdapterSession.Id = null;
        AdapterIdentityState.Set("runtime", "qwen");
        var refresher = Refresher(_ => SessionBody(), IdentityFileWriter(4242));

        await refresher.RefreshAsync(CancellationToken.None);

        var path = Path.Combine(IdentityRoot, "identity", "parent-4242.json");
        File.Exists(path).ShouldBeTrue();
        var payload = JsonNode.Parse(File.ReadAllText(path))!;
        payload["alias"]!.GetValue<string>().ShouldBe("runtime");
        payload["vendor"]!.GetValue<string>().ShouldBe("qwen");
        payload["sessionId"]!.GetValue<string>().ShouldBe(AdapterSession.Id!.Value.ToString());
        DateTimeOffset.Parse(payload["registeredAt"]!.GetValue<string>())
            .ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Handshake_PlaceholderEnvWithWorkspaceIdentity_SetsIdentityToTheSavedPair()
    {
        AdapterSession.Id = null;
        var sessionId = Guid.NewGuid();
        Workspace().Write(new SessionIdentitySnapshot("mark-wolff", "claude", Guid.NewGuid(), DateTime.UtcNow));
        var refresher = Refresher(request =>
            IsIdentityCall(request)
                ? IdentityEcho(sessionId, "mark-wolff", "claude")
                : JsonBody(new { id = sessionId }));

        await refresher.RefreshAsync(CancellationToken.None);

        var registration = Requests.Single(r => r.Uri == "http://127.0.0.1:4761/api/agent/sessions");
        JsonNode.Parse(registration.Body)!["alias"]!.GetValue<string>().ShouldBe("agent");

        var identity = Requests.Single(r => r.Uri.EndsWith("/identity", StringComparison.Ordinal));
        identity.Uri.ShouldBe($"http://127.0.0.1:4761/api/agent/sessions/{sessionId}/identity");
        var payload = JsonNode.Parse(identity.Body)!;
        payload["alias"]!.GetValue<string>().ShouldBe("mark-wolff");
        payload["vendor"]!.GetValue<string>().ShouldBe("claude");

        var saved = Workspace().TryRead();
        saved!.Alias.ShouldBe("mark-wolff");
        saved.SessionId.ShouldBe(sessionId);
    }

    [Fact]
    public async Task Handshake_ExplicitEnvAlias_WinsOverTheWorkspaceFile()
    {
        AdapterSession.Id = null;
        Environment.SetEnvironmentVariable("OVERMIND_ALIAS", "env-alias");
        Environment.SetEnvironmentVariable("OVERMIND_VENDOR", "qwen");
        Workspace().Write(new SessionIdentitySnapshot("mark-wolff", "claude", Guid.NewGuid(), DateTime.UtcNow));
        var refresher = Refresher(_ => SessionBody());

        await refresher.RefreshAsync(CancellationToken.None);

        Requests.ShouldNotContain(r => r.Uri.EndsWith("/identity", StringComparison.Ordinal));
        var registration = Requests.Single(r => r.Uri == "http://127.0.0.1:4761/api/agent/sessions");
        JsonNode.Parse(registration.Body)!["alias"]!.GetValue<string>().ShouldBe("env-alias");
        Workspace().TryRead()!.Alias.ShouldBe("env-alias");
    }

    [Fact]
    public async Task Handshake_SlotIdentity_OutranksTheWorkspaceFile()
    {
        AdapterSession.Id = null;
        AdapterIdentityState.Set("runtime", "qwen");
        Workspace().Write(new SessionIdentitySnapshot("mark-wolff", "claude", Guid.NewGuid(), DateTime.UtcNow));
        var refresher = Refresher(_ => SessionBody());

        await refresher.RefreshAsync(CancellationToken.None);

        Requests.ShouldNotContain(r => r.Uri.EndsWith("/identity", StringComparison.Ordinal));
        Workspace().TryRead()!.Alias.ShouldBe("runtime");
    }

    [Fact]
    public async Task Handshake_LiveForeignHolderOnTheSavedAlias_FallsBackToEnvIdentity()
    {
        AdapterSession.Id = null;
        var sessionId = Guid.NewGuid();
        Workspace().Write(new SessionIdentitySnapshot("mark-wolff", "claude", sessionId, DateTime.UtcNow));
        var refresher = Refresher(request =>
            IsIdentityCall(request)
                ? new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent(
                        """{"title":"AliasHeld","detail":"Alias 'mark-wolff' is held by an alive session."}""",
                        Encoding.UTF8,
                        "application/json"),
                }
                : JsonBody(new { id = sessionId }));

        await Should.NotThrowAsync(() => refresher.RefreshAsync(CancellationToken.None));

        AdapterSession.Id.ShouldBe(sessionId);
        var registration = Requests.Single(r => r.Uri == "http://127.0.0.1:4761/api/agent/sessions");
        JsonNode.Parse(registration.Body)!["alias"]!.GetValue<string>().ShouldBe("agent");
        Workspace().TryRead()!.Alias.ShouldBe("mark-wolff");
    }

    [Fact]
    public async Task Handshake_HangingIdentityCall_FallsBackToEnvIdentityWithinBudget()
    {
        AdapterSession.Id = null;
        var sessionId = Guid.NewGuid();
        Workspace().Write(new SessionIdentitySnapshot("mark-wolff", "claude", Guid.NewGuid(), DateTime.UtcNow));
        var http = new HttpClient(new HangingIdentityHandler(Requests, sessionId))
        {
            BaseAddress = new Uri("http://127.0.0.1:4761"),
        };
        var refresher = new SessionIdentityRefresher(
            new AgentSessionLifecycle(http), IdentityFileWriter(null), Workspace());

        var watch = System.Diagnostics.Stopwatch.StartNew();
        await refresher.RefreshAsync(CancellationToken.None);
        watch.Stop();

        watch.Elapsed.ShouldBeLessThan(SessionIdentityRefresher.Budget * 2 + TimeSpan.FromSeconds(3));
        AdapterSession.Id.ShouldBe(sessionId);
        Workspace().TryRead()!.Alias.ShouldBe("mark-wolff");
    }

    private sealed class FixedParentPid(int? pid) : IParentProcessIdSource
    {
        public int? Resolve() => pid;
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

    private sealed class HangingIdentityHandler(
        List<(string Method, string Uri, string Body)> requests,
        Guid registeredSession) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            requests.Add((request.Method.ToString(), request.RequestUri!.ToString(), body));

            if (IsIdentityCall(request))
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }

            return JsonBody(new { id = registeredSession });
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
