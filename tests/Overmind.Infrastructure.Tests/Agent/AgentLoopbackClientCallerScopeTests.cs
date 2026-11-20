using System.Net;
using System.Text;
using Overmind.Infrastructure.Agent;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class AgentLoopbackClientCallerScopeTests
{
    private static readonly Guid ConversationProject = Guid.Parse("019712af-8ea4-7aa1-9f5b-2f3c4d5e6f01");
    private static readonly AgentCaller Scoped = new(ConversationProject);

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task EveryVerb_WithCaller_SendsTheScopeAndTheAgentMarker(string verb)
    {
        var (client, handler) = Client();
        var entry = Guid.NewGuid();

        await (verb switch
        {
            "GET" => client.GetAsync($"/api/entries/{entry}", Scoped, CancellationToken.None),
            "POST" => client.PostAsync($"/api/agent/planning/artifacts/{entry}/validate", new { }, Scoped,
                CancellationToken.None),
            "PUT" => client.PutAsync($"/api/entries/{entry}", new { content = "x" }, Scoped, CancellationToken.None),
            _ => client.DeleteAsync($"/api/entries/{entry}", new { reason = "gone" }, Scoped, CancellationToken.None),
        });

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ToString().ShouldBe(verb);
        Scope(request).ShouldBe(ConversationProject.ToString("D"), verb);
        Marker(request).ShouldBe(AgentCallerHeaders.MarkerValue, verb);
    }

    [Fact]
    public async Task DeleteAsync_WithoutBody_WithCaller_SendsBothHeaders()
    {
        var (client, handler) = Client();

        await client.DeleteAsync("/api/entries/" + Guid.NewGuid(), Scoped, CancellationToken.None);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Delete);
        Scope(request).ShouldBe(ConversationProject.ToString("D"));
        Marker(request).ShouldBe(AgentCallerHeaders.MarkerValue);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task EveryVerb_ProjectlessCaller_SendsTheGlobalSentinel(string verb)
    {
        var (client, handler) = Client();
        var entry = Guid.NewGuid();

        await (verb switch
        {
            "GET" => client.GetAsync($"/api/entries/{entry}", AgentCaller.Global, CancellationToken.None),
            "POST" => client.PostAsync($"/api/agent/planning/artifacts/{entry}/validate", new { },
                AgentCaller.Global, CancellationToken.None),
            "PUT" => client.PutAsync($"/api/entries/{entry}", new { content = "x" }, AgentCaller.Global,
                CancellationToken.None),
            _ => client.DeleteAsync($"/api/entries/{entry}", new { reason = "gone" }, AgentCaller.Global,
                CancellationToken.None),
        });

        var request = handler.Requests.ShouldHaveSingleItem();
        Marker(request).ShouldBe(AgentCallerHeaders.MarkerValue, verb);
        Scope(request).ShouldBe(AgentCallerHeaders.GlobalScopeValue,
            $"{verb}: a conversation with no project is an honest global call, not a missing header — R4");
    }

    [Fact]
    public async Task MarkerWithoutScope_IsRefusedBeforeTheRequestExists()
    {
        var (client, handler) = Client();
        var half = new Dictionary<string, string> { [AgentCallerHeaders.MarkerHeader] = "embedded-agent" };

        var error = await Should.ThrowAsync<AgentCallerContractException>(
            () => client.PostAsync("/api/entries/" + Guid.NewGuid(), new { }, half, CancellationToken.None));

        error.Code.ShouldBe(AgentCallerHeaders.MarkerWithoutScopeCode);
        error.Message.ShouldContain(AgentCallerHeaders.ScopeHeader);
        handler.Requests.ShouldBeEmpty("a half-assembled caller identity never reaches the host");
    }

    [Fact]
    public void Pair_MarkerWithoutScope_IsTheRefusedShape()
    {
        var half = new Dictionary<string, string> { [AgentCallerHeaders.MarkerHeader] = "embedded-agent" };

        var error = Should.Throw<AgentCallerContractException>(() => AgentCallerHeaders.Pair(null, half));

        error.Code.ShouldBe(AgentCallerHeaders.MarkerWithoutScopeCode);
        error.Message.ShouldContain(AgentCallerHeaders.GlobalScopeValue, Case.Sensitive,
            "the refusal names the sentinel a project-less call should have carried, so the fix is obvious to the caller");
    }

    [Fact]
    public async Task MarkerWithoutScope_InLowerCasing_IsStillRefusedBeforeTheRequestExists()
    {
        var (client, handler) = Client();
        var half = new Dictionary<string, string>
        {
            [AgentCallerHeaders.MarkerHeader.ToLowerInvariant()] = "embedded-agent",
        };

        var error = await Should.ThrowAsync<AgentCallerContractException>(
            () => client.PostAsync("/api/entries/" + Guid.NewGuid(), new { }, half, CancellationToken.None));

        error.Code.ShouldBe(AgentCallerHeaders.MarkerWithoutScopeCode);
        handler.Requests.ShouldBeEmpty(
            "header names are case-insensitive on the wire, so a lowercase marker is the same forgery");
    }

    [Fact]
    public void Pair_WithoutACaller_RefusesAnyReservedHeaderNameNotJustTheHalfPair()
    {
        var forged = new Dictionary<string, string>
        {
            [AgentCallerHeaders.MarkerHeader.ToLowerInvariant()] = "embedded-agent",
            [AgentCallerHeaders.ScopeHeader.ToUpperInvariant()] = Guid.NewGuid().ToString("D"),
        };

        var error = Should.Throw<AgentCallerContractException>(() => AgentCallerHeaders.Pair(null, forged));

        error.Code.ShouldBe(AgentCallerHeaders.ReservedHeaderCollisionCode);
        error.Message.ShouldContain(AgentCallerHeaders.MarkerHeader, Case.Sensitive,
            "a complete pair assembled by hand is still identity the transport never issued");
        error.Message.ShouldContain(AgentCallerHeaders.ScopeHeader, Case.Sensitive);
        error.Message.ShouldNotContain(
            AgentCallerHeaders.MarkerHeader.ToLowerInvariant(), Case.Sensitive,
            "the refusal names the headers as the host spells them, not as the caller typed them");
    }

    [Fact]
    public async Task HandPassedCallerHeaderTogetherWithTheTransportCaller_IsRefusedAsACollision()
    {
        var (client, handler) = Client();
        var extra = new Dictionary<string, string> { [AgentCallerHeaders.ScopeHeader] = Guid.NewGuid().ToString("D") };

        var error = await Should.ThrowAsync<AgentCallerContractException>(
            () => client.PostAsync("/api/agent/planning/artifacts/" + Guid.NewGuid() + "/validate", new { },
                Scoped, extra, CancellationToken.None));

        error.Code.ShouldBe(AgentCallerHeaders.ReservedHeaderCollisionCode);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DelegatedApprovalHeaders_TravelTogetherWithTheCallerScope()
    {
        var (client, handler) = Client();
        var instruction = Guid.NewGuid();
        var headers = new Dictionary<string, string> { [AgentLoopbackClient.DelegatedByHeader] = instruction.ToString("D") };

        await client.PostAsync("/api/agent/planning/artifacts/" + Guid.NewGuid() + "/validate", new { },
            Scoped, headers, CancellationToken.None);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Headers.GetValues(AgentLoopbackClient.DelegatedByHeader).ShouldBe([instruction.ToString("D")]);
        Scope(request).ShouldBe(ConversationProject.ToString("D"));
        Marker(request).ShouldBe(AgentCallerHeaders.MarkerValue);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task UnscopedOverloads_SendNoCallerIdentity(string verb)
    {
        var (client, handler) = Client();
        var entry = Guid.NewGuid();

        await (verb switch
        {
            "GET" => client.GetAsync($"/api/entries/{entry}", CancellationToken.None),
            "POST" => client.PostAsync($"/api/entries/{entry}", new { }, CancellationToken.None),
            "PUT" => client.PutAsync($"/api/entries/{entry}", new { content = "x" }, CancellationToken.None),
            _ => client.DeleteAsync($"/api/entries/{entry}", new { reason = "gone" }, CancellationToken.None),
        });

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Headers.Contains(AgentCallerHeaders.ScopeHeader).ShouldBeFalse(verb);
        request.Headers.Contains(AgentCallerHeaders.MarkerHeader).ShouldBeFalse(verb);
    }

    [Fact]
    public async Task PostWithDelegatedHeadersOnly_KeepsThePre246WireShape()
    {
        var (client, handler) = Client();
        var instruction = Guid.NewGuid();
        var headers = new Dictionary<string, string> { [AgentLoopbackClient.DelegatedByHeader] = instruction.ToString("D") };

        await client.PostAsync("/api/agent/planning/artifacts/" + Guid.NewGuid() + "/validate", new { },
            headers, CancellationToken.None);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Headers.GetValues(AgentLoopbackClient.DelegatedByHeader).ShouldBe([instruction.ToString("D")]);
        request.Headers.Contains(AgentCallerHeaders.MarkerHeader).ShouldBeFalse();
        handler.Bodies.ShouldHaveSingleItem().ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BodySurvivesTheHeaderWidening(bool withCaller)
    {
        var (client, handler) = Client();
        var path = "/api/entries/" + Guid.NewGuid();

        if (withCaller)
        {
            await client.PutAsync(path, new { title = "t", content = "body-canary" }, Scoped, CancellationToken.None);
        }
        else
        {
            await client.PutAsync(path, new { title = "t", content = "body-canary" }, CancellationToken.None);
        }

        handler.Bodies.ShouldHaveSingleItem().ShouldContain("body-canary");
    }

    private static string? Scope(HttpRequestMessage request) =>
        request.Headers.TryGetValues(AgentCallerHeaders.ScopeHeader, out var values) ? values.Single() : null;

    private static string? Marker(HttpRequestMessage request) =>
        request.Headers.TryGetValues(AgentCallerHeaders.MarkerHeader, out var values) ? values.Single() : null;

    private static (AgentLoopbackClient Client, RecordingHandler Handler) Client()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:1") };
        return (new AgentLoopbackClient(http), handler);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly List<HttpRequestMessage> _requests = [];
        private readonly List<string> _bodies = [];

        public IReadOnlyList<HttpRequestMessage> Requests => _requests;

        public IReadOnlyList<string> Bodies => _bodies;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requests.Add(request);
            _bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        }
    }
}
