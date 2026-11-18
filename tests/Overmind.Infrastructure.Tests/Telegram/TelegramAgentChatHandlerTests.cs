using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Tests.Agent;
using Overmind.Infrastructure.Llm;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramAgentChatHandlerTests : IDisposable
{
    private const long OwnerChatId = 42;
    private const string TestToken = "123456:TESTSECRETTOKEN";

    private readonly TempDatabase _temp = new();
    private readonly FakeTelegramApi _api = new();
    private readonly TelegramBotClientCache _cache;
    private readonly ConversationTurnGate _gate = new();

    public TelegramAgentChatHandlerTests()
    {
        _cache = new TelegramBotClientCache(new StubTelegramBotClientFactory(_api));
    }

    [Fact]
    public async Task RoundTrip_PersistsTurnAndDeliversStrippedReply()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var rig = await CreateHandlerAsync(new StubStreamSource(
        [
            ConversationStreamEvent.Delta("Hello **world**"),
            ConversationStreamEvent.Completed(30)
        ]));

        await rig.Handler.HandleAsync(new TelegramIncomingMessage(OwnerChatId, "privet"), CancellationToken.None);

        rig.Conversation.Title.ShouldBe(TelegramChatStore.ConversationTitle);
        rig.Conversation.ProjectId.ShouldBeNull();
        SentTexts().ShouldBe(["Hello world"]);

        await using var context = _temp.CreateContext();
        var roles = await context.ConversationMessages
            .Where(row => row.ConversationId == rig.Conversation.Id)
            .OrderBy(row => row.CreatedAt)
            .Select(row => row.Role)
            .ToListAsync();
        roles.ShouldBe([ConversationMessage.UserRole, ConversationMessage.AssistantRole]);
    }

    [Fact]
    public async Task RoundTrip_LearnedRoute_PrefixesTheReplyWithTheTraceLine()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var rig = await CreateHandlerAsync(new StubStreamSource(
        [
            ConversationStreamEvent.Routed(RoutingTargets.Worker, RoutingSource.Learned),
            ConversationStreamEvent.Delta("Queue is clear."),
            ConversationStreamEvent.Completed(30)
        ]));

        await rig.Handler.HandleAsync(new TelegramIncomingMessage(OwnerChatId, "privet"), CancellationToken.None);

        SentTexts().ShouldBe(["→ worker · learned\n\nQueue is clear."]);
    }

    [Fact]
    public async Task RoundTrip_ClassifiedRoute_PrefixesTheReplyWithTheTargetOnly()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var rig = await CreateHandlerAsync(new StubStreamSource(
        [
            ConversationStreamEvent.Routed(RoutingTargets.Chat, RoutingSource.Classification),
            ConversationStreamEvent.Delta("Hello there."),
            ConversationStreamEvent.Completed(30)
        ]));

        await rig.Handler.HandleAsync(new TelegramIncomingMessage(OwnerChatId, "privet"), CancellationToken.None);

        SentTexts().ShouldBe(["→ chat\n\nHello there."]);
    }

    [Fact]
    public async Task NotificationReply_DoesNotTouchTheStreamSource()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var stream = new StubStreamSource([[]]);
        var notifications = new NotificationsService(
            _temp.CreateContext(),
            new ReplyAutoActFanout(_temp.CreateContext(), new ReplyAutoActQueue(), NullLogger<ReplyAutoActFanout>.Instance),
            TestWakeFanout.Create(_temp).Fanout,
            []);
        var rig = await CreateHandlerAsync(stream, notifications);
        var notification = AgentNotification.Create(NotificationKind.Approval, "approve me", null, null, null);
        await using (var context = _temp.CreateContext())
        {
            context.AgentNotifications.Add(notification);
            await context.SaveChangesAsync();
            await new TelegramNotificationDeliveryStore(context).SaveAsync(OwnerChatId, 9, notification.Id);
        }

        await rig.Handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "approved", RepliedToMessageId: 9), CancellationToken.None);

        stream.Invocations.ShouldBe(0);
        SentTexts().ShouldBe([TelegramAgentChatHandler.ReplyRegisteredAck]);
    }

    [Fact]
    public async Task RoundTrip_ReusesTheMappedConversationOnTheNextMessage()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var stream = new StubStreamSource(
        [
            [ConversationStreamEvent.Delta("first"), ConversationStreamEvent.Completed(10)],
            [ConversationStreamEvent.Delta("second"), ConversationStreamEvent.Completed(10)]
        ]);
        var rig = await CreateHandlerAsync(stream);

        await rig.Handler.HandleAsync(new TelegramIncomingMessage(OwnerChatId, "one"), CancellationToken.None);
        await rig.Handler.HandleAsync(new TelegramIncomingMessage(OwnerChatId, "two"), CancellationToken.None);

        await using var context = _temp.CreateContext();
        context.Conversations.Count().ShouldBe(1);
        context.TelegramChatMappings.Count().ShouldBe(1);
        context.ConversationMessages.Count(row => row.Role == ConversationMessage.UserRole).ShouldBe(2);
        SentTexts().ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task BusyConversation_RefusesHonestlyWithoutRunningTheTurn()
    {
        var rig = await CreateHandlerAsync(new StubStreamSource(
        [
            [ConversationStreamEvent.Delta("never"), ConversationStreamEvent.Completed(0)]
        ]));
        _gate.Enter(rig.Conversation.Id);

        await rig.Handler.HandleAsync(new TelegramIncomingMessage(OwnerChatId, "while busy"), CancellationToken.None);

        ((StubStreamSource)rig.Stream).Invocations.ShouldBe(0);
        SentTexts().ShouldBe([TelegramAgentChatHandler.BusyReplyText]);

        await using var context = _temp.CreateContext();
        context.ConversationMessages.Count(row => row.ConversationId == rig.Conversation.Id).ShouldBe(0);
    }

    [Fact]
    public async Task WaveCommand_TurnAlreadyBusy_StillDispatchesWithoutEnteringTheGate()
    {
        var waveCommands = Substitute.For<ITelegramWaveCommandService>();
        var stream = new StubStreamSource([[]]);
        var rig = await CreateHandlerAsync(stream, waveCommands: waveCommands);
        _gate.Enter(rig.Conversation.Id);

        await rig.Handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "/wave_snapshot"), CancellationToken.None);

        await waveCommands.Received(1).HandleAsync(
            TelegramWaveCommand.WaveSnapshot,
            Arg.Is<TelegramIncomingMessage>(seeded => seeded.ChatId == OwnerChatId),
            Arg.Any<CancellationToken>());
        _gate.IsBusy(rig.Conversation.Id).ShouldBeTrue();
        stream.Invocations.ShouldBe(0);
        SentTexts().ShouldNotContain(TelegramAgentChatHandler.BusyReplyText);

        await using var context = _temp.CreateContext();
        context.ConversationMessages.Count(row => row.ConversationId == rig.Conversation.Id).ShouldBe(0);
    }

    [Fact]
    public async Task WaveCommand_UnmappedChat_CreatesNoConversation()
    {
        const long unmappedChatId = 77;
        var waveCommands = Substitute.For<ITelegramWaveCommandService>();
        var stream = new StubStreamSource([[]]);
        var rig = await CreateHandlerAsync(stream, waveCommands: waveCommands);

        await rig.Handler.HandleAsync(
            new TelegramIncomingMessage(unmappedChatId, "/send_to_wave"), CancellationToken.None);

        await waveCommands.Received(1).HandleAsync(
            TelegramWaveCommand.SendToWave,
            Arg.Is<TelegramIncomingMessage>(seeded => seeded.ChatId == unmappedChatId),
            Arg.Any<CancellationToken>());
        stream.Invocations.ShouldBe(0);

        await using var context = _temp.CreateContext();
        context.TelegramChatMappings.Count(m => m.ChatId == unmappedChatId).ShouldBe(0);
        context.Conversations.Count().ShouldBe(1);
    }

    [Fact]
    public async Task WaveCommand_ReplyingToANotification_StaysOnTheReplyDoor()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var waveCommands = Substitute.For<ITelegramWaveCommandService>();
        var rig = await CreateHandlerAsync(
            new StubStreamSource([[]]), CreateWorkingNotificationsService(), waveCommands: waveCommands);
        var notification = AgentNotification.Create(NotificationKind.Approval, "approve me", null, null, null);
        await using (var context = _temp.CreateContext())
        {
            context.AgentNotifications.Add(notification);
            await context.SaveChangesAsync();
            await new TelegramNotificationDeliveryStore(context).SaveAsync(OwnerChatId, 21, notification.Id);
        }

        await rig.Handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "/wave_snapshot", RepliedToMessageId: 21),
            CancellationToken.None);

        await waveCommands.DidNotReceive().HandleAsync(
            Arg.Any<TelegramWaveCommand>(), Arg.Any<TelegramIncomingMessage>(), Arg.Any<CancellationToken>());
        SentTexts().ShouldBe([TelegramAgentChatHandler.ReplyRegisteredAck]);
    }

    [Fact]
    public async Task UnrecognizedSlashCommand_FallsThroughToTheAgentTurn()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var waveCommands = Substitute.For<ITelegramWaveCommandService>();
        var rig = await CreateHandlerAsync(
            new StubStreamSource(
            [
                [ConversationStreamEvent.Delta("that is not a command I know"), ConversationStreamEvent.Completed(10)]
            ]),
            waveCommands: waveCommands);

        await rig.Handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "/wave_snapshot now"), CancellationToken.None);

        await waveCommands.DidNotReceive().HandleAsync(
            Arg.Any<TelegramWaveCommand>(), Arg.Any<TelegramIncomingMessage>(), Arg.Any<CancellationToken>());
        SentTexts().ShouldBe(["that is not a command I know"]);
    }

    [Fact]
    public async Task OrdinaryMessage_StaysOffTheWaveCommandDoor()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var waveCommands = Substitute.For<ITelegramWaveCommandService>();
        var rig = await CreateHandlerAsync(
            new StubStreamSource(
            [
                [ConversationStreamEvent.Delta("hello back"), ConversationStreamEvent.Completed(10)]
            ]),
            waveCommands: waveCommands);

        await rig.Handler.HandleAsync(new TelegramIncomingMessage(OwnerChatId, "privet"), CancellationToken.None);

        await waveCommands.DidNotReceive().HandleAsync(
            Arg.Any<TelegramWaveCommand>(), Arg.Any<TelegramIncomingMessage>(), Arg.Any<CancellationToken>());
        SentTexts().ShouldBe(["hello back"]);
    }

    [Fact]
    public async Task NotificationReplyDoor_KeepsPriorityOverTheWaveDraftCapture()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var waveCommands = Substitute.For<ITelegramWaveCommandService>();
        waveCommands.TryHandleDraftReplyAsync(Arg.Any<TelegramIncomingMessage>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var rig = await CreateHandlerAsync(
            new StubStreamSource([[]]), CreateWorkingNotificationsService(), waveCommands: waveCommands);
        var notification = AgentNotification.Create(NotificationKind.Approval, "approve me", null, null, null);
        await using (var context = _temp.CreateContext())
        {
            context.AgentNotifications.Add(notification);
            await context.SaveChangesAsync();
            await new TelegramNotificationDeliveryStore(context).SaveAsync(OwnerChatId, 21, notification.Id);
        }

        await rig.Handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "the wave signal I meant", RepliedToMessageId: 21),
            CancellationToken.None);

        SentTexts().ShouldBe([TelegramAgentChatHandler.ReplyRegisteredAck]);
        await waveCommands.DidNotReceive().TryHandleDraftReplyAsync(
            Arg.Any<TelegramIncomingMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaveDraftCapture_KeepsTheMessageOffTheAgentTurn()
    {
        var waveCommands = Substitute.For<ITelegramWaveCommandService>();
        waveCommands.TryHandleDraftReplyAsync(Arg.Any<TelegramIncomingMessage>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var stream = new StubStreamSource([[]]);
        var rig = await CreateHandlerAsync(stream, waveCommands: waveCommands);

        await rig.Handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "posted to the lane", RepliedToMessageId: 21),
            CancellationToken.None);

        stream.Invocations.ShouldBe(0);
        _api.RequestCount.ShouldBe(0);
        await using var context = _temp.CreateContext();
        context.ConversationMessages.Count(row => row.ConversationId == rig.Conversation.Id).ShouldBe(0);
    }

    [Fact]
    public async Task ReplyToAnUnclaimedMessage_FallsThroughToTheAgentTurn()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var waveCommands = Substitute.For<ITelegramWaveCommandService>();
        var rig = await CreateHandlerAsync(
            new StubStreamSource(
            [
                [ConversationStreamEvent.Delta("ordinary reply"), ConversationStreamEvent.Completed(10)]
            ]),
            waveCommands: waveCommands);

        await rig.Handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "privet", RepliedToMessageId: 4_004),
            CancellationToken.None);

        await waveCommands.Received(1).TryHandleDraftReplyAsync(
            Arg.Any<TelegramIncomingMessage>(), Arg.Any<CancellationToken>());
        SentTexts().ShouldBe(["ordinary reply"]);
    }

    [Fact]
    public async Task HandlerFailure_RetriesUpToCapThenDeadLetters()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var stream = new StubStreamSource([[]]) { AlwaysThrow = true };
        var rig = await CreateHandlerAsync(stream);

        await rig.Handler.HandleAsync(new TelegramIncomingMessage(OwnerChatId, "poison"), CancellationToken.None);

        stream.Invocations.ShouldBe(TelegramAgentChatHandler.MaxAttempts);
        rig.LogSnapshot().ShouldContain(line => line.StartsWith("Error:") && line.Contains("dead-lettered"));
        SentTexts().ShouldHaveSingleItem();
        SentTexts()[0].ShouldContain("failed 3 times in a row");
    }

    [Fact]
    public async Task HandlerFailure_RetryRecoversWithoutDuplicatingTheUserMessage()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        ConversationStreamEvent[][] rounds =
        [
            [],
            [ConversationStreamEvent.Delta("recovered"), ConversationStreamEvent.Completed(15)]
        ];
        var stream = new StubStreamSource(rounds) { ThrowFirstInvocations = 1 };
        var rig = await CreateHandlerAsync(stream);

        await rig.Handler.HandleAsync(new TelegramIncomingMessage(OwnerChatId, "flaky"), CancellationToken.None);

        stream.Invocations.ShouldBe(2);
        SentTexts().ShouldBe(["recovered"]);

        await using var context = _temp.CreateContext();
        context.ConversationMessages.Count(row => row.Role == ConversationMessage.UserRole).ShouldBe(1);
        context.ConversationMessages.Count(row => row.Role == ConversationMessage.AssistantRole).ShouldBe(1);
    }

    [Fact]
    public async Task PartialDelivery_StopsWithoutRetryingTheTurn()
    {
        var firstChunk = new string('a', 3_000);
        var secondChunk = new string('b', 3_000);
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        _api.QueueResponse("""{"ok":false,"error_code":500,"description":"delivery broke"}""");
        var stream = new StubStreamSource(
        [
            [ConversationStreamEvent.Delta($"{firstChunk}\n\n{secondChunk}"), ConversationStreamEvent.Completed(10)]
        ]);
        var rig = await CreateHandlerAsync(stream);

        await rig.Handler.HandleAsync(new TelegramIncomingMessage(OwnerChatId, "split me"), CancellationToken.None);

        stream.Invocations.ShouldBe(1);
        _api.RequestCount.ShouldBe(2);
        rig.LogSnapshot().ShouldContain(line => line.StartsWith("Error:") && line.Contains("partially delivered"));
    }

    [Fact]
    public async Task RefusalFrame_IsDeliveredHonestlyWithoutRetry()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var stream = new StubStreamSource(
        [
            [ConversationStreamEvent.Failed("Daily budget exhausted: 9000 of 8000 tokens used today.")]
        ]);
        var rig = await CreateHandlerAsync(stream);

        await rig.Handler.HandleAsync(new TelegramIncomingMessage(OwnerChatId, "hello"), CancellationToken.None);

        stream.Invocations.ShouldBe(1);
        SentTexts().ShouldBe(["Daily budget exhausted: 9000 of 8000 tokens used today."]);

        await using var context = _temp.CreateContext();
        context.ConversationMessages.Count(row => row.Role == ConversationMessage.AssistantRole).ShouldBe(0);
    }

    [Fact]
    public async Task RoundTrip_CountsTurnUsageInTheSharedBudget()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var (streamSource, budget) = CreateLoopBackedSource();
        var rig = await CreateHandlerAsync(streamSource);

        await rig.Handler.HandleAsync(new TelegramIncomingMessage(OwnerChatId, "count me"), CancellationToken.None);

        budget.Used.ShouldBe(120);
        SentTexts().ShouldBe(["telegram reply body"]);
    }

    [Fact]
    public async Task NotificationReply_RecordsProofAndHandsTheNotificationToTheValidator()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var validator = Substitute.For<INotificationValidatorRunner>();
        var notifications = CreateWorkingNotificationsService();
        var rig = await CreateHandlerAsync(new StubStreamSource([[]]), notifications, validator);
        var notification = AgentNotification.Create(NotificationKind.Approval, "approve me", null, null, null);
        await using (var context = _temp.CreateContext())
        {
            context.AgentNotifications.Add(notification);
            await context.SaveChangesAsync();
            await new TelegramNotificationDeliveryStore(context).SaveAsync(OwnerChatId, 11, notification.Id);
        }

        await rig.Handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "lgtm approve it", RepliedToMessageId: 11), CancellationToken.None);

        SentTexts().ShouldContain(TelegramAgentChatHandler.ReplyRegisteredAck);
        await validator.Received(1).RunAsync(
            OwnerChatId,
            Arg.Is<AgentNotification>(seeded => seeded.Id == notification.Id),
            "lgtm approve it",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationReply_RegisterFailure_SkipsTheValidator()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var validator = Substitute.For<INotificationValidatorRunner>();
        var rig = await CreateHandlerAsync(new StubStreamSource([[]]), CreateNotificationsService(), validator);
        var notification = AgentNotification.Create(NotificationKind.Approval, "approve me", null, null, null);
        await using (var context = _temp.CreateContext())
        {
            context.AgentNotifications.Add(notification);
            await context.SaveChangesAsync();
            await new TelegramNotificationDeliveryStore(context).SaveAsync(OwnerChatId, 12, notification.Id);
        }

        await rig.Handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "approve", RepliedToMessageId: 12), CancellationToken.None);

        await validator.DidNotReceive().RunAsync(
            Arg.Any<long>(), Arg.Any<AgentNotification>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationReply_RegisterFailure_RetriesUpToCapThenDeadLetters()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var notifications = CreateNotificationsService();
        var rig = await CreateHandlerAsync(new StubStreamSource([[]]), notifications);
        var notification = AgentNotification.Create(NotificationKind.Approval, "approve me", null, null, null);
        await using (var context = _temp.CreateContext())
        {
            context.AgentNotifications.Add(notification);
            await context.SaveChangesAsync();
            await new TelegramNotificationDeliveryStore(context).SaveAsync(OwnerChatId, 5, notification.Id);
        }

        await rig.Handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "approve", RepliedToMessageId: 5), CancellationToken.None);

        rig.LogSnapshot().ShouldContain(line => line.Contains("failed on attempt 1; retrying"));
        rig.LogSnapshot().ShouldContain(line => line.StartsWith("Error:") && line.Contains("dead-lettered"));
        SentTexts().ShouldBe([TelegramAgentChatHandler.ReplyFailedNotice]);
    }

    [Fact]
    public async Task FullDeliveryFailure_RetriesWithoutLeavingAStaleAssistantRow()
    {
        var firstChunk = new string('a', 3_000);
        var secondChunk = new string('b', 3_000);
        _api.QueueResponse("""{"ok":false,"error_code":500,"description":"first chunk broke"}""");
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var stream = new StubStreamSource(
        [
            [ConversationStreamEvent.Delta($"{firstChunk}\n\n{secondChunk}"), ConversationStreamEvent.Completed(10)]
        ]);
        var rig = await CreateHandlerAsync(stream);

        await rig.Handler.HandleAsync(new TelegramIncomingMessage(OwnerChatId, "split me"), CancellationToken.None);

        stream.Invocations.ShouldBe(2);
        _api.RequestCount.ShouldBe(3);
        await using var context = _temp.CreateContext();
        context.ConversationMessages.Count(row => row.ConversationId == rig.Conversation.Id
            && row.Role == ConversationMessage.AssistantRole).ShouldBe(1);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _temp.Dispose();
    }

    private (IConversationStreamSource Source, CountingBudgetStore Budget) CreateLoopBackedSource()
    {
        var settingsProvider = Substitute.For<IAgentRunSettingsProvider>();
        settingsProvider.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AgentRunSettings("overmind-agent", 8_192, 0));

        var prompts = Substitute.For<IAgentPromptComposer>();
        prompts
            .ComposeAsync(
                Arg.Any<Conversation>(),
                Arg.Any<IReadOnlyList<ConversationMessage>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => new List<ChatMessage>
            {
                new(ChatRole.System, "system prompt"),
                new(ChatRole.User, (string)callInfo[2]!)
            });

        var tools = Substitute.For<IAgentToolInvoker>();
        tools.CreateTools(Arg.Any<bool>()).Returns([]);
        tools
            .InvokeAsync(
                Arg.Any<string>(), Arg.Any<JsonElement>(), Arg.Any<AgentToolInvocation>(), Arg.Any<CancellationToken>())
            .Returns(call => JsonSerializer.SerializeToElement(new { ok = true }));

        var notifier = Substitute.For<IAgentGuardNotifier>();
        var delegation = Substitute.For<IAgentDelegationGate>();
        delegation.IsEnabledAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);

        var sessionState = new EmbeddedAgentSessionState();
        sessionState.Set(Guid.NewGuid());
        var registrar = new EmbeddedAgentSessionRegistrar(
            new AgentLoopbackClient(new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1") }),
            sessionState,
            settingsProvider);

        var client = new FakeAgentChatClient(
        [
            FakeAgentChatClient.TextUpdate("telegram reply body"),
            FakeAgentChatClient.UsageUpdate(120)
        ]);
        var chatFactory = Substitute.For<ILlmChatClientFactory>();
        chatFactory.CreateAsync(Arg.Any<CancellationToken>()).Returns(client);

        var budget = new CountingBudgetStore();
        var loop = new AgentLoop(
            chatFactory,
            prompts,
            tools,
            settingsProvider,
            budget,
            notifier,
            new ConversationMessageStore(_temp.CreateContext()),
            registrar,
            delegation,
            new AgentRouteRecorder(new AgentRouteMemoryStore()),
            NullLogger<AgentLoop>.Instance);

        return (new LlmConversationStreamSource(loop), budget);
    }

    private async Task<HandlerRig> CreateHandlerAsync(
        IConversationStreamSource streamSource,
        NotificationsService? notificationsService = null,
        INotificationValidatorRunner? validator = null,
        ITelegramWaveCommandService? waveCommands = null)
    {
        await ConfigureTransportAsync();
        var chats = new TelegramChatStore(_temp.CreateContext());
        var conversation = await chats.GetOrCreateConversationAsync(OwnerChatId, CancellationToken.None);
        var logs = new List<string>();
        var handler = new TelegramAgentChatHandler(
            new TelegramChatStore(_temp.CreateContext()),
            new ConversationMessageStore(_temp.CreateContext()),
            streamSource,
            _gate,
            new TelegramReplySender(
                _cache,
                new TelegramTransportOptionsProvider(new AppSettingsService(_temp.CreateContext()))),
            new TelegramNotificationDeliveryStore(_temp.CreateContext()),
            notificationsService ?? CreateNotificationsService(),
            validator ?? Substitute.For<INotificationValidatorRunner>(),
            waveCommands ?? Substitute.For<ITelegramWaveCommandService>(),
            new CollectingLogger<TelegramAgentChatHandler>(logs));

        return new HandlerRig(handler, streamSource, conversation, logs);
    }

    private NotificationsService CreateNotificationsService()
    {
        var broken = _temp.CreateContext();
        broken.Dispose();
        return new NotificationsService(
            broken,
            new ReplyAutoActFanout(
                _temp.CreateContext(), new ReplyAutoActQueue(), NullLogger<ReplyAutoActFanout>.Instance),
            TestWakeFanout.Create(_temp).Fanout,
            []);
    }

    private NotificationsService CreateWorkingNotificationsService()
    {
        return new NotificationsService(
            _temp.CreateContext(),
            new ReplyAutoActFanout(
                _temp.CreateContext(), new ReplyAutoActQueue(), NullLogger<ReplyAutoActFanout>.Instance),
            TestWakeFanout.Create(_temp).Fanout,
            []);
    }

    private async Task ConfigureTransportAsync()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.TelegramEnabled, "true", CancellationToken.None);
        await settings.SetAsync(SettingKeys.TelegramAllowedChatIds, "42", CancellationToken.None);
        await settings.SetAsync(SettingKeys.TelegramBotToken, TestToken, CancellationToken.None);
    }

    private string[] SentTexts()
    {
        lock (_api)
        {
            return [.. _api.Requests.Select(SentText)];
        }
    }

    private static string SentText(FakeTelegramApi.Captured captured)
    {
        using var document = JsonDocument.Parse(captured.Body);
        return document.RootElement.TryGetProperty("text", out var text)
            ? text.GetString() ?? string.Empty
            : string.Empty;
    }

    private sealed record HandlerRig(
        TelegramAgentChatHandler Handler,
        IConversationStreamSource Stream,
        Conversation Conversation,
        List<string> Logs)
    {
        public string[] LogSnapshot()
        {
            lock (Logs)
            {
                return [.. Logs];
            }
        }
    }

    private sealed class CountingBudgetStore : IAgentBudgetStore
    {
        public long Used { get; private set; }

        public Task<long> GetUsedAsync(DateOnly day, CancellationToken ct) => Task.FromResult(Used);

        public Task<long> AddUsageAsync(DateOnly day, long tokens, CancellationToken ct)
        {
            Used += tokens;
            return Task.FromResult(Used);
        }
    }

    private sealed class StubStreamSource(params ConversationStreamEvent[][] turns) : IConversationStreamSource
    {
        public int Invocations { get; private set; }

        public bool AlwaysThrow { get; init; }

        public int ThrowFirstInvocations { get; init; }

        public async IAsyncEnumerable<ConversationStreamEvent> StreamAsync(
            Conversation conversation,
            IReadOnlyList<ConversationMessage> history,
            string userText,
            [EnumeratorCancellation] CancellationToken ct = default,
            RoutingMissContext? missContext = null)
        {
            Invocations++;
            if (AlwaysThrow || Invocations <= ThrowFirstInvocations)
            {
                throw new InvalidOperationException("the stream source failed on purpose");
            }

            var round = turns[Math.Min(Invocations - 1, turns.Length - 1)];
            foreach (var streamEvent in round)
            {
                await Task.Yield();
                yield return streamEvent;
            }
        }
    }
}
