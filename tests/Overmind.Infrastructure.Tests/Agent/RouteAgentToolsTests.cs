using System.Net.Http;
using System.Text.Json;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Agent.Tools;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class RouteAgentToolsTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly AgentRouteMemoryStore _store = new();
    private readonly AgentRouteRecorder _recorder;
    private readonly Conversation _conversation;
    private readonly ConversationMessageStore _messages;

    public RouteAgentToolsTests()
    {
        _recorder = new AgentRouteRecorder(_store);
        _conversation = Conversation.Create(null, "route tool tests");
        using var context = _temp.CreateContext();
        context.Conversations.Add(_conversation);
        context.SaveChanges();
        _messages = new ConversationMessageStore(_temp.CreateContext());
    }

    [Fact]
    public async Task InvokeAsync_RecordRouteWithSuggestedPattern_StoresTheAnalyzedRowAndMarksTheTurn()
    {
        _recorder.BeginTurn(_conversation.Id);
        var registry = CreateRegistry();

        var result = await registry.InvokeAsync(
            "record_route",
            JsonSerializer.SerializeToElement(new
            {
                domain = "planning",
                missReason = "keywords too specific",
                suggestedPattern = "sort board, backlog order"
            }),
            new AgentToolInvocation(false, null, _conversation.Id, null),
            CancellationToken.None);

        result.GetProperty("recorded").GetBoolean().ShouldBeTrue();
        result.GetProperty("pattern").GetString().ShouldBe("sort board, backlog order");
        result.GetProperty("status").GetString().ShouldBe("provisional");

        var row = (await _store.HotHeadAsync(10)).Single();
        row.Pattern.ShouldBe("sort board, backlog order");
        row.Domain.ShouldBe("planning");
        row.MissReason.ShouldBe("keywords too specific");
        _recorder.TurnAnalyzed(_conversation.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_RecordRouteWithoutSuggestedPattern_UsesTheNormalizedRequestText()
    {
        _recorder.BeginTurn(_conversation.Id);
        await _messages.AppendAsync(
            _conversation.Id, ConversationMessage.UserRole, "Sort my board by priority please!", CancellationToken.None);
        var registry = CreateRegistry();

        var result = await registry.InvokeAsync(
            "record_route",
            JsonSerializer.SerializeToElement(new { domain = "planning", missReason = "no wave rows in hot head" }),
            new AgentToolInvocation(false, null, _conversation.Id, null),
            CancellationToken.None);

        result.GetProperty("pattern").GetString().ShouldBe("sort my board by priority please");
        var row = (await _store.HotHeadAsync(10)).Single();
        row.Pattern.ShouldNotContain("!");
    }

    [Fact]
    public async Task InvokeAsync_RecordRouteWithoutConversationScope_IsRefused()
    {
        var registry = CreateRegistry();

        var result = await registry.InvokeAsync(
            "record_route",
            JsonSerializer.SerializeToElement(new { domain = "planning", missReason = "miss" }),
            default,
            CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldContain("current conversation");
        (await _store.HotHeadAsync(10)).ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_RecordRouteWithNothingToRecord_IsRejected()
    {
        _recorder.BeginTurn(_conversation.Id);
        var registry = CreateRegistry();

        var result = await registry.InvokeAsync(
            "record_route",
            JsonSerializer.SerializeToElement(new { domain = "planning", missReason = "miss" }),
            new AgentToolInvocation(false, null, _conversation.Id, null),
            CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldContain("suggested pattern");
        (await _store.HotHeadAsync(10)).ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_BounceTurn_ReturnsTheStructuredVerdict()
    {
        var registry = CreateRegistry();

        var result = await registry.InvokeAsync(
            "bounce_turn",
            JsonSerializer.SerializeToElement(new { reason = "belongs to the planning specialist" }),
            new AgentToolInvocation(false, null, _conversation.Id, null),
            CancellationToken.None);

        result.GetProperty("bounced").GetBoolean().ShouldBeTrue();
        result.GetProperty("reason").GetString().ShouldBe("belongs to the planning specialist");
    }

    private AgentToolRegistry CreateRegistry()
    {
        var http = new HttpClient(new RecordingHandler()) { BaseAddress = new Uri("http://127.0.0.1:1") };
        var state = new EmbeddedAgentSessionState();
        state.Set(Guid.NewGuid());
        return new AgentToolRegistry(
            new AgentLoopbackClient(http),
            state,
            Substitute.For<IGrimoireMemoryClient>(),
            _messages,
            _recorder);
    }

    public void Dispose() => _temp.Dispose();

    private sealed class RecordingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
