using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Telegram;
using Overmind.Infrastructure.Wake;
using Overmind.Infrastructure.Wave;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramWaveSendFlowTests : IAsyncLifetime
{
    private const long OwnerChatId = 42;
    private const string TestToken = "123456:TESTSECRETTOKEN";
    private const string Signal = "lane is clear, @marshal read the snapshot";
    private const string CancelLabel = "✖️ Cancel";

    private readonly TempDatabase _temp = new();
    private readonly FakeTelegramApi _api = new();
    private readonly TelegramBotClientCache _cache;
    private readonly ServiceProvider _wakeServices;
    private readonly WakeDropFanout _wakeFanout;
    private readonly StubWaveChatBroadcaster _broadcaster = new();
    private readonly List<string> _logs = [];
    private readonly TelegramWaveDraftStore _drafts = new(TimeProvider.System);
    private readonly ITelegramWaveSnapshotService _snapshotDoor = Substitute.For<ITelegramWaveSnapshotService>();
    private readonly TelegramWaveCommandService _commands;
    private readonly TelegramWaveCallbackHandler _callbacks;
    private Guid _projectId = Guid.Empty;

    public TelegramWaveSendFlowTests()
    {
        _cache = new TelegramBotClientCache(new StubTelegramBotClientFactory(_api));
        (_wakeFanout, _wakeServices) = TestWakeFanout.Create(_temp);
        _commands = new TelegramWaveCommandService(
            new TelegramReplySender(_cache, CreateOptionsProvider()),
            _drafts,
            new WaveChatService(_temp.CreateContext(), _wakeFanout, _broadcaster),
            _temp.CreateContext(),
            _snapshotDoor,
            new CollectingLogger<TelegramWaveCommandService>(_logs));
        _callbacks = new TelegramWaveCallbackHandler(
            CreateOptionsProvider(), _cache, _commands, _snapshotDoor,
            new CollectingLogger<TelegramWaveCallbackHandler>(_logs));
    }

    public async Task InitializeAsync()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.TelegramEnabled, "true", CancellationToken.None);
        await settings.SetAsync(SettingKeys.TelegramAllowedChatIds, "42", CancellationToken.None);
        await settings.SetAsync(SettingKeys.TelegramBotToken, TestToken, CancellationToken.None);
    }

    [Fact]
    public async Task SendToWave_OpensTheProjectPickerWithACancelDoor()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);

        await RunSendToWaveAsync();

        var picker = SentRequests()[0];
        SentText(picker).ShouldBe(TelegramWaveCommandService.ProjectPickerText);
        Labels(picker).ShouldBe(["Overmind", CancelLabel]);
        Data(picker).ShouldBe([$"wave:proj:{_projectId:D}", TelegramWaveCallbackData.CancelData]);
    }

    [Fact]
    public async Task SendToWave_TwiceSupersedesThePendingDraft()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        var firstToken = Prompt().Token;
        var firstRoom = Prompt().Room;

        await RunSendToWaveAsync();

        firstRoom.ShouldBeNull();
        Prompt().Token.ShouldNotBe(firstToken);
        Prompt().ProjectId.ShouldBeNull();
        SentRequests().Count.ShouldBe(3);
    }

    [Fact]
    public async Task ProjectPick_OffersTheSharedRoomPlusTheLaneRoster()
    {
        await SeedProjectAsync("Overmind", ["mcp-tool-description-diet", "telegram-bridge"]);

        await OpenRoomPickerAsync();

        var picker = SentRequests()[1];
        SentText(picker).ShouldBe("Which room in Overmind?");
        Labels(picker).ShouldBe(["all", "mcp-tool-description-diet", "telegram-bridge", CancelLabel]);
        var tokens = RoomData(picker).Select(data => data.Split(':')[2]).Distinct().ToList();
        tokens.ShouldHaveSingleItem().ShouldBe(Prompt().Token);
        Answers().ShouldContain(TelegramWaveCommandService.RoomPickAnswer);
    }

    [Fact]
    public async Task RoomPick_AsksForTheSignalWithForceReply()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();

        await PickRoomAsync("telegram-bridge");

        var prompt = SentRequests()[2];
        SentText(prompt).ShouldBe(
            string.Format(TelegramWaveCommandService.PromptTextFormat, "Overmind · telegram-bridge"));
        Markup(prompt).GetProperty("force_reply").GetBoolean().ShouldBeTrue();
        Markup(prompt).TryGetProperty("inline_keyboard", out _).ShouldBeFalse();
        Answers().ShouldContain(TelegramWaveCommandService.PromptAnswer);
        Prompt().PromptMessageId.ShouldNotBeNull();
    }

    [Fact]
    public async Task FullFlow_ProjectPickRoomPickPromptReply_LandsOneWaveMessage()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;

        await ReplyAsync(promptMessageId, Signal);

        var posted = (await WaveMessagesAsync()).ShouldHaveSingleItem();
        posted.ProjectId.ShouldBe(_projectId);
        posted.Room.ShouldBe("telegram-bridge");
        posted.AuthorAlias.ShouldBe("Owner");
        posted.AuthorRole.ShouldBe(TelegramWaveCommandService.AuthorRole);
        posted.Text.ShouldBe(Signal);
        SentTexts().ShouldContain($"Posted to {posted.Room}.");
        _broadcaster.Published.ShouldHaveSingleItem().Room.ShouldBe("telegram-bridge");
        Prompt().PromptMessageId.ShouldBeNull();
    }

    [Fact]
    public async Task WaveFlow_TouchesNeitherTheConversationStoreNorTheAgentTurn()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        await ReplyAsync(Prompt().PromptMessageId!.Value, Signal);

        await using var context = _temp.CreateContext();
        context.Conversations.Count().ShouldBe(0);
        context.ConversationMessages.Count().ShouldBe(0);
        context.TelegramChatMappings.Count().ShouldBe(0);
    }

    [Fact]
    public async Task SignalText_IsPostedTrimmedAndVerbatim()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");

        await ReplyAsync(Prompt().PromptMessageId!.Value, $"  {Signal}\n\nsecond paragraph  ");

        (await WaveMessagesAsync()).ShouldHaveSingleItem().Text.ShouldBe($"{Signal}\n\nsecond paragraph");
    }

    [Fact]
    public async Task Cancel_AbandonsTheDraftAndAnswersThePress()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();

        await PressAsync(RoomPickerMessageId, TelegramWaveCallbackData.CancelData);

        Answers().ShouldContain(TelegramWaveCommandService.CancelAnswer);
        _drafts.Current(OwnerChatId).ShouldBeNull();
        var consumed = await TryReplyAsync(OrphanPromptMessageId, "orphaned");
        consumed.ShouldBeFalse();
        (await WaveMessagesAsync()).ShouldBeEmpty();
        SentRequests().Count.ShouldBe(2);
    }

    [Fact]
    public async Task PromptReplyAfterCancel_FallsThroughToTheConversationPath()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;

        await PressAsync(PromptMessageId, TelegramWaveCallbackData.CancelData);
        var consumed = await TryReplyAsync(promptMessageId, "orphaned");

        consumed.ShouldBeFalse();
        (await WaveMessagesAsync()).ShouldBeEmpty();
        SentRequests().Count.ShouldBe(3);
    }

    [Fact]
    public async Task SignalOverTheRoomCap_NoticesTheSenderAndPostsNothing()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;

        var consumed = await TryReplyAsync(promptMessageId, new string('x', WaveMessage.MaxTextLength + 1));

        consumed.ShouldBeTrue();
        (await WaveMessagesAsync()).ShouldBeEmpty();
        SentTexts().ShouldContain(text =>
            text.StartsWith("That is 501 characters", StringComparison.Ordinal)
            && text.Contains("capped at 500", StringComparison.Ordinal));
        Prompt().PromptMessageId.ShouldBe(promptMessageId);
    }

    [Fact]
    public async Task ReplyToAnUnknownPrompt_IsLeftToTheConversationPath()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");

        var consumed = await TryReplyAsync(Prompt().PromptMessageId!.Value + 1, "wrong parent");

        consumed.ShouldBeFalse();
        (await WaveMessagesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplyBeforeAnyRoomPick_IsLeftToTheConversationPath()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await RunSendToWaveAsync();

        var consumed = await TryReplyAsync(RoomPickerMessageId, "too early");

        consumed.ShouldBeFalse();
        (await WaveMessagesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task RoomPressAgainstASupersededDraft_IsRefusedWithoutAPrompt()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        var staleToken = Prompt().Token;
        await RunSendToWaveAsync();

        await PressAsync(RoomPickerMessageId, TelegramWaveCallbackData.RoomData(staleToken, 0));

        Answers().ShouldContain(TelegramNotificationCallbackHandler.InvalidButtonAnswer);
        SentRequests().Count.ShouldBe(3);
        Prompt().PromptMessageId.ShouldBeNull();
    }

    [Fact]
    public async Task RoomIndexBeyondTheDraftSnapshot_IsRefused()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();

        await PressAsync(RoomPickerMessageId, TelegramWaveCallbackData.RoomData(Prompt().Token, 9));

        Answers().ShouldContain(TelegramNotificationCallbackHandler.InvalidButtonAnswer);
        Prompt().Room.ShouldBeNull();
    }

    [Fact]
    public async Task PressesWithoutADraftAreRefusedOnEveryScheme()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);

        await PressAsync(ProjectPickerMessageId, $"wave:proj:{_projectId:D}");
        await PressAsync(RoomPickerMessageId, TelegramWaveCallbackData.RoomData("1f0aa0e0", 0));

        Answers().ShouldBe([
            TelegramNotificationCallbackHandler.InvalidButtonAnswer,
            TelegramNotificationCallbackHandler.InvalidButtonAnswer
        ]);
    }

    [Fact]
    public async Task ForeignChatPress_IsDeniedWithoutAPickerOrAPost()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await RunSendToWaveAsync();

        await _callbacks.HandleAsync(
            new TelegramCallback(999, "cq-foreign", ProjectPickerMessageId, "@stranger", "picker",
                $"wave:proj:{_projectId:D}"),
            CancellationToken.None);

        Answers().ShouldBe([TelegramNotificationCallbackHandler.NotAllowedAnswer]);
        SentRequests().Count.ShouldBe(1);
        (await WaveMessagesAsync()).ShouldBeEmpty();
        _logs.ShouldContain(line => line.Contains("not allowlisted; denied", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GarbageWaveData_IsRefusedWithTheSharedEtiquetteAnswer()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);

        await _callbacks.HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-junk", 1, "@owner", "picker", "wave:room:ZZZZZZZZ:1"),
            CancellationToken.None);

        Answers().ShouldBe([TelegramNotificationCallbackHandler.InvalidButtonAnswer]);
        SentRequests().ShouldBeEmpty();
    }

    [Fact]
    public async Task EveryPress_CarriesExactlyOneAnswerCallbackQuery()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");

        await PressAsync(22, TelegramWaveCallbackData.CancelData);

        Answers().Count().ShouldBe(3);
        Answers().ShouldAllBe(answer => answer.Length > 0);
        Answers().ShouldAllBe(answer => answer.Length <= TelegramNotificationCallbackHandler.AnswerTextLimit);
    }

    [Fact]
    public async Task GeneratedRoomData_FitsTheByteBudgetForTheLongestAddressableLaneName()
    {
        await SeedProjectAsync("Overmind", ["mcp-tool-description-diet"]);

        await OpenRoomPickerAsync();

        var picker = SentRequests()[1];
        var name = Labels(picker)[1];
        name.ShouldBe("mcp-tool-description-diet");
        foreach (var data in RoomData(picker))
        {
            Encoding.UTF8.GetByteCount(data).ShouldBeLessThanOrEqualTo(
                TelegramWaveCallbackData.CallbackDataLimit);
        }

        Encoding.UTF8.GetByteCount($"wave:room:{_projectId:D}:{name}")
            .ShouldBeGreaterThan(TelegramWaveCallbackData.CallbackDataLimit);
    }

    [Fact]
    public async Task CyrillicLane_StaysAddressableAndKeepsItsOwnSpellingInThePostedRoom()
    {
        var cyrillic = "мост-для-волны-через-телеграм";
        await SeedProjectAsync("Grimoire", [cyrillic]);
        await OpenRoomPickerAsync();

        Encoding.UTF8.GetByteCount(cyrillic)
            .ShouldBeGreaterThan(
                TelegramWaveCallbackData.CallbackDataLimit
                - Encoding.UTF8.GetByteCount($"wave:room:{_projectId:D}:"));
        await PickRoomAsync(cyrillic);
        await ReplyAsync(Prompt().PromptMessageId!.Value, "cyrillic signal");

        (await WaveMessagesAsync()).ShouldHaveSingleItem().Room.ShouldBe(cyrillic);
    }

    [Fact]
    public async Task DeletedRoomBetweenPickerAndReply_DropsTheDraftAndNoticesTheSender()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;

        await using (var context = _temp.CreateContext())
        {
            var lane = await context.PlanningLanes.SingleAsync(l => l.Name == "telegram-bridge");
            context.PlanningLanes.Remove(lane);
            await context.SaveChangesAsync();
        }

        await ReplyAsync(promptMessageId, "signal into a deleted lane");

        (await WaveMessagesAsync()).ShouldBeEmpty();
        SentTexts().ShouldContain(text => text.Contains("refused the post", StringComparison.Ordinal));
        _drafts.Current(OwnerChatId).ShouldBeNull();
    }

    [Fact]
    public async Task WaveSnapshot_HandsTheCommandToTheSnapshotDoor()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);

        await _commands.HandleAsync(
            TelegramWaveCommand.WaveSnapshot,
            new TelegramIncomingMessage(OwnerChatId, "/wave_snapshot", From: "Owner"),
            CancellationToken.None);

        await _snapshotDoor.Received(1).HandleCommandAsync(OwnerChatId, Arg.Any<CancellationToken>());
        SentRequests().ShouldBeEmpty();
        _drafts.Current(OwnerChatId).ShouldBeNull();
    }

    [Fact]
    public async Task NoProjectsOnTheHost_SaysSoInsteadOfSendingAnEmptyPicker()
    {
        await RunSendToWaveAsync();

        SentTexts().ShouldBe([TelegramWaveCommandService.NoProjectsReply]);
        SentRequests().ShouldHaveSingleItem().Body.ShouldNotContain("reply_markup");
    }

    [Fact]
    public async Task ProjectPickerCountsTheProjectsItLeftOut()
    {
        for (var index = 0; index < TelegramWaveCommandService.MaxProjectButtons + 5; index++)
        {
            await SeedProjectAsync($"p{index:D2}", []);
        }

        await RunSendToWaveAsync();

        SentTexts()[0].ShouldBe(
            $"Which project owns this signal? (the first {TelegramWaveCommandService.MaxProjectButtons} by name, 5 more not shown)");
        SentRequests().ShouldHaveSingleItem().Body.ShouldNotContain("p44");
    }

    [Fact]
    public async Task SenderWithoutAUsernamePostsUnderTheFallbackAlias()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");

        await _commands.TryHandleDraftReplyAsync(
            new TelegramIncomingMessage(
                OwnerChatId, "anonymous signal", Prompt().PromptMessageId, null),
            CancellationToken.None);

        (await WaveMessagesAsync()).ShouldHaveSingleItem()
            .AuthorAlias.ShouldBe(TelegramWaveCommandService.AnonymousAuthor);
    }

    [Fact]
    public async Task LongSenderName_IsClampedToTheIdentityCapInsteadOfThrowing()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");

        await ReplyAsync(
            Prompt().PromptMessageId!.Value,
            "clamped author",
            from: new string('ы', WaveMessage.MaxIdentityLength + 10));

        (await WaveMessagesAsync()).ShouldHaveSingleItem()
            .AuthorAlias.Length.ShouldBe(WaveMessage.MaxIdentityLength);
    }

    [Fact]
    public async Task ProjectPickerTruncatesTheLabelButNeverTheCallbackData()
    {
        await SeedProjectAsync(new string('ы', 90), ["telegram-bridge"]);

        await RunSendToWaveAsync();

        var picker = SentRequests()[0];
        Labels(picker)[0].Length.ShouldBe(TelegramWaveCallbackData.CallbackDataLimit);
        Data(picker)[0].ShouldBe($"wave:proj:{_projectId:D}");
    }

    public Task DisposeAsync()
    {
        _cache.Dispose();
        _wakeServices.Dispose();
        _temp.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task NotificationReplyDoor_WinsOverTheWaveDraftCapture()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;

        var notification = AgentNotification.Create(NotificationKind.Approval, "approve me", null, null, null);
        await using (var context = _temp.CreateContext())
        {
            context.AgentNotifications.Add(notification);
            await context.SaveChangesAsync();
            await new TelegramNotificationDeliveryStore(context)
                .SaveAsync(OwnerChatId, promptMessageId, notification.Id);
        }

        var (handler, stream) = CreateAgentHandler();
        await handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "approved via the prompt", promptMessageId, "Owner"),
            CancellationToken.None);

        SentTexts().ShouldContain(TelegramAgentChatHandler.ReplyRegisteredAck);
        stream.Invocations.ShouldBe(0);
        (await WaveMessagesAsync()).ShouldBeEmpty();
        await using var proof = _temp.CreateContext();
        (await proof.AgentNotifications.SingleAsync(n => n.Id == notification.Id)).RepliedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task WaveReplyThroughTheMessageDoor_NeverReachesTheAgentTurn()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;

        var (handler, stream) = CreateAgentHandler();
        await handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, Signal, promptMessageId, "Owner"),
            CancellationToken.None);

        stream.Invocations.ShouldBe(0);
        SentTexts().ShouldContain($"Posted to telegram-bridge.");
        (await WaveMessagesAsync()).ShouldHaveSingleItem().Text.ShouldBe(Signal);
        await using var context = _temp.CreateContext();
        context.TelegramChatMappings.Any().ShouldBeFalse();
    }

    [Fact]
    public async Task TypedCommand_KeepsPriorityOverTheWaveDraftCapture()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;
        var capturedToken = Prompt().Token;

        var (handler, stream) = CreateAgentHandler();
        await handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "/send_to_wave", promptMessageId, "Owner"),
            CancellationToken.None);

        stream.Invocations.ShouldBe(0);
        (await WaveMessagesAsync()).ShouldBeEmpty();
        Prompt().Token.ShouldNotBe(capturedToken);
        Prompt().PromptMessageId.ShouldBeNull();
    }

    [Fact]
    public async Task OrdinaryReplyToAnUnknownMessage_StillRunsTheAgentTurn()
    {
        var (handler, stream) = CreateAgentHandler(
            [ConversationStreamEvent.Delta("ordinary answer"), ConversationStreamEvent.Completed(5)]);

        await handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "privet", 9_009, "Owner"),
            CancellationToken.None);

        stream.Invocations.ShouldBe(1);
        SentTexts().ShouldBe(["ordinary answer"]);
    }

    private (TelegramAgentChatHandler Handler, CountingStreamSource Stream) CreateAgentHandler(
        IReadOnlyList<ConversationStreamEvent>? turn = null)
    {
        var stream = new CountingStreamSource(turn);
        var handler = new TelegramAgentChatHandler(
            new TelegramChatStore(_temp.CreateContext()),
            new ConversationMessageStore(_temp.CreateContext()),
            stream,
            new ConversationTurnGate(),
            new TelegramReplySender(_cache, CreateOptionsProvider()),
            new TelegramNotificationDeliveryStore(_temp.CreateContext()),
            new NotificationsService(
                _temp.CreateContext(),
                new ReplyAutoActFanout(
                    _temp.CreateContext(), new ReplyAutoActQueue(), NullLogger<ReplyAutoActFanout>.Instance),
                _wakeFanout,
                []),
            Substitute.For<INotificationValidatorRunner>(),
            _commands,
            new CollectingLogger<TelegramAgentChatHandler>(_logs));
        return (handler, stream);
    }

    private sealed class CountingStreamSource(IReadOnlyList<ConversationStreamEvent>? turn)
        : IConversationStreamSource
    {
        public int Invocations { get; private set; }

        public async IAsyncEnumerable<ConversationStreamEvent> StreamAsync(
            Conversation conversation,
            IReadOnlyList<ConversationMessage> history,
            string userText,
            [EnumeratorCancellation] CancellationToken ct = default,
            RoutingMissContext? missContext = null)
        {
            Invocations++;
            if (turn is null)
            {
                throw new InvalidOperationException("the wave flow must never start an agent turn");
            }

            foreach (var streamEvent in turn)
            {
                yield return streamEvent;
            }

            await Task.CompletedTask;
        }
    }

    private const int ProjectPickerMessageId = 11;
    private const int RoomPickerMessageId = 12;
    private const int PromptMessageId = 21;
    private const long OrphanPromptMessageId = 9_999;

    private TelegramTransportOptionsProvider CreateOptionsProvider() =>
        new(new AppSettingsService(_temp.CreateContext()));

    private TelegramWaveDraft Prompt() => _drafts.Current(OwnerChatId)!;

    private async Task OpenRoomPickerAsync()
    {
        await RunSendToWaveAsync();
        await PressAsync(ProjectPickerMessageId, $"wave:proj:{_projectId:D}");
    }

    private Task RunSendToWaveAsync() =>
        _commands.HandleAsync(
            TelegramWaveCommand.SendToWave,
            new TelegramIncomingMessage(OwnerChatId, "/send_to_wave", From: "Owner"),
            CancellationToken.None);

    private async Task PickRoomAsync(string room)
    {
        var draft = Prompt();
        var index = draft.Rooms.ToList().IndexOf(room);
        index.ShouldBeGreaterThanOrEqualTo(0, $"the draft snapshot never offered {room}");
        await PressAsync(RoomPickerMessageId, TelegramWaveCallbackData.RoomData(draft.Token, index));
    }

    private Task PressAsync(int messageId, string data) =>
        _callbacks.HandleAsync(
            new TelegramCallback(OwnerChatId, $"cq-{messageId}-{data}", messageId, "@owner", "picker", data),
            CancellationToken.None);

    private async Task ReplyAsync(long promptMessageId, string text, string? from = "Owner")
    {
        var consumed = await TryReplyAsync(promptMessageId, text, from);
        consumed.ShouldBeTrue();
    }

    private Task<bool> TryReplyAsync(long promptMessageId, string text, string? from = "Owner") =>
        _commands.TryHandleDraftReplyAsync(
            new TelegramIncomingMessage(OwnerChatId, text, promptMessageId, from), CancellationToken.None);

    private async Task<Guid> SeedProjectAsync(string name, IReadOnlyList<string> lanes)
    {
        await using var context = _temp.CreateContext();
        var project = Project.Create(name, $"/tmp/{Guid.NewGuid():N}");
        context.Projects.Add(project);
        var order = 1;
        foreach (var lane in lanes)
        {
            context.PlanningLanes.Add(PlanningLane.Create(project.Id, lane, "contour", order++));
        }

        await context.SaveChangesAsync();
        _projectId = project.Id;
        return project.Id;
    }

    private async Task<List<WaveMessage>> WaveMessagesAsync()
    {
        await using var context = _temp.CreateContext();
        return await context.WaveMessages.AsNoTracking().ToListAsync();
    }

    private List<FakeTelegramApi.Captured> SentRequests() =>
        [.. _api.Requests.Where(request => request.Path.Contains("/sendMessage", StringComparison.Ordinal))];

    private List<string> SentTexts() => [.. SentRequests().Select(SentText)];

    private List<string> Answers() =>
        [.. _api.Requests
            .Where(request => request.Path.Contains("answerCallbackQuery", StringComparison.Ordinal))
            .Select(TextOf)];

    private static string SentText(FakeTelegramApi.Captured captured) => TextOf(captured);

    private static string TextOf(FakeTelegramApi.Captured captured)
    {
        using var document = JsonDocument.Parse(captured.Body);
        return document.RootElement.TryGetProperty("text", out var text)
            ? text.GetString() ?? string.Empty
            : string.Empty;
    }

    private static JsonElement Markup(FakeTelegramApi.Captured captured)
    {
        using var document = JsonDocument.Parse(captured.Body);
        return document.RootElement.GetProperty("reply_markup").Clone();
    }

    private static List<(string Label, string Data)> Rows(FakeTelegramApi.Captured captured)
    {
        using var document = JsonDocument.Parse(captured.Body);
        var rows = document.RootElement.GetProperty("reply_markup").GetProperty("inline_keyboard");
        return [.. rows.EnumerateArray().SelectMany(row => row.EnumerateArray())
            .Select(button => (button.GetProperty("text").GetString() ?? string.Empty,
                button.GetProperty("callback_data").GetString() ?? string.Empty))];
    }

    private static List<string> Labels(FakeTelegramApi.Captured captured) =>
        [.. Rows(captured).Select(row => row.Label)];

    private static List<string> Data(FakeTelegramApi.Captured captured) =>
        [.. Rows(captured).Select(row => row.Data)];

    private static List<string> RoomData(FakeTelegramApi.Captured captured) =>
        [.. Data(captured).Where(data => data.StartsWith("wave:room:", StringComparison.Ordinal))];
}
