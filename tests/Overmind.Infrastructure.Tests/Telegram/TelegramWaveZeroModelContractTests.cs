using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Planning;
using Overmind.Infrastructure.Telegram;
using Overmind.Infrastructure.Wake;
using Overmind.Infrastructure.Wave;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramWaveZeroModelContractTests : IAsyncLifetime
{
    private const long OwnerChatId = 42;
    private const string TestToken = "123456:TESTSECRETTOKEN";
    private const string Signal = "the wave lanes are clear";

    private readonly TempDatabase _temp = new();
    private readonly FakeTelegramApi _api = new();
    private readonly TelegramBotClientCache _cache;
    private readonly ServiceProvider _wakeServices;
    private readonly WakeDropFanout _wakeFanout;
    private readonly StubWaveChatBroadcaster _broadcaster = new();
    private readonly List<string> _logs = [];
    private readonly ZeroModelProbe _probe = new();
    private readonly TelegramWaveDraftStore _drafts = new(new TelegramTestClock());
    private readonly ConversationTurnGate _turns = new();
    private readonly TelegramWaveCallbackHandler _callbacks;
    private readonly TelegramAgentChatHandler _handler;
    private Guid _projectId = Guid.Empty;

    public TelegramWaveZeroModelContractTests()
    {
        _cache = new TelegramBotClientCache(new StubTelegramBotClientFactory(_api));
        (_wakeFanout, _wakeServices) = TestWakeFanout.Create(_temp);
        var sender = new TelegramReplySender(_cache, CreateOptionsProvider());
        var chat = new WaveChatService(_temp.CreateContext(), _wakeFanout, _broadcaster);
        var snapshots = new TelegramWaveSnapshotService(
            sender,
            new WaveSnapshotService(
                _temp.CreateContext(),
                new FeatureClaimService(_temp.CreateContext(), Dispatcher()),
                new DirectiveService(_temp.CreateContext())),
            chat,
            _temp.CreateContext(),
            new CollectingLogger<TelegramWaveSnapshotService>(_logs));
        var commands = new TelegramWaveCommandService(
            sender, _drafts, new TelegramWavePromptTombstone(TimeProvider.System), chat,
            _temp.CreateContext(), snapshots,
            new CollectingLogger<TelegramWaveCommandService>(_logs));
        _callbacks = new TelegramWaveCallbackHandler(
            CreateOptionsProvider(), _cache, commands, snapshots,
            new CollectingLogger<TelegramWaveCallbackHandler>(_logs));
        _handler = new TelegramAgentChatHandler(
            new TelegramChatStore(_temp.CreateContext()),
            new ConversationMessageStore(_temp.CreateContext()),
            _probe,
            _turns,
            sender,
            new TelegramNotificationDeliveryStore(_temp.CreateContext()),
            new NotificationsService(
                _temp.CreateContext(),
                new ReplyAutoActFanout(
                    _temp.CreateContext(), new ReplyAutoActQueue(), NullLogger<ReplyAutoActFanout>.Instance),
                _wakeFanout,
                []),
            _probe,
            commands,
            new CollectingLogger<TelegramAgentChatHandler>(_logs));
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
    public async Task TheWholeSendToWavePath_NeverOpensAModelTurn()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);

        await SayAsync("/send_to_wave");
        await PressAsync(11, $"wave:proj:{_projectId:D}");
        await PressAsync(12, TelegramWaveCallbackData.RoomData(_drafts.Current(OwnerChatId)!.Token, 0));
        var promptMessageId = _drafts.Current(OwnerChatId)!.PromptMessageId!.Value;
        await SayAsync(Signal, promptMessageId);

        _probe.AssertUntouched("/send_to_wave is a free channel");
        await ZeroModelProbe.AssertNoConversationFootprintAsync(_temp);
        (await WaveMessageCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task TheWholeWaveSnapshotPath_NeverOpensAModelTurn()
    {
        await SeedProjectAsync("Alpha", []);
        await SeedProjectAsync("Beta", []);

        await SayAsync("/wave_snapshot");
        await PressAsync(13, $"wave:sproj:{_projectId:D}");

        _probe.AssertUntouched("/wave_snapshot is a read, not a turn");
        await ZeroModelProbe.AssertNoConversationFootprintAsync(_temp);
        SentTexts().ShouldContain(text => text.StartsWith("Wave · Beta", StringComparison.Ordinal));
        (await WaveMessageCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AReplyToAClosedPrompt_NeverOpensAModelTurn()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await SayAsync("/send_to_wave");
        await PressAsync(11, $"wave:proj:{_projectId:D}");
        await PressAsync(12, TelegramWaveCallbackData.RoomData(_drafts.Current(OwnerChatId)!.Token, 0));
        var promptMessageId = _drafts.Current(OwnerChatId)!.PromptMessageId!.Value;
        await PressAsync(promptMessageId, TelegramWaveCallbackData.CancelData);

        await SayAsync("a signal the owner thought was still live", promptMessageId);

        _probe.AssertUntouched("a closed draft is refused, not paid for");
        await ZeroModelProbe.AssertNoConversationFootprintAsync(_temp);
        (await WaveMessageCountAsync()).ShouldBe(0);
        SentTexts().ShouldContain(TelegramWaveCommandService.DraftClosedReply);
    }

    [Fact]
    public async Task AnOrdinaryChatMessage_DoesOpenAModelTurn()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        _probe.Turn = [ConversationStreamEvent.Delta("agent answer"), ConversationStreamEvent.Completed(5)];

        await SayAsync("privet");

        _probe.StreamInvocations.ShouldBe(1);
        SentTexts().ShouldContain("agent answer");
        await ZeroModelProbe.AssertFootprintGrowsWhenTheModelRunsAsync(_temp);
    }

    public Task DisposeAsync()
    {
        _cache.Dispose();
        _wakeServices.Dispose();
        _temp.Dispose();
        return Task.CompletedTask;
    }

    private Task SayAsync(string text, long? repliedTo = null) =>
        _handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, text, repliedTo, "Owner"), CancellationToken.None);

    private Task PressAsync(long messageId, string data) =>
        _callbacks.HandleAsync(
            new TelegramCallback(OwnerChatId, $"cq-{messageId}", (int)messageId, "@owner", "picker", data),
            CancellationToken.None);

    private TelegramTransportOptionsProvider CreateOptionsProvider() =>
        new(new AppSettingsService(_temp.CreateContext()));

    private PlanningMaterializationDispatcher Dispatcher() =>
        TestPlanningDispatcher.Create(_temp.CreateContext(), Substitute.For<IPlanningMaterializer>());

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

    private async Task<int> WaveMessageCountAsync()
    {
        await using var context = _temp.CreateContext();
        return await context.WaveMessages.CountAsync();
    }

    private List<string> SentTexts()
    {
        lock (_api)
        {
            return [.. _api.Requests
                .Where(request => request.Path.Contains("/sendMessage", StringComparison.Ordinal))
                .Select(TextOf)];
        }
    }

    private static string TextOf(FakeTelegramApi.Captured captured)
    {
        using var document = JsonDocument.Parse(captured.Body);
        return document.RootElement.TryGetProperty("text", out var text)
            ? text.GetString() ?? string.Empty
            : string.Empty;
    }
}
