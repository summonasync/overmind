using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Telegram;
using Overmind.Infrastructure.Wake;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramNotificationCallbackHandlerTests : IDisposable
{
    private const long OwnerChatId = 42;
    private const int ButtonMessageId = 5;
    private const string TestToken = "123456:TESTSECRETTOKEN";
    private const string OriginalText = "[approval] approve the plan";

    private readonly TempDatabase _temp = new();
    private readonly FakeTelegramApi _api = new();
    private readonly TelegramBotClientCache _cache;
    private List<string> _logs = [];

    public TelegramNotificationCallbackHandlerTests()
    {
        _cache = new TelegramBotClientCache(new StubTelegramBotClientFactory(_api));
    }

    [Theory]
    [InlineData("approve", "approved", "✅")]
    [InlineData("decline", "declined", "❌")]
    public async Task Handle_ValidPress_RecordsProvenanceAndEditsTheDecisionLine(
        string action, string decision, string marker)
    {
        await ConfigureTransportAsync();
        var notificationId = await SeedPendingNotificationAsync();
        var handler = CreateHandler();

        await handler.HandleAsync(
            new TelegramCallback(
                OwnerChatId, "cq-1", ButtonMessageId, "@owner", OriginalText,
                $"notif:{notificationId:D}:{action}"),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(2, customMessage: string.Join(" | ", _logs));
        _api.Request(0).Path.ShouldContain("answerCallbackQuery");
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.RecordedAnswer);
        _api.Request(1).Path.ShouldContain("editMessageText");

        using var edit = JsonDocument.Parse(_api.Request(1).Body);
        edit.RootElement.GetProperty("chat_id").GetInt64().ShouldBe(OwnerChatId);
        edit.RootElement.GetProperty("message_id").GetInt32().ShouldBe(ButtonMessageId);
        edit.RootElement.GetProperty("text").GetString()
            .ShouldBe($"{OriginalText}\n\n{marker} {decision} by @owner");
        edit.RootElement.TryGetProperty("reply_markup", out _).ShouldBeFalse();

        await using var context = _temp.CreateContext();
        var stored = await context.AgentNotifications.SingleAsync(n => n.Id == notificationId);
        stored.ReplyText.ShouldBe($"{decision} via telegram button by @owner");
        stored.RepliedAt.ShouldNotBeNull();
        stored.ReadAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Handle_SecondPress_AnswersAlreadyDecidedWithoutOverwrite()
    {
        await ConfigureTransportAsync();
        var notificationId = await SeedPendingNotificationAsync();
        var handler = CreateHandler();

        await handler.HandleAsync(
            new TelegramCallback(
                OwnerChatId, "cq-1", ButtonMessageId, "@owner", OriginalText,
                $"notif:{notificationId:D}:approve"),
            CancellationToken.None);
        await handler.HandleAsync(
            new TelegramCallback(
                OwnerChatId, "cq-2", ButtonMessageId, "@owner", OriginalText,
                $"notif:{notificationId:D}:decline"),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(3);
        _api.Request(2).Path.ShouldContain("answerCallbackQuery");
        AnswerText(_api.Request(2)).ShouldBe(TelegramNotificationCallbackHandler.AlreadyDecidedAnswer);
        _api.Requests.Count(captured => captured.Path.Contains("editMessageText")).ShouldBe(1);

        await using var context = _temp.CreateContext();
        var stored = await context.AgentNotifications.SingleAsync(n => n.Id == notificationId);
        stored.ReplyText.ShouldBe("approved via telegram button by @owner");
    }

    [Fact]
    public async Task Handle_ForeignChat_AnswersNotAllowedWithoutStateChange()
    {
        await ConfigureTransportAsync();
        var notificationId = await SeedPendingNotificationAsync();
        var handler = CreateHandler();

        await handler.HandleAsync(
            new TelegramCallback(
                999, "cq-9", ButtonMessageId, "@stranger", OriginalText,
                $"notif:{notificationId:D}:approve"),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        _api.Request(0).Path.ShouldContain("answerCallbackQuery");
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.NotAllowedAnswer);

        await using var context = _temp.CreateContext();
        var stored = await context.AgentNotifications.SingleAsync(n => n.Id == notificationId);
        stored.ReplyText.ShouldBeNull();
        stored.RepliedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("notif:123:approve")]
    [InlineData("notify:00000000-0000-0000-0000-000000000000:approve")]
    public async Task Handle_GarbageData_AnswersInvalidButtonWithoutStateChange(string data)
    {
        await ConfigureTransportAsync();
        var notificationId = await SeedPendingNotificationAsync();
        var handler = CreateHandler();

        await handler.HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-0", ButtonMessageId, "@owner", OriginalText, data),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        _api.Request(0).Path.ShouldContain("answerCallbackQuery");
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.InvalidButtonAnswer);

        await using var context = _temp.CreateContext();
        var stored = await context.AgentNotifications.SingleAsync(n => n.Id == notificationId);
        stored.ReplyText.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_ForeignNotificationId_AnswersInvalidButtonWithoutStateChange()
    {
        await ConfigureTransportAsync();
        await SeedPendingNotificationAsync();
        var handler = CreateHandler();
        var forgedId = Guid.NewGuid();

        await handler.HandleAsync(
            new TelegramCallback(
                OwnerChatId, "cq-4", ButtonMessageId, "@owner", OriginalText,
                $"notif:{forgedId:D}:approve"),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.InvalidButtonAnswer);

        await using var context = _temp.CreateContext();
        context.AgentNotifications.Single(n => n.Id != forgedId).ReplyText.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_EditFailure_KeepsTheDecisionAndNeverThrows()
    {
        await ConfigureTransportAsync();
        var notificationId = await SeedPendingNotificationAsync();
        var handler = CreateHandler();
        _api.FailPath(
            "editMessageText",
            """{"ok":false,"error_code":400,"description":"Bad Request: message to edit not found"}""");

        await handler.HandleAsync(
            new TelegramCallback(
                OwnerChatId, "cq-5", ButtonMessageId, "@owner", OriginalText,
                $"notif:{notificationId:D}:approve"),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(2);
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.RecordedAnswer);

        await using var context = _temp.CreateContext();
        var stored = await context.AgentNotifications.SingleAsync(n => n.Id == notificationId);
        stored.ReplyText.ShouldBe("approved via telegram button by @owner");
        LogSnapshot().ShouldContain(line => line.Contains("Editing telegram message 5"));
    }

    [Fact]
    public void HandlerDependencyGraph_CarriesNoAgentOrLlmSurface()
    {
        var namespaces = typeof(TelegramNotificationCallbackHandler)
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

    private TelegramNotificationCallbackHandler CreateHandler()
    {
        _logs = [];
        return new TelegramNotificationCallbackHandler(
            new TelegramTransportOptionsProvider(new AppSettingsService(_temp.CreateContext())),
            _cache,
            new TelegramNotificationDeliveryStore(_temp.CreateContext()),
            new NotificationsService(
                _temp.CreateContext(),
                new ReplyAutoActFanout(
                    _temp.CreateContext(), new ReplyAutoActQueue(), NullLogger<ReplyAutoActFanout>.Instance),
                TestWakeFanout.Create(_temp).Fanout,
                []),
            new CollectingLogger<TelegramNotificationCallbackHandler>(_logs));
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

    private string[] LogSnapshot()
    {
        lock (_logs)
        {
            return [.. _logs];
        }
    }
}
