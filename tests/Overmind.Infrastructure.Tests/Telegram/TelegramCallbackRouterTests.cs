using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Telegram;
using Overmind.Infrastructure.Wake;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramCallbackRouterTests : IDisposable
{
    private const long OwnerChatId = 42;
    private const int ButtonMessageId = 5;
    private const string TestToken = "123456:TESTSECRETTOKEN";
    private const string OriginalText = "[approval] approve the plan";
    private const string Project = "1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111";

    private readonly TempDatabase _temp = new();
    private readonly FakeTelegramApi _api = new();
    private readonly TelegramBotClientCache _cache;
    private List<string> _logs = [];

    public TelegramCallbackRouterTests()
    {
        _cache = new TelegramBotClientCache(new StubTelegramBotClientFactory(_api));
    }

    [Fact]
    public async Task Handle_NotificationData_RunsTheNotificationHandlerUnchanged()
    {
        await ConfigureTransportAsync();
        var notificationId = await SeedPendingNotificationAsync();

        await CreateRouter().HandleAsync(
            new TelegramCallback(
                OwnerChatId, "cq-1", ButtonMessageId, "@owner", OriginalText, $"notif:{notificationId:D}:approve"),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(2);
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.RecordedAnswer);
        _api.Request(1).Path.ShouldContain("editMessageText");

        await using var context = _temp.CreateContext();
        var stored = await context.AgentNotifications.SingleAsync(n => n.Id == notificationId);
        stored.ReplyText.ShouldBe("approved via telegram button by @owner");
        stored.RepliedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Handle_WaveData_RunsTheWaveHandler()
    {
        await ConfigureTransportAsync();

        await CreateRouter().HandleAsync(
            new TelegramCallback(
                OwnerChatId, "cq-2", ButtonMessageId, "@owner", "wave picker", "wave:cancel"),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        AnswerText(_api.Request(0)).ShouldBe(TelegramWaveCommandService.CancelAnswer);
    }

    [Fact]
    public async Task Handle_GarbageData_AnswersInvalidButtonThroughTheNotificationDoor()
    {
        await ConfigureTransportAsync();
        await SeedPendingNotificationAsync();

        await CreateRouter().HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-3", ButtonMessageId, "@owner", OriginalText, "garbage"),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.InvalidButtonAnswer);

        await using var context = _temp.CreateContext();
        context.AgentNotifications.Single().RepliedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_MalformedWaveData_AnswersInvalidButtonWithoutTouchingNotifications()
    {
        await ConfigureTransportAsync();
        await SeedPendingNotificationAsync();

        await CreateRouter().HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-4", ButtonMessageId, "@owner", OriginalText, "wave:proj:123"),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.InvalidButtonAnswer);

        await using var context = _temp.CreateContext();
        context.AgentNotifications.Single().RepliedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData($"notif:{Project}:approve")]
    [InlineData("wave:cancel")]
    public async Task Handle_ForeignChat_AnswersNotAllowedWithoutStateChange(string data)
    {
        await ConfigureTransportAsync();
        await SeedPendingNotificationAsync();

        await CreateRouter().HandleAsync(
            new TelegramCallback(999, "cq-5", ButtonMessageId, "@stranger", OriginalText, data),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.NotAllowedAnswer);

        await using var context = _temp.CreateContext();
        context.AgentNotifications.Single().RepliedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData(typeof(TelegramCallbackRouter))]
    [InlineData(typeof(TelegramWaveCallbackHandler))]
    [InlineData(typeof(TelegramWaveCommandService))]
    public void WaveDoorDependencyGraph_CarriesNoAgentOrLlmSurface(Type door)
    {
        var namespaces = door
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType.Namespace)
            .ToArray();

        namespaces.ShouldNotContain(ns =>
            ns!.StartsWith("Overmind.Infrastructure.Agent") || ns!.StartsWith("Overmind.Infrastructure.Llm"));
    }

    public void Dispose()
    {
        _cache.Dispose();
        _temp.Dispose();
    }

    private TelegramCallbackRouter CreateRouter()
    {
        _logs = [];
        var optionsProvider = new TelegramTransportOptionsProvider(new AppSettingsService(_temp.CreateContext()));
        var notifications = new TelegramNotificationCallbackHandler(
            optionsProvider,
            _cache,
            new TelegramNotificationDeliveryStore(_temp.CreateContext()),
            new NotificationsService(
                _temp.CreateContext(),
                new ReplyAutoActFanout(
                    _temp.CreateContext(), new ReplyAutoActQueue(), NullLogger<ReplyAutoActFanout>.Instance),
                TestWakeFanout.Create(_temp).Fanout,
                []),
            new CollectingLogger<TelegramNotificationCallbackHandler>(_logs));
        var waveCommands = Substitute.For<ITelegramWaveCommandService>();
        waveCommands.Cancel(Arg.Any<long>()).Returns(TelegramWaveCommandService.CancelAnswer);
        var waves = new TelegramWaveCallbackHandler(
            optionsProvider,
            _cache,
            waveCommands,
            Substitute.For<ITelegramWaveSnapshotService>(),
            new CollectingLogger<TelegramWaveCallbackHandler>(_logs));

        return new TelegramCallbackRouter(notifications, waves);
    }

    private async Task<Guid> SeedPendingNotificationAsync()
    {
        var notification = AgentNotification.Create(
            NotificationKind.Approval, "approve the plan", null, null, null);
        await using var context = _temp.CreateContext();
        context.AgentNotifications.Add(notification);
        await context.SaveChangesAsync();
        await new TelegramNotificationDeliveryStore(context).SaveAsync(OwnerChatId, ButtonMessageId, notification.Id);
        return notification.Id;
    }

    private async Task ConfigureTransportAsync()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.TelegramEnabled, "true", CancellationToken.None);
        await settings.SetAsync(SettingKeys.TelegramAllowedChatIds, "42", CancellationToken.None);
        await settings.SetAsync(SettingKeys.TelegramBotToken, TestToken, CancellationToken.None);
    }

    private string AnswerText(FakeTelegramApi.Captured captured)
    {
        using var document = JsonDocument.Parse(captured.Body);
        return document.RootElement.TryGetProperty("text", out var text)
            ? text.GetString() ?? string.Empty
            : string.Empty;
    }
}
