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

public sealed class RouterConversationStreamSourceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly IAgentRouteStore _store;
    private readonly ILlmChatClientFactory _chatFactory = Substitute.For<ILlmChatClientFactory>();
    private readonly ScriptedWorkerSource _worker;
    private readonly RouterConversationStreamSource _source;
    private readonly Conversation _conversation = Conversation.Create(null, "router stream");

    public RouterConversationStreamSourceTests()
    {
        _store = new AgentRouteEntityStore(_db.CreateContext());
        _worker = new ScriptedWorkerSource();
        var router = new IntentRouter(
            new RouterPromptComposer(),
            _chatFactory,
            _store,
            Substitute.For<IRouteVectorIndex>(),
            new CountingBudgetStore(),
            NullLogger<IntentRouter>.Instance);
        _source = new RouterConversationStreamSource(
            router,
            new RouterChatResponder(
                new RouterPromptComposer(),
                _chatFactory,
                new CountingBudgetStore(),
                NullLogger<RouterChatResponder>.Instance),
            _worker,
            NullLogger<RouterConversationStreamSource>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task StreamAsync_ClassificationFallback_EmitsRouteEventThenWorkerTurn()
    {
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns((IChatClient?)null);
        _worker.Enqueue(
            ConversationStreamEvent.Delta("working on it "),
            ConversationStreamEvent.ToolCall("planning_task_queue"),
            ConversationStreamEvent.Completed());

        var events = await CollectAsync("show me the board", CancellationToken.None);

        events[0].Kind.ShouldBe(ConversationStreamEventKind.Routed);
        events[0].RouteTarget.ShouldBe(RoutingTargets.Worker);
        events[0].RouteSource.ShouldBe(RoutingSource.Classification);
        events.Select(streamEvent => streamEvent.Kind).ShouldContain(ConversationStreamEventKind.ToolCall);
        events.Last().Kind.ShouldBe(ConversationStreamEventKind.Completed);
        _worker.Invocations.ShouldBe(1);
        _worker.ReceivedTexts.Single().ShouldBe("show me the board");
        var miss = _worker.ReceivedMiss.Single();
        miss.ShouldNotBeNull();
        miss!.Reason.ShouldBe(IntentRouter.NoProviderReason);
    }

    [Fact]
    public async Task StreamAsync_LearnedMatch_RoutesDirectlyWithLearnedSource()
    {
        await _store.RecordAsync(
            new AgentRouteRecording("show wave chat unread", "wave", "learned ask"), CancellationToken.None);
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns(new FakeAgentChatClient());
        _worker.Enqueue(
            ConversationStreamEvent.Delta("here it is"),
            ConversationStreamEvent.Completed(42));

        var events = await CollectAsync("please show the wave chat unread now", CancellationToken.None);

        events[0].RouteSource.ShouldBe(RoutingSource.Learned);
        events.Last().Kind.ShouldBe(ConversationStreamEventKind.Completed);
        events.Last().TokensUsed.ShouldBe(42);
        _worker.ReceivedMiss.Single()!.Reason.ShouldContain("learned pattern match");
        _ = _chatFactory.DidNotReceiveWithAnyArgs().CreateAsync(default);
    }

    [Fact]
    public async Task StreamAsync_ChatDecision_AnswersWithoutTouchingTheWorker()
    {
        var chatClient = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("""{"target":"chat","reason":"small talk"}"""),
            FakeAgentChatClient.UsageUpdate(3, 5),
        ], [
            FakeAgentChatClient.TextUpdate("hey! "),
            FakeAgentChatClient.TextUpdate("how can I help?"),
        ]);
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns(chatClient);

        var events = await CollectAsync("hello router!", CancellationToken.None);

        events[0].RouteTarget.ShouldBe(RoutingTargets.Chat);
        events.Count(streamEvent => streamEvent.Kind == ConversationStreamEventKind.Delta).ShouldBe(2);
        events.Last().Kind.ShouldBe(ConversationStreamEventKind.Completed);
        _worker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task StreamAsync_WorkerBounce_ReRoutesWithinBudget()
    {
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns((IChatClient?)null);
        _worker.Enqueue(
            ConversationStreamEvent.Bounced("wave only"),
            ConversationStreamEvent.Completed());
        _worker.Enqueue(
            ConversationStreamEvent.Delta("handled"),
            ConversationStreamEvent.Completed());

        var events = await CollectAsync("do something", CancellationToken.None);

        events.Count(streamEvent => streamEvent.Kind == ConversationStreamEventKind.Routed).ShouldBe(2);
        events.Last().Kind.ShouldBe(ConversationStreamEventKind.Completed);
        _worker.Invocations.ShouldBe(2);
        var secondMiss = _worker.ReceivedMiss.Last();
        secondMiss.ShouldNotBeNull();
        secondMiss!.Reason.ShouldContain("re-route landed on the bounced target worker");
        secondMiss.Reason.ShouldContain("wave only");
    }

    [Fact]
    public async Task StreamAsync_BounceBudgetExhausted_StopsAfterTheFinalFallback()
    {
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns((IChatClient?)null);
        _worker.Enqueue(
            ConversationStreamEvent.Bounced("first refusal"),
            ConversationStreamEvent.Completed());
        _worker.Enqueue(
            ConversationStreamEvent.Bounced("second refusal"),
            ConversationStreamEvent.Completed());
        _worker.Enqueue(
            ConversationStreamEvent.Delta("third time answers anyway"),
            ConversationStreamEvent.Completed());

        var events = await CollectAsync("do the thing", CancellationToken.None);

        events.Count(streamEvent => streamEvent.Kind == ConversationStreamEventKind.Routed).ShouldBe(3);
        events.Last().Kind.ShouldBe(ConversationStreamEventKind.Completed);
        _worker.Invocations.ShouldBe(3);
        var finalMiss = _worker.ReceivedMiss.Last();
        finalMiss.ShouldNotBeNull();
        finalMiss!.Reason.ShouldContain("bounce budget exhausted after 2");
        finalMiss.Reason.ShouldContain("second refusal");
    }

    [Fact]
    public async Task StreamAsync_WorkerFailure_TerminatesWithTheFailureEvent()
    {
        _chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns((IChatClient?)null);
        _worker.Enqueue(
            ConversationStreamEvent.Delta("half answer"),
            ConversationStreamEvent.Failed("the model stream broke", "modelStreamFailed"));

        var events = await CollectAsync("do it", CancellationToken.None);

        events.Last().Kind.ShouldBe(ConversationStreamEventKind.Failed);
        events.Last().Code.ShouldBe("modelStreamFailed");
        _worker.Invocations.ShouldBe(1);
    }

    private async Task<List<ConversationStreamEvent>> CollectAsync(string userText, CancellationToken ct)
    {
        var events = new List<ConversationStreamEvent>();
        await foreach (var streamEvent in _source.StreamAsync(_conversation, [], userText, ct))
        {
            events.Add(streamEvent);
        }

        return events;
    }

    private sealed class ScriptedWorkerSource : IConversationStreamSource
    {
        private readonly Queue<ConversationStreamEvent[]> _turns = new();

        public int Invocations { get; private set; }

        public List<string> ReceivedTexts { get; } = [];

        public List<RoutingMissContext?> ReceivedMiss { get; } = [];

        public void Enqueue(params ConversationStreamEvent[] events) => _turns.Enqueue(events);

        public async IAsyncEnumerable<ConversationStreamEvent> StreamAsync(
            Conversation conversation,
            IReadOnlyList<ConversationMessage> history,
            string userText,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default,
            RoutingMissContext? missContext = null)
        {
            Invocations++;
            ReceivedTexts.Add(userText);
            ReceivedMiss.Add(missContext);
            foreach (var streamEvent in _turns.Dequeue())
            {
                await Task.Yield();
                yield return streamEvent;
            }
        }
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
}
