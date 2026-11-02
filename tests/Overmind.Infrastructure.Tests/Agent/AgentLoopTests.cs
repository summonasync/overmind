using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Llm;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class AgentLoopTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly IAgentToolInvoker _tools = Substitute.For<IAgentToolInvoker>();
    private readonly IAgentGuardNotifier _notifier = Substitute.For<IAgentGuardNotifier>();
    private readonly FakeBudgetStore _budget = new();
    private readonly EmbeddedAgentSessionState _sessionState = new();
    private readonly Conversation _conversation;
    private readonly ConversationMessageStore _messages;

    public AgentLoopTests()
    {
        _tools.CreateTools().Returns([]);
        _tools.InvokeAsync(Arg.Any<string>(), Arg.Any<JsonElement>(), Arg.Any<CancellationToken>())
            .Returns(call => JsonSerializer.SerializeToElement(new { ok = true }));
        _conversation = Conversation.Create(null, "loop tests");
        using var context = _temp.CreateContext();
        context.Conversations.Add(_conversation);
        context.SaveChanges();
        _messages = new ConversationMessageStore(_temp.CreateContext());
        _sessionState.Set(Guid.NewGuid());
    }

    [Fact]
    public async Task ToolCallRoundTrip_DispatchesStreamsResultAndFinishes()
    {
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("Checking the queue. "),
            FakeAgentChatClient.ToolCallUpdate("c1", "planning_task_queue", """{"projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f70"}"""),
            FakeAgentChatClient.UsageUpdate(120)
        ],
        [
            FakeAgentChatClient.TextUpdate("The queue is clear."),
            FakeAgentChatClient.UsageUpdate(180)
        ]);
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000);

        var frames = await CollectAsync(loop);

        frames.Select(frame => frame.Kind).ShouldBe([
            ConversationStreamEventKind.Delta,
            ConversationStreamEventKind.ToolCall,
            ConversationStreamEventKind.Delta,
            ConversationStreamEventKind.Completed
        ]);
        frames[1].ToolName.ShouldBe("planning_task_queue");
        frames.Last().TokensUsed.ShouldBe(300);

        await _tools.Received(1).InvokeAsync(
            "planning_task_queue",
            Arg.Is<JsonElement>(args => args.GetProperty("projectId").GetString() == "019712af-8ea4-7aa1-9f5b-2f3c4d5e6f70"),
            Arg.Any<CancellationToken>());

        var secondRound = client.Requests[1];
        secondRound.Count.ShouldBe(4);
        secondRound[2].Role!.Value.ShouldBe("assistant");
        secondRound[2].Contents.OfType<FunctionCallContent>().Single().Name.ShouldBe("planning_task_queue");
        secondRound[3].Role!.Value.ShouldBe("tool");
        secondRound[3].Contents.OfType<FunctionResultContent>().Single().CallId.ShouldBe("c1");

        var history = await _messages.ListPageAsync(_conversation.Id, ct: CancellationToken.None);
        history.ShouldContain(row => row.Role == ConversationMessage.ToolRole &&
            row.Content.Contains("planning_task_queue"));
    }

    [Fact]
    public async Task PerTurnTokenCapExceeded_StopsLoopWithNotification()
    {
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("digging"),
            FakeAgentChatClient.ToolCallUpdate("c1", "planning_list_features", """{"projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f70"}"""),
            FakeAgentChatClient.UsageUpdate(9_000)
        ]);
        var loop = CreateLoop(client, perTurnMaxTokens: 8_192);

        var frames = await CollectAsync(loop);

        var failed = frames.Last();
        failed.Kind.ShouldBe(ConversationStreamEventKind.Failed);
        failed.Code.ShouldBe(AgentLoop.PerTurnCapCode);
        failed.Text!.ShouldContain("8192");

        await _notifier.Received(1).NotifyGuardAsync(
            Arg.Any<string>(),
            Arg.Is<string>(text => text.Contains("9000")),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
        frames.ShouldNotContain(frame => frame.Kind == ConversationStreamEventKind.Completed);
    }

    [Fact]
    public async Task MaxIterationsGuard_StopsARunawayLoop()
    {
        var client = new FakeAgentChatClient { AlwaysRequestTool = true };
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000_000);

        var frames = await CollectAsync(loop);

        var failed = frames.Last();
        failed.Kind.ShouldBe(ConversationStreamEventKind.Failed);
        failed.Code.ShouldBe(AgentLoop.MaxIterationsCode);
        client.RoundsServed.ShouldBe(AgentLoop.MaxIterations);

        await _notifier.Received(1).NotifyGuardAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DailyBudgetAlreadyExhausted_StopsBeforeAnyModelCall()
    {
        _budget.Used = 500;
        var client = new FakeAgentChatClient();
        var loop = CreateLoop(client, dailyBudget: 400);

        var frames = await CollectAsync(loop);

        var failed = frames.Single();
        failed.Kind.ShouldBe(ConversationStreamEventKind.Failed);
        failed.Code.ShouldBe(AgentLoop.DailyBudgetCode);
        client.RoundsServed.ShouldBe(0);

        await _notifier.Received(1).NotifyGuardAsync(
            Arg.Any<string>(), Arg.Is<string>(text => text.Contains("500")), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnconfiguredProvider_YieldsSetupHintWithoutModelCall()
    {
        var loop = CreateLoop(null, perTurnMaxTokens: 1_000);

        var frames = await CollectAsync(loop);

        frames.Last().Kind.ShouldBe(ConversationStreamEventKind.Completed);
        frames[0].Text.ShouldNotBeNullOrWhiteSpace();
    }

    private AgentLoop CreateLoop(IChatClient? client = null, int perTurnMaxTokens = 8_192, int dailyBudget = 0)
    {
        var settingsProvider = Substitute.For<IAgentRunSettingsProvider>();
        settingsProvider.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AgentRunSettings("overmind-agent", perTurnMaxTokens, dailyBudget));

        var prompts = Substitute.For<IAgentPromptComposer>();
        prompts.ComposeAsync(
                Arg.Any<Conversation>(),
                Arg.Any<IReadOnlyList<ConversationMessage>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => new List<ChatMessage>
            {
                new(ChatRole.System, "system prompt"),
                new(ChatRole.User, (string)callInfo[2]!)
            });

        var registrar = new EmbeddedAgentSessionRegistrar(
            new AgentLoopbackClient(new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1") }),
            _sessionState,
            settingsProvider);

        return new AgentLoop(
            StubFactory(client),
            prompts,
            _tools,
            settingsProvider,
            _budget,
            _notifier,
            _messages,
            registrar,
            NullLogger<AgentLoop>.Instance);
    }

    private ILlmChatClientFactory StubFactory(IChatClient? client)
    {
        var factory = Substitute.For<ILlmChatClientFactory>();
        factory.CreateAsync(Arg.Any<CancellationToken>()).Returns(client);
        return factory;
    }

    private async Task<List<ConversationStreamEvent>> CollectAsync(AgentLoop loop)
    {
        var frames = new List<ConversationStreamEvent>();
        await foreach (var frame in loop.RunAsync(_conversation, [], "sort the board", CancellationToken.None))
        {
            frames.Add(frame);
        }

        return frames;
    }

    public void Dispose() => _temp.Dispose();

    private sealed class FakeBudgetStore : IAgentBudgetStore
    {
        public long Used { get; set; }

        public Task<long> GetUsedAsync(DateOnly day, CancellationToken ct) => Task.FromResult(Used);

        public Task<long> AddUsageAsync(DateOnly day, long tokens, CancellationToken ct)
        {
            Used += tokens;
            return Task.FromResult(Used);
        }
    }
}
