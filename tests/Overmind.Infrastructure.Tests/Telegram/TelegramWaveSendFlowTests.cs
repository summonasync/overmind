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
    private readonly TelegramTestClock _clock = new();
    private readonly TelegramWaveDraftStore _drafts;
    private readonly TelegramWavePromptTombstone _prompts;
    private readonly ConversationTurnGate _turns = new();
    private readonly ITelegramWaveSnapshotService _snapshotDoor = Substitute.For<ITelegramWaveSnapshotService>();
    private readonly TelegramWaveCommandService _commands;
    private readonly TelegramWaveCallbackHandler _callbacks;
    private Guid _projectId = Guid.Empty;

    public TelegramWaveSendFlowTests()
    {
        _cache = new TelegramBotClientCache(new StubTelegramBotClientFactory(_api));
        (_wakeFanout, _wakeServices) = TestWakeFanout.Create(_temp);
        _drafts = new TelegramWaveDraftStore(_clock);
        _prompts = new TelegramWavePromptTombstone(_clock);
        _commands = BuildCommandService(_drafts, _prompts);
        _callbacks = new TelegramWaveCallbackHandler(
            CreateOptionsProvider(), _cache, _commands, _snapshotDoor,
            new CollectingLogger<TelegramWaveCallbackHandler>(_logs));
    }

    private TelegramWaveCommandService BuildCommandService(
        TelegramWaveDraftStore drafts, TelegramWavePromptTombstone prompts) => new(
            new TelegramReplySender(_cache, CreateOptionsProvider()),
            drafts,
            prompts,
            new WaveChatService(_temp.CreateContext(), _wakeFanout, _broadcaster),
            _temp.CreateContext(),
            _snapshotDoor,
            new CollectingLogger<TelegramWaveCommandService>(_logs));

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
    public async Task PromptReplyAfterCancel_AnswersTheClosedDraftNoticeInsteadOfTheModel()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;

        await PressAsync(PromptMessageId, TelegramWaveCallbackData.CancelData);
        var (handler, probe) = CreateAgentHandler();

        await handler.HandleAsync(
            new TelegramIncomingMessage(
                OwnerChatId, "orphaned", promptMessageId, "Owner", ReplyMessageId, ReplyUpdateId),
            CancellationToken.None);

        (await WaveMessagesAsync()).ShouldBeEmpty();
        probe.AssertUntouched("a cancelled prompt must not buy an agent turn");
        await ZeroModelProbe.AssertNoConversationFootprintAsync(_temp);
        SentTexts().ShouldContain(TelegramWaveCommandService.DraftClosedReply);
        SentRequests().Count.ShouldBe(4);
        _logs.ShouldContain(line =>
            line.StartsWith("Information:", StringComparison.Ordinal)
            && line.Contains(
                $"was closed; reply message {ReplyMessageId} of update {ReplyUpdateId} to prompt {promptMessageId}",
                StringComparison.Ordinal));
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

    [Theory]
    [MemberData(nameof(WaveBranchCases.WaveCommands), MemberType = typeof(WaveBranchCases))]
    public async Task EveryWaveCommand_LandsOnTheDoorItsBranchMapPromises(TelegramWaveCommand command)
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);

        await _commands.HandleAsync(
            command,
            new TelegramIncomingMessage(
                OwnerChatId, WaveBranchCases.CommandText[command], From: "Owner",
                MessageId: CommandMessageId, UpdateId: CommandUpdateId),
            CancellationToken.None);

        switch (WaveBranchCases.CommandDoor[command])
        {
            case WaveBranchCases.WaveDoor.SnapshotDoor:
                await _snapshotDoor.Received(1).HandleCommandAsync(OwnerChatId, Arg.Any<CancellationToken>());
                SentRequests().ShouldBeEmpty();
                _drafts.Current(OwnerChatId).ShouldBeNull();
                break;
            case WaveBranchCases.WaveDoor.SendFlow:
                await _snapshotDoor.DidNotReceiveWithAnyArgs().HandleCommandAsync(default, default);
                SentRequests().ShouldHaveSingleItem();
                _drafts.Current(OwnerChatId).ShouldNotBeNull();
                break;
        }
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

        var (handler, probe) = CreateAgentHandler();
        await handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "approved via the prompt", promptMessageId, "Owner"),
            CancellationToken.None);

        SentTexts().ShouldContain(TelegramAgentChatHandler.ReplyRegisteredAck);
        probe.StreamInvocations.ShouldBe(0);
        probe.ValidatorInvocations.ShouldBe(1);
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

        var (handler, probe) = CreateAgentHandler();
        await handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, Signal, promptMessageId, "Owner"),
            CancellationToken.None);

        probe.AssertUntouched("a live wave draft must not buy an agent turn");
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

        var (handler, probe) = CreateAgentHandler();
        await handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "/send_to_wave", promptMessageId, "Owner"),
            CancellationToken.None);

        probe.AssertUntouched("a typed wave command must not buy an agent turn");
        (await WaveMessagesAsync()).ShouldBeEmpty();
        Prompt().Token.ShouldNotBe(capturedToken);
        Prompt().PromptMessageId.ShouldBeNull();
    }

    [Fact]
    public async Task OrdinaryReplyToAnUnknownMessage_StillRunsTheAgentTurn()
    {
        var (handler, probe) = CreateAgentHandler(
            [ConversationStreamEvent.Delta("ordinary answer"), ConversationStreamEvent.Completed(5)]);

        await handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "privet", 9_009, "Owner"),
            CancellationToken.None);

        probe.StreamInvocations.ShouldBe(1);
        SentTexts().ShouldBe(["ordinary answer"]);
        await ZeroModelProbe.AssertFootprintGrowsWhenTheModelRunsAsync(_temp);
    }

    [Fact]
    public async Task PromptReplyAfterTheDraftExpires_AnswersTheClosedDraftNotice()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;

        _clock.Advance(TimeSpan.FromMinutes(TelegramWaveDraft.LifetimeMinutes + 1));
        var consumed = await TryReplyAsync(promptMessageId, "a signal an hour late");

        consumed.ShouldBeTrue();
        (await WaveMessagesAsync()).ShouldBeEmpty();
        SentTexts().ShouldContain(TelegramWaveCommandService.DraftClosedReply);
        _drafts.Current(OwnerChatId).ShouldBeNull();
    }

    [Fact]
    public async Task PromptReplyAfterTheDraftExpiresThroughTheMessageDoor_AnswersTheClosedDraftNoticeWithNoAgentTurn()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;

        _clock.Advance(TimeSpan.FromMinutes(TelegramWaveDraft.LifetimeMinutes + 1));
        var (handler, probe) = CreateAgentHandler();

        await handler.HandleAsync(
            new TelegramIncomingMessage(
                OwnerChatId, "a signal an hour late", promptMessageId, "Owner", ReplyMessageId, ReplyUpdateId),
            CancellationToken.None);

        probe.AssertUntouched("an expired prompt must not buy an agent turn");
        await ZeroModelProbe.AssertNoConversationFootprintAsync(_temp);
        (await WaveMessagesAsync()).ShouldBeEmpty();
        SentTexts().ShouldContain(TelegramWaveCommandService.DraftClosedReply);
        _logs.ShouldContain(line =>
            line.StartsWith("Information:", StringComparison.Ordinal)
            && line.Contains(
                $"was closed; reply message {ReplyMessageId} of update {ReplyUpdateId} to prompt {promptMessageId}",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task SecondReplyToAPostedPrompt_IsRefusedInsteadOfPostingTwice()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;
        await ReplyAsync(promptMessageId, Signal);

        var consumed = await TryReplyAsync(promptMessageId, Signal);

        consumed.ShouldBeTrue();
        (await WaveMessagesAsync()).ShouldHaveSingleItem();
        SentTexts().ShouldContain(TelegramWaveCommandService.DraftClosedReply);
    }

    [Fact]
    public async Task RoomPick_APromptThatSplits_RegistersEveryChunkInDraftAndTombstone()
    {
        var chunks = await OpenSplitPromptAsync();

        chunks.Count.ShouldBeGreaterThanOrEqualTo(3);
        string.Concat(chunks.Select(SentText))
            .ShouldBe(string.Format(
                TelegramWaveCommandService.PromptTextFormat,
                $"{SplitPromptProjectName} · telegram-bridge"));
        foreach (var chunk in chunks)
        {
            Markup(chunk).GetProperty("force_reply").GetBoolean().ShouldBeTrue();
        }

        List<long> delivered = [.. chunks.Select(chunk => chunk.ResultMessageId!.Value)];
        Prompt().PromptMessageIds.ShouldBe(delivered);
        foreach (var id in delivered)
        {
            _prompts.IsClosed(OwnerChatId, id).ShouldBeTrue();
        }

        _prompts.IsClosed(OwnerChatId, delivered[^1] + 1).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task PromptReplyToAnyChunkOfASplitPrompt_IsCapturedByTheWaveDoorWithNoAgentTurn(int chunkSelector)
    {
        var chunks = await OpenSplitPromptAsync();

        var (handler, probe) = CreateAgentHandler();
        await handler.HandleAsync(
            new TelegramIncomingMessage(
                OwnerChatId, Signal, ChunkIdAt(chunks, chunkSelector), "Owner", ReplyMessageId, ReplyUpdateId),
            CancellationToken.None);

        probe.AssertUntouched("a reply aimed at any chunk of a split prompt is the wave door's");
        (await WaveMessagesAsync()).ShouldHaveSingleItem().Text.ShouldBe(Signal);
        SentTexts().ShouldContain("Posted to telegram-bridge.");
        await ZeroModelProbe.AssertNoConversationFootprintAsync(_temp);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task ReplyToAnyChunkOfAPostedSplitPrompt_GetsTheRestartNoticeNotAnAgentTurn(int chunkSelector)
    {
        var chunks = await OpenSplitPromptAsync();
        var promptMessageId = ChunkIdAt(chunks, chunkSelector);
        await ReplyAsync(ChunkIdAt(chunks, -1), Signal);

        var (handler, probe) = CreateAgentHandler();
        await handler.HandleAsync(
            new TelegramIncomingMessage(
                OwnerChatId, "a second signal", promptMessageId, "Owner", ReplyMessageId, ReplyUpdateId),
            CancellationToken.None);

        probe.AssertUntouched("a closed split prompt must not buy an agent turn on any of its chunks");
        (await WaveMessagesAsync()).ShouldHaveSingleItem().Text.ShouldBe(Signal);
        SentTexts().ShouldContain(TelegramWaveCommandService.DraftClosedReply);
        _logs.ShouldContain(line =>
            line.StartsWith("Information:", StringComparison.Ordinal)
            && line.Contains(
                $"was closed; reply message {ReplyMessageId} of update {ReplyUpdateId} to prompt {promptMessageId}",
                StringComparison.Ordinal));
    }

    [Fact]
    public void WavePromptText_AtTheWidestLegalTarget_StaysASingleChunk()
    {
        var widest = string.Format(
            TelegramWaveCommandService.PromptTextFormat,
            $"{new string('p', WidestProjectNameLength)} · {new string('r', PlanningLane.MaxNameLength)}");

        widest.Length.ShouldBeLessThan(TelegramReplySplitter.MaxMessageLength);
        TelegramReplySplitter.Split(widest).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RestartedHost_FallsThroughOnAnOldPromptTheNewProcessNeverIssued()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;
        var revived = BuildCommandService(new TelegramWaveDraftStore(_clock), new TelegramWavePromptTombstone(_clock));

        var consumed = await revived.TryHandleDraftReplyAsync(
            new TelegramIncomingMessage(OwnerChatId, "signal after the restart", promptMessageId, "Owner"),
            CancellationToken.None);

        consumed.ShouldBeFalse();
        (await WaveMessagesAsync()).ShouldBeEmpty();
        SentTexts().ShouldNotContain(TelegramWaveCommandService.DraftClosedReply);
    }

    [Fact]
    public async Task DeletedProjectBetweenPickAndPost_NoticesTheSenderWithoutACrash()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;
        await using (var context = _temp.CreateContext())
        {
            context.Projects.Remove(await context.Projects.SingleAsync(p => p.Id == _projectId));
            await context.SaveChangesAsync();
        }

        var consumed = await TryReplyAsync(promptMessageId, "signal into a deleted project");

        consumed.ShouldBeTrue();
        (await WaveMessagesAsync()).ShouldBeEmpty();
        SentTexts().ShouldContain(text => text.Contains("refused the post", StringComparison.Ordinal));
        _drafts.Current(OwnerChatId).ShouldBeNull();
        _logs.ShouldContain(line => line.Contains("was refused; the draft was dropped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DraftReplyWhileAnAgentTurnIsRunning_IsCapturedAndNeverEntersTheGate()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;
        var conversation = await new TelegramChatStore(_temp.CreateContext())
            .GetOrCreateConversationAsync(OwnerChatId, CancellationToken.None);
        _turns.TryEnter(conversation.Id).ShouldBeTrue();
        var (handler, probe) = CreateAgentHandler();

        await handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, Signal, promptMessageId, "Owner"),
            CancellationToken.None);

        probe.AssertUntouched("a wave signal must not queue behind a running turn");
        (await WaveMessagesAsync()).ShouldHaveSingleItem().Text.ShouldBe(Signal);
        SentTexts().ShouldNotContain(TelegramAgentChatHandler.BusyReplyText);
        _turns.IsBusy(conversation.Id).ShouldBeTrue();
        _turns.Exit(conversation.Id);
    }

    [Fact]
    public async Task TypedCommandWhileAnAgentTurnIsRunning_OpensThePickerAndKeepsTheGate()
    {
        var conversation = await new TelegramChatStore(_temp.CreateContext())
            .GetOrCreateConversationAsync(OwnerChatId, CancellationToken.None);
        _turns.TryEnter(conversation.Id).ShouldBeTrue();
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        var (handler, probe) = CreateAgentHandler();

        await handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "/send_to_wave", From: "Owner"), CancellationToken.None);

        probe.AssertUntouched("a wave command must not queue behind a running turn");
        Prompt().ShouldNotBeNull();
        SentTexts().ShouldNotContain(TelegramAgentChatHandler.BusyReplyText);
        _turns.IsBusy(conversation.Id).ShouldBeTrue();
        _turns.Exit(conversation.Id);
    }

    [Fact]
    public async Task NotificationReplyDoor_KeepsPriorityOverTheClosedDraftRefusal()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;
        await PressAsync(PromptMessageId, TelegramWaveCallbackData.CancelData);
        var notification = AgentNotification.Create(NotificationKind.Approval, "approve me", null, null, null);
        await using (var context = _temp.CreateContext())
        {
            context.AgentNotifications.Add(notification);
            await context.SaveChangesAsync();
            await new TelegramNotificationDeliveryStore(context)
                .SaveAsync(OwnerChatId, promptMessageId, notification.Id);
        }

        var (handler, probe) = CreateAgentHandler();
        await handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "approved late", promptMessageId, "Owner"),
            CancellationToken.None);

        SentTexts().ShouldContain(TelegramAgentChatHandler.ReplyRegisteredAck);
        SentTexts().ShouldNotContain(TelegramWaveCommandService.DraftClosedReply);
        probe.StreamInvocations.ShouldBe(0);
        probe.ValidatorInvocations.ShouldBe(1);
    }

    [Fact]
    public async Task TypedCommandDoor_KeepsPriorityOverTheClosedDraftRefusal()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        var promptMessageId = Prompt().PromptMessageId!.Value;
        await PressAsync(PromptMessageId, TelegramWaveCallbackData.CancelData);

        var (handler, probe) = CreateAgentHandler();
        await handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "/send_to_wave", promptMessageId, "Owner"),
            CancellationToken.None);

        SentTexts().ShouldNotContain(TelegramWaveCommandService.DraftClosedReply);
        SentTexts().ShouldContain(TelegramWaveCommandService.ProjectPickerText);
        probe.AssertUntouched("a typed command wins over the closed draft refusal");
    }

    [Fact]
    public async Task CommandInvocationsAndTheWavePost_AreLoggedForTheSmokeAudit()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        await PickRoomAsync("telegram-bridge");
        await ReplyAsync(Prompt().PromptMessageId!.Value, Signal);

        await _commands.HandleAsync(
            TelegramWaveCommand.WaveSnapshot,
            new TelegramIncomingMessage(
                OwnerChatId, "/wave_snapshot", From: "Owner",
                MessageId: CommandMessageId, UpdateId: CommandUpdateId),
            CancellationToken.None);

        var lines = _logs.ToArray();
        lines.ShouldContain(line =>
            line.StartsWith("Information:", StringComparison.Ordinal)
            && line.Contains(
                $"Telegram wave command {TelegramWaveCommand.SendToWave} invoked in chat {OwnerChatId} by Owner"
                + $" over message {CommandMessageId} of update {CommandUpdateId}",
                StringComparison.Ordinal));
        lines.ShouldContain(line =>
            line.Contains(
                $"Telegram wave command {TelegramWaveCommand.WaveSnapshot} invoked in chat {OwnerChatId} by Owner"
                + $" over message {CommandMessageId} of update {CommandUpdateId}",
                StringComparison.Ordinal));
        lines.ShouldContain(line => line.Contains(
            $"Telegram wave draft opened in chat {OwnerChatId}", StringComparison.Ordinal));
        lines.ShouldContain(line =>
            line.Contains(
                $"posted {Signal.Length} characters into room telegram-bridge of project {_projectId:D}",
                StringComparison.Ordinal)
            && line.Contains($"as Owner with role {TelegramWaveCommandService.AuthorRole}", StringComparison.Ordinal));
        await _snapshotDoor.Received(1).HandleCommandAsync(OwnerChatId, Arg.Any<CancellationToken>());
    }

    private (TelegramAgentChatHandler Handler, ZeroModelProbe Probe) CreateAgentHandler(
        IReadOnlyList<ConversationStreamEvent>? turn = null) =>
        CreateAgentHandler(new ZeroModelProbe(turn));

    private (TelegramAgentChatHandler Handler, ZeroModelProbe Probe) CreateAgentHandler(ZeroModelProbe probe)
    {
        var handler = new TelegramAgentChatHandler(
            new TelegramChatStore(_temp.CreateContext()),
            new ConversationMessageStore(_temp.CreateContext()),
            probe,
            _turns,
            new TelegramReplySender(_cache, CreateOptionsProvider()),
            new TelegramNotificationDeliveryStore(_temp.CreateContext()),
            new NotificationsService(
                _temp.CreateContext(),
                new ReplyAutoActFanout(
                    _temp.CreateContext(), new ReplyAutoActQueue(), NullLogger<ReplyAutoActFanout>.Instance),
                _wakeFanout,
                []),
            probe,
            _commands,
            new CollectingLogger<TelegramAgentChatHandler>(_logs));
        return (handler, probe);
    }

    private const int ProjectPickerMessageId = 11;
    private const int RoomPickerMessageId = 12;
    private const int PromptMessageId = 21;
    private const long OrphanPromptMessageId = 9_999;
    private const long CommandMessageId = 10;
    private const long CommandUpdateId = 100;
    private const long ReplyMessageId = 55;
    private const long ReplyUpdateId = 550;
    private const int SplitPromptProjectNameLength = 9_000;
    private const int WidestProjectNameLength = 255;
    private static readonly string SplitPromptProjectName = new('p', SplitPromptProjectNameLength);

    private TelegramTransportOptionsProvider CreateOptionsProvider() =>
        new(new AppSettingsService(_temp.CreateContext()));

    private TelegramWaveDraft Prompt() => _drafts.Current(OwnerChatId)!;

    private async Task<List<FakeTelegramApi.Captured>> OpenSplitPromptAsync()
    {
        await SeedProjectAsync(SplitPromptProjectName, ["telegram-bridge"]);
        await OpenRoomPickerAsync();
        var delivered = SentRequests().Count;

        await PickRoomAsync("telegram-bridge");

        return [.. SentRequests().Skip(delivered)];
    }

    private static long ChunkIdAt(IReadOnlyList<FakeTelegramApi.Captured> chunks, int chunkSelector) =>
        (chunkSelector < 0 ? chunks[^1] : chunks[chunkSelector]).ResultMessageId!.Value;

    private async Task OpenRoomPickerAsync()
    {
        await RunSendToWaveAsync();
        await PressAsync(ProjectPickerMessageId, $"wave:proj:{_projectId:D}");
    }

    private Task RunSendToWaveAsync() =>
        _commands.HandleAsync(
            TelegramWaveCommand.SendToWave,
            new TelegramIncomingMessage(
                OwnerChatId, "/send_to_wave", From: "Owner", MessageId: CommandMessageId, UpdateId: CommandUpdateId),
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
            new TelegramIncomingMessage(
                OwnerChatId, text, promptMessageId, from, ReplyMessageId, ReplyUpdateId), CancellationToken.None);

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
