using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Llm;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class IntentRouterTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly IAgentRouteStore _store;
    private readonly ILlmChatClientFactory _chatFactory = Substitute.For<ILlmChatClientFactory>();
    private readonly CountingBudgetStore _budget = new();
    private readonly IntentRouter _router;
    private readonly Conversation _conversation = Conversation.Create(null, "router decisions");

    public IntentRouterTests()
    {
        _store = new AgentRouteEntityStore(_db.CreateContext());
        _router = new IntentRouter(
            new RouterPromptComposer(),
            _chatFactory,
            _store,
            _budget,
            NullLogger<IntentRouter>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task GetHotHeadAsync_ReturnsStoreRowsOrderedByHits()
    {
        await _store.RecordAsync(new AgentRouteRecording("route alpha", "planning", "miss"), CancellationToken.None);
        await _store.RecordAsync(new AgentRouteRecording("route beta", "wave", "miss"), CancellationToken.None);
        await _store.RecordAsync(new AgentRouteRecording("route beta", "wave", "miss again"), CancellationToken.None);

        var hotHead = await _router.GetHotHeadAsync(CancellationToken.None);

        hotHead.Count.ShouldBe(2);
        hotHead[0].Pattern.ShouldBe("route beta");
    }

    [Fact]
    public async Task RouteAsync_LearnedMatch_RoutesWithoutAnyModelCall()
    {
        await _store.RecordAsync(
            new AgentRouteRecording("show wave chat unread", "wave", "learned ask"), CancellationToken.None);
        var hotHead = await _router.GetHotHeadAsync(CancellationToken.None);

        var decision = await _router.RouteAsync(
            _conversation, [], "please show me the wave chat unread count", hotHead, CancellationToken.None);

        decision.Target.ShouldBe(RoutingTargets.Worker);
        decision.Source.ShouldBe(RoutingSource.Learned);
        decision.Miss.Reason.ShouldContain("learned pattern match");
        _ = _chatFactory.DidNotReceiveWithAnyArgs().CreateAsync(default);
    }

    [Fact]
    public async Task RouteAsync_KeywordListMatch_CatchesThePhrasingVariant()
    {
        await _store.RecordAsync(
            new AgentRouteRecording("wave chat, unread count", "wave", "keywords learned by the worker"), CancellationToken.None);
        var hotHead = await _router.GetHotHeadAsync(CancellationToken.None);

        var decision = await _router.RouteAsync(
            _conversation, [], "how big is the unread count right now?", hotHead, CancellationToken.None);

        decision.Source.ShouldBe(RoutingSource.Learned);
    }

    [Fact]
    public async Task RouteAsync_ClassifierChoosesChat_StaysWithTheRouter()
    {
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("""{"target":"chat","reason":"small talk"}"""),
            FakeAgentChatClient.UsageUpdate(4, 6),
        ]);
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns(client);
        var hotHead = await _router.GetHotHeadAsync(CancellationToken.None);

        var decision = await _router.RouteAsync(_conversation, [], "hey there!", hotHead, CancellationToken.None);

        decision.Target.ShouldBe(RoutingTargets.Chat);
        decision.Source.ShouldBe(RoutingSource.Classification);
        decision.Miss.Reason.ShouldContain("small talk");
        _budget.Recorded.ShouldBe(6);
    }

    [Fact]
    public async Task RouteAsync_ClassifierChoosesWorker_ForwardsWithMissContext()
    {
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("""{"target":"worker","reason":"touches the planning board"}"""),
        ]);
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns(client);
        var hotHead = await _router.GetHotHeadAsync(CancellationToken.None);

        var decision = await _router.RouteAsync(_conversation, [], "move feature 42 to the top", hotHead, CancellationToken.None);

        decision.Target.ShouldBe(RoutingTargets.Worker);
        decision.Source.ShouldBe(RoutingSource.Classification);
        decision.Miss.Reason.ShouldContain("touches the planning board");
        decision.Miss.HotHeadSize.ShouldBe(0);
    }

    [Fact]
    public async Task RouteAsync_ClassificationFailure_FallsBackWithoutDroppingTheRequest()
    {
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns(new ThrowingChatClient());
        var hotHead = await _router.GetHotHeadAsync(CancellationToken.None);

        var decision = await _router.RouteAsync(_conversation, [], "build the report", hotHead, CancellationToken.None);

        decision.Target.ShouldBe(RoutingTargets.Worker);
        decision.Miss.Reason.ShouldBe(IntentRouter.ClassificationFailedReason);
        _budget.Recorded.ShouldBe(0);
    }

    [Fact]
    public async Task RouteAsync_UnparseableClassifierOutput_FallsBack()
    {
        var client = new FakeAgentChatClient([FakeAgentChatClient.TextUpdate("I think the worker should take it, sounds good!")]);
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns(client);
        var hotHead = await _router.GetHotHeadAsync(CancellationToken.None);

        var decision = await _router.RouteAsync(_conversation, [], "do the thing", hotHead, CancellationToken.None);

        decision.Target.ShouldBe(RoutingTargets.Worker);
        decision.Miss.Reason.ShouldBe(IntentRouter.ClassificationUnparseableReason);
    }

    [Fact]
    public async Task RouteAsync_NoProviderConfigured_FallsBack()
    {
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns((IChatClient?)null);
        var hotHead = await _router.GetHotHeadAsync(CancellationToken.None);

        var decision = await _router.RouteAsync(_conversation, [], "do the thing", hotHead, CancellationToken.None);

        decision.Target.ShouldBe(RoutingTargets.Worker);
        decision.Miss.Reason.ShouldBe(IntentRouter.NoProviderReason);
    }

    [Fact]
    public async Task ReRouteAsync_WithinBudget_RoutesAgain()
    {
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("""{"target":"chat","reason":"small talk"}"""),
        ]);
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns(client);
        var hotHead = await _router.GetHotHeadAsync(CancellationToken.None);

        var decision = await _router.ReRouteAsync(
            "wrong domain", RoutingTargets.Worker, 1, _conversation, [], "hey again", hotHead, CancellationToken.None);

        decision.Target.ShouldBe(RoutingTargets.Chat);
        decision.Source.ShouldBe(RoutingSource.Classification);
    }

    [Fact]
    public async Task ReRouteAsync_BudgetExhausted_FallsBackWithBounceReason()
    {
        var hotHead = await _router.GetHotHeadAsync(CancellationToken.None);

        var decision = await _router.ReRouteAsync(
            "wave only", RoutingTargets.Worker, IntentRouter.MaxBounces,
            _conversation, [], "anything", hotHead, CancellationToken.None);

        decision.Target.ShouldBe(RoutingTargets.Worker);
        decision.Miss.Reason.ShouldContain("bounce budget exhausted after 2");
        decision.Miss.Reason.ShouldContain("wave only");
    }

    [Fact]
    public async Task ReRouteAsync_ReRouteKeepsTheBouncedTarget_FallsBack()
    {
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("""{"target":"worker","reason":"still the worker"}"""),
        ]);
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns(client);
        var hotHead = await _router.GetHotHeadAsync(CancellationToken.None);

        var decision = await _router.ReRouteAsync(
            "not mine", RoutingTargets.Worker, 1,
            _conversation, [], "some request", hotHead, CancellationToken.None);

        decision.Target.ShouldBe(RoutingTargets.Worker);
        decision.Miss.Reason.ShouldContain("re-route landed on the bounced target worker");
        decision.Miss.Reason.ShouldContain("not mine");
    }

    private sealed class CountingBudgetStore : IAgentBudgetStore
    {
        public long Recorded { get; private set; }

        public Task<long> GetUsedAsync(DateOnly day, CancellationToken ct) => Task.FromResult(Recorded);

        public Task<long> AddUsageAsync(DateOnly day, long tokens, CancellationToken ct)
        {
            Recorded += tokens;
            return Task.FromResult(Recorded);
        }
    }

    private sealed class ThrowingChatClient : IChatClient
    {
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new InvalidOperationException("classification call failed on purpose");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        void IDisposable.Dispose()
        {
        }
    }
}
