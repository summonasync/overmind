using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Agent.Tools;
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
    private readonly IAgentRouteStore _routeStore = new AgentRouteMemoryStore();
    private readonly AgentRouteRecorder _routes;
    private readonly Conversation _conversation;
    private readonly ConversationMessageStore _messages;
    private bool _delegationEnabled;

    public AgentLoopTests()
    {
        _tools.CreateTools(Arg.Any<bool>()).Returns([]);
        _tools
            .InvokeAsync(
                Arg.Any<string>(), Arg.Any<JsonElement>(), Arg.Any<AgentToolInvocation>(), Arg.Any<CancellationToken>())
            .Returns(call => JsonSerializer.SerializeToElement(new { ok = true }));
        _conversation = Conversation.Create(null, "loop tests");
        using var context = _temp.CreateContext();
        context.Conversations.Add(_conversation);
        context.SaveChanges();
        _messages = new ConversationMessageStore(_temp.CreateContext());
        _routes = new AgentRouteRecorder(_routeStore);
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
            Arg.Any<AgentToolInvocation>(),
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

    [Fact]
    public async Task DelegationArmed_ResolvesOwnerInstructionAndArmsToolCalls()
    {
        var instruction = await _messages.AppendAsync(
            _conversation.Id, ConversationMessage.UserRole, "approve spec of 007", CancellationToken.None);
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("Validating. "),
            FakeAgentChatClient.ToolCallUpdate("c1", "planning_validate", """{"artifactId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f70"}"""),
            FakeAgentChatClient.UsageUpdate(50)
        ],
        [
            FakeAgentChatClient.TextUpdate("Validated."),
            FakeAgentChatClient.UsageUpdate(10)
        ]);
        _delegationEnabled = true;
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000);

        await CollectAsync(loop);

        await _tools.Received(1).InvokeAsync(
            "planning_validate",
            Arg.Any<JsonElement>(),
            Arg.Is<AgentToolInvocation>(invocation =>
                invocation.DelegationArmed && invocation.InstructionMessageId == instruction.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DailyBudget_CountsCompletionTokensOnly()
    {
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("Checking the board. "),
            FakeAgentChatClient.ToolCallUpdate("c1", "planning_list_features", """{"projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f70"}"""),
            FakeAgentChatClient.UsageUpdate(60_000, 1_500)
        ],
        [
            FakeAgentChatClient.TextUpdate("Done."),
            FakeAgentChatClient.UsageUpdate(65_000, 800)
        ]);
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000_000);

        await CollectAsync(loop);

        _budget.Used.ShouldBe(2_300);
    }

    [Fact]
    public async Task DailyBudget_TripsOnOutputWhileTotalStaysUnderTheCap()
    {
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("Checking the board. "),
            FakeAgentChatClient.ToolCallUpdate("c1", "planning_list_features", """{"projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f70"}"""),
            FakeAgentChatClient.UsageUpdate(60_000, 1_500)
        ],
        [
            FakeAgentChatClient.TextUpdate("Wrapping up."),
            FakeAgentChatClient.UsageUpdate(65_000, 800)
        ]);
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000_000, dailyBudget: 2_000);

        var frames = await CollectAsync(loop);

        var failed = frames.Last();
        failed.Kind.ShouldBe(ConversationStreamEventKind.Failed);
        failed.Code.ShouldBe(AgentLoop.DailyBudgetCode);
        failed.Text!.ShouldContain("2300");
        client.RoundsServed.ShouldBe(2);
    }

    [Fact]
    public async Task PerTurnCap_StillCountsTotalTokensWhileDailyStaysOnOutput()
    {
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("Reading the whole board."),
            FakeAgentChatClient.UsageUpdate(100_000, 500)
        ]);
        var loop = CreateLoop(client, perTurnMaxTokens: 8_192);

        var frames = await CollectAsync(loop);

        var failed = frames.Last();
        failed.Kind.ShouldBe(ConversationStreamEventKind.Failed);
        failed.Code.ShouldBe(AgentLoop.PerTurnCapCode);
        _budget.Used.ShouldBe(500);
    }

    [Fact]
    public async Task UsageWithoutSplitFields_KeepsCountingTowardTheDailyBudget()
    {
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("Checking."),
            FakeAgentChatClient.UsageUpdate(4_000)
        ]);
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000_000);

        await CollectAsync(loop);

        _budget.Used.ShouldBe(4_000);
    }

    [Fact]
    public async Task GuardNotificationFailure_StillYieldsFailedFrameAndPersistsUsage()
    {
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("digging"),
            FakeAgentChatClient.ToolCallUpdate("c1", "planning_list_features", """{"projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f70"}"""),
            FakeAgentChatClient.UsageUpdate(9_000)
        ]);
        _notifier
            .NotifyGuardAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("notification channel down")));
        var loop = CreateLoop(client, perTurnMaxTokens: 8_192, dailyBudget: 10_000);

        var frames = await CollectAsync(loop);

        var failed = frames.Last();
        failed.Kind.ShouldBe(ConversationStreamEventKind.Failed);
        failed.Code.ShouldBe(AgentLoop.PerTurnCapCode);
        _budget.Used.ShouldBe(9_000);
    }

    [Fact]
    public async Task LongConversation_TurnsOnWindowedContextAndKeepsStorageIntact()
    {
        for (var turn = 1; turn <= 30; turn++)
        {
            await _messages.AppendAsync(
                _conversation.Id, ConversationMessage.UserRole, $"user {turn}", CancellationToken.None);
            await _messages.AppendAsync(
                _conversation.Id, ConversationMessage.AssistantRole, $"assistant {turn}", CancellationToken.None);
        }

        var feed = await _messages.ListPageAsync(_conversation.Id, ct: CancellationToken.None);
        feed.Count.ShouldBe(ConversationMessageStore.DefaultHistoryLimit);

        var composed = new List<IReadOnlyList<ConversationMessage>>();
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("Answered from the window."),
            FakeAgentChatClient.UsageUpdate(40)
        ]);
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000, onCompose: composed.Add);

        var frames = await CollectAsync(loop, feed);

        frames.Last().Kind.ShouldBe(ConversationStreamEventKind.Completed);
        var window = composed.Single();
        window.Count.ShouldBe(ConversationContextWindow.MaxMessages);
        var expected = new List<string>();
        for (var turn = 21; turn <= 30; turn++)
        {
            expected.Add($"user {turn}");
            expected.Add($"assistant {turn}");
        }

        window.Select(message => message.Content).ShouldBe(expected);
        window.ShouldNotContain(message => message.Content == "sort the board");

        var stored = await _messages.ListPageAsync(
            _conversation.Id, limit: ConversationMessageStore.MaxHistoryLimit, ct: CancellationToken.None);
        stored.Count.ShouldBe(60);
    }

    [Fact]
    public async Task ToolRounds_LogPerRoundUsageDeltas()
    {
        var logger = new CapturingLogger<AgentLoop>();
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("Checking the queue. "),
            FakeAgentChatClient.ToolCallUpdate("c1", "planning_task_queue", """{"projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f70"}"""),
            FakeAgentChatClient.UsageUpdate(60_000, 1_500)
        ],
        [
            FakeAgentChatClient.TextUpdate("The queue is clear."),
            FakeAgentChatClient.UsageUpdate(65_000, 800)
        ]);
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000_000, logger: logger);

        await CollectAsync(loop);

        var rounds = logger.Entries
            .Select(entry => entry.Values.ToDictionary(pair => pair.Key, pair => pair.Value))
            .Where(values => values.ContainsKey("Round"))
            .ToList();
        rounds.Count.ShouldBe(2);

        rounds[0]["Round"].ShouldBe(1);
        rounds[0]["ConversationId"].ShouldBe(_conversation.Id);
        Convert.ToInt64(rounds[0]["InputTokens"]).ShouldBe(60_000);
        Convert.ToInt64(rounds[0]["OutputTokens"]).ShouldBe(1_500);
        Convert.ToInt64(rounds[0]["TotalTokens"]).ShouldBe(61_500);

        rounds[1]["Round"].ShouldBe(2);
        rounds[1]["ConversationId"].ShouldBe(_conversation.Id);
        Convert.ToInt64(rounds[1]["InputTokens"]).ShouldBe(65_000);
        Convert.ToInt64(rounds[1]["OutputTokens"]).ShouldBe(800);
        Convert.ToInt64(rounds[1]["TotalTokens"]).ShouldBe(65_800);
    }

    [Fact]
    public async Task TurnStart_LogsPromptAndToolSchemaSizes()
    {
        var logger = new CapturingLogger<AgentLoop>();
        const string toolSchema = """{"type":"object","properties":{"projectId":{"type":"string"}}}""";
        _tools.CreateTools(Arg.Any<bool>()).Returns(
        [
            new AgentToolFunction(new AgentToolSpec(
                "test_tool",
                "Does test things",
                toolSchema,
                (_, _) => Task.FromResult(JsonSerializer.SerializeToElement(new { ok = true }))))
        ]);
        await _messages.AppendAsync(
            _conversation.Id, ConversationMessage.UserRole, "what moved on the board", CancellationToken.None);
        await _messages.AppendAsync(
            _conversation.Id, ConversationMessage.AssistantRole, "two features moved", CancellationToken.None);
        var feed = await _messages.ListPageAsync(_conversation.Id, ct: CancellationToken.None);
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("Done."),
            FakeAgentChatClient.UsageUpdate(10)
        ]);
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000, logger: logger);

        await CollectAsync(loop, feed);

        var compose = logger.Entries.Single(entry => entry.Values.Any(pair => pair.Key == "SystemPromptChars"));
        var values = compose.Values.ToDictionary(pair => pair.Key, pair => pair.Value);
        values["ConversationId"].ShouldBe(_conversation.Id);
        Convert.ToInt64(values["SystemPromptChars"]).ShouldBe("system prompt".Length);
        Convert.ToInt64(values["HistoryChars"]).ShouldBe(feed.Sum(row => row.Content.Length));
        Convert.ToInt64(values["ToolSchemaChars"]).ShouldBe(
            "test_tool".Length + "Does test things".Length +
            JsonDocument.Parse(toolSchema).RootElement.GetRawText().Length);
    }

    [Fact]
    public async Task Telemetry_LogsNoConversationOrToolContent()
    {
        var logger = new CapturingLogger<AgentLoop>();
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
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000, logger: logger);

        await CollectAsync(loop);

        logger.Entries.ShouldNotBeEmpty();
        foreach (var entry in logger.Entries)
        {
            var text = entry.Message + " " + string.Join(" ", entry.Values.Select(pair => $"{pair.Key}={pair.Value}"));
            foreach (var content in new[]
                     {
                         "sort the board", "Checking the queue", "The queue is clear",
                         "planning_task_queue", "019712af"
                     })
            {
                text.ShouldNotContain(content);
            }
        }
    }

    [Fact]
    public async Task BounceCall_EndsTurnWithVerdictAndRecordsTheDegradationRow()
    {
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("That one is not mine. "),
            FakeAgentChatClient.ToolCallUpdate("c1", "bounce_turn", """{"reason":"belongs to the planning specialist"}"""),
            FakeAgentChatClient.UsageUpdate(50)
        ]);
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000);

        var frames = await CollectAsync(loop);

        frames.Select(frame => frame.Kind).ShouldBe([
            ConversationStreamEventKind.Delta,
            ConversationStreamEventKind.ToolCall,
            ConversationStreamEventKind.Bounced,
            ConversationStreamEventKind.Completed
        ]);
        frames[1].ToolName.ShouldBe("bounce_turn");
        frames[2].Text.ShouldBe("belongs to the planning specialist");

        var rows = await _routeStore.HotHeadAsync(10, CancellationToken.None);
        var row = rows.Single();
        row.Pattern.ShouldBe("sort the board");
        row.Domain.ShouldBe(AgentRouteRecorder.UnknownDomain);
        row.MissReason.ShouldBe(AgentRouteRecorder.NotAnalyzedReason);
        row.HitCount.ShouldBe(1);
    }

    [Fact]
    public async Task RecordRouteCall_StoresAgentAnalyzedRowAndSkipsDegradation()
    {
        _tools
            .InvokeAsync(
                "record_route",
                Arg.Any<JsonElement>(),
                Arg.Any<AgentToolInvocation>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var invocation = callInfo.ArgAt<AgentToolInvocation>(2);
                await _routes.RecordAnalysisAsync(
                    invocation.ConversationId!.Value,
                    "board planning tasks",
                    "planning",
                    "no keywords in hot head",
                    callInfo.ArgAt<CancellationToken>(3));
                return JsonSerializer.SerializeToElement(new { recorded = true });
            });
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("Checking. "),
            FakeAgentChatClient.ToolCallUpdate(
                "c1",
                "record_route",
                """{"domain":"planning","missReason":"no keywords in hot head","suggestedPattern":"board planning tasks"}"""),
            FakeAgentChatClient.UsageUpdate(60)
        ],
        [
            FakeAgentChatClient.TextUpdate("Done."),
            FakeAgentChatClient.UsageUpdate(40)
        ]);
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000);

        var frames = await CollectAsync(loop);

        frames.Last().Kind.ShouldBe(ConversationStreamEventKind.Completed);
        var row = (await _routeStore.HotHeadAsync(10, CancellationToken.None)).Single();
        row.Pattern.ShouldBe("board planning tasks");
        row.Domain.ShouldBe("planning");
        row.MissReason.ShouldBe("no keywords in hot head");
        row.HitCount.ShouldBe(1);
        row.Status.ShouldBe(AgentRouteStatus.Provisional);
        row.Source.ShouldBe(AgentRouteSource.Learned);
        row.Pattern.ShouldNotContain("sort the board");
    }

    [Fact]
    public async Task WorkerTurnWithoutRecordRoute_RecordsUnanalyzedRow()
    {
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("Sorted."),
            FakeAgentChatClient.UsageUpdate(30)
        ]);
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000);

        await CollectAsync(loop);

        var row = (await _routeStore.HotHeadAsync(10, CancellationToken.None)).Single();
        row.Pattern.ShouldBe("sort the board");
        row.Domain.ShouldBe(AgentRouteRecorder.UnknownDomain);
        row.MissReason.ShouldBe(AgentRouteRecorder.NotAnalyzedReason);
    }

    [Fact]
    public async Task WorkerTurn_TelemetryCarriesWorkerRoleTag()
    {
        var logger = new CapturingLogger<AgentLoop>();
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("Sorted."),
            FakeAgentChatClient.UsageUpdate(30)
        ]);
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000, logger: logger);

        await CollectAsync(loop);

        logger.Entries.ShouldNotBeEmpty();
        foreach (var entry in logger.Entries)
        {
            var values = entry.Values.ToDictionary(pair => pair.Key, pair => pair.Value);
            if (values.ContainsKey("Round") || values.ContainsKey("SystemPromptChars"))
            {
                values["Role"].ShouldBe("worker");
            }
        }
    }

    [Fact]
    public async Task RouterTurn_SkipsRouteRowsAndTagsRouterRole()
    {
        var logger = new CapturingLogger<AgentLoop>();
        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("Router chat answer."),
            FakeAgentChatClient.UsageUpdate(30)
        ]);
        var loop = CreateLoop(client, perTurnMaxTokens: 1_000, logger: logger);

        var frames = await CollectAsync(loop, role: AgentRole.Router);

        frames.Last().Kind.ShouldBe(ConversationStreamEventKind.Completed);
        (await _routeStore.HotHeadAsync(10, CancellationToken.None)).ShouldBeEmpty();
        var roles = logger.Entries
            .Select(entry => entry.Values.ToDictionary(pair => pair.Key, pair => pair.Value))
            .Where(values => values.ContainsKey("Role"))
            .Select(values => values["Role"])
            .ToList();
        roles.ShouldNotBeEmpty();
        roles.All(value => (string?)value == "router").ShouldBeTrue();
    }

    private AgentLoop CreateLoop(
        IChatClient? client = null,
        int perTurnMaxTokens = 8_192,
        int dailyBudget = 0,
        Action<IReadOnlyList<ConversationMessage>>? onCompose = null,
        ILogger<AgentLoop>? logger = null)
    {
        var settingsProvider = Substitute.For<IAgentRunSettingsProvider>();
        settingsProvider.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AgentRunSettings("overmind-agent", perTurnMaxTokens, dailyBudget));

        var prompts = Substitute.For<IAgentPromptComposer>();
        var compose = prompts.ComposeAsync(
                Arg.Any<Conversation>(),
                Arg.Any<IReadOnlyList<ConversationMessage>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var messages = new List<ChatMessage> { new(ChatRole.System, "system prompt") };
                messages.AddRange(callInfo.ArgAt<IReadOnlyList<ConversationMessage>>(1).Select(
                    row => new ChatMessage(
                        row.Role == ConversationMessage.UserRole ? ChatRole.User : ChatRole.Assistant,
                        row.Content)));
                messages.Add(new ChatMessage(ChatRole.User, (string)callInfo[2]!));
                return messages;
            });
        if (onCompose is not null)
        {
            compose.AndDoes(callInfo => onCompose(callInfo.ArgAt<IReadOnlyList<ConversationMessage>>(1)));
        }

        var registrar = new EmbeddedAgentSessionRegistrar(
            new AgentLoopbackClient(new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1") }),
            _sessionState,
            settingsProvider);

        var delegation = Substitute.For<IAgentDelegationGate>();
        delegation
            .IsEnabledAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => (Guid?)callInfo[0] == _conversation.ProjectId && _delegationEnabled);

        return new AgentLoop(
            StubFactory(client),
            prompts,
            _tools,
            settingsProvider,
            _budget,
            _notifier,
            _messages,
            registrar,
            delegation,
            _routes,
            logger ?? NullLogger<AgentLoop>.Instance);
    }

    private ILlmChatClientFactory StubFactory(IChatClient? client)
    {
        var factory = Substitute.For<ILlmChatClientFactory>();
        factory.CreateAsync(Arg.Any<CancellationToken>()).Returns(client);
        return factory;
    }

    private async Task<List<ConversationStreamEvent>> CollectAsync(
        AgentLoop loop,
        IReadOnlyList<ConversationMessage>? history = null,
        AgentRole role = AgentRole.Worker)
    {
        var frames = new List<ConversationStreamEvent>();
        await foreach (var frame in loop.RunAsync(
            _conversation, history ?? [], "sort the board", CancellationToken.None, role))
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
