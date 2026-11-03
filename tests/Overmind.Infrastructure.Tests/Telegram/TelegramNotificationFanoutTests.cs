using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramNotificationFanoutTests : IDisposable
{
    private const long OwnerChatId = 42;

    private readonly TempDatabase _temp = new();
    private readonly FakeTelegramApi _api = new();
    private readonly TelegramBotClientCache _cache;
    private readonly List<IDisposable> _disposables = [];
    private List<string> _logs = [];
    private static readonly Uri Origin = new("http://127.0.0.1:5099/");

    public TelegramNotificationFanoutTests()
    {
        _cache = new TelegramBotClientCache(new StubTelegramBotClientFactory(_api));
    }

    [Fact]
    public async Task TryDeliver_SendsKindTextAndDeepLinkToEveryAllowlistedChat()
    {
        await ConfigureTransportAsync("42,43");
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse(messageId: 11));
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse(messageId: 12));
        var fanout = CreateFanout();
        var notification = await SaveNotificationAsync(
            NotificationKind.Approval, "approve the plan", "159-telegram-connector", "spec");

        await fanout.TryDeliverAsync(notification, CancellationToken.None);

        var texts = SentTexts();
        texts.Length.ShouldBe(2);
        texts.ShouldAllBe(text =>
            text.StartsWith("[approval] approve the plan")
            && text.Contains($"http://127.0.0.1:5099/dashboard?feature=159-telegram-connector&tab=spec"));

        await using var context = _temp.CreateContext();
        var mappings = await context.TelegramNotificationDeliveries.ToListAsync();
        mappings.Count.ShouldBe(2);
        mappings.Select(row => row.MessageId).ShouldBe([11L, 12L], true);
        mappings.ShouldAllBe(row => row.NotificationId == notification.Id && (row.ChatId == 42L || row.ChatId == 43L));
    }

    [Fact]
    public async Task TryDeliver_NotRunnable_SendsNothing()
    {
        await ConfigureTransportAsync("42", enabled: false);
        var fanout = CreateFanout();

        await fanout.TryDeliverAsync(
            await SaveNotificationAsync(NotificationKind.Info, "quiet", null, null), CancellationToken.None);

        _api.RequestCount.ShouldBe(0);
        await using var context = _temp.CreateContext();
        context.TelegramNotificationDeliveries.Count().ShouldBe(0);
    }

    [Fact]
    public async Task TryDeliver_WithoutFeatureSlug_SendsTextWithoutLink()
    {
        await ConfigureTransportAsync("42");
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var fanout = CreateFanout();

        await fanout.TryDeliverAsync(
            await SaveNotificationAsync(NotificationKind.Question, "which shape?", null, null),
            CancellationToken.None);

        SentTexts().ShouldBe(["[question] which shape?"]);
    }

    [Fact]
    public async Task TryDeliver_SendFailure_LogsAndKeepsTheDeliveredMappings()
    {
        await ConfigureTransportAsync("42,43");
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse(messageId: 21));
        _api.QueueResponse("""{"ok":false,"error_code":500,"description":"second chat broke"}""");
        var fanout = CreateFanout();
        await fanout.TryDeliverAsync(
            await SaveNotificationAsync(NotificationKind.Info, "fan out", null, null), CancellationToken.None);

        _api.RequestCount.ShouldBe(2, customMessage: string.Join(" | ", _logs));
        await using var context = _temp.CreateContext();
        var mappings = await context.TelegramNotificationDeliveries.ToListAsync();
        mappings.Select(row => row.ChatId).ShouldBe([42L]);
    }

    [Fact]
    public async Task TryDeliver_FirstChatFailure_StillDeliversTheRemainingChats()
    {
        await ConfigureTransportAsync("42,43");
        _api.QueueResponse("""{"ok":false,"error_code":500,"description":"first chat broke"}""");
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse(messageId: 31));
        var fanout = CreateFanout();
        var notification = await SaveNotificationAsync(NotificationKind.Info, "fan out", null, null);

        await fanout.TryDeliverAsync(notification, CancellationToken.None);

        _api.RequestCount.ShouldBe(2);
        await using var context = _temp.CreateContext();
        var mappings = await context.TelegramNotificationDeliveries.ToListAsync();
        mappings.Select(row => row.ChatId).ShouldBe([43L]);
        mappings.ShouldAllBe(row => row.MessageId == 31L);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _cache.Dispose();
        _temp.Dispose();
    }

    private TelegramNotificationFanout CreateFanout()
    {
        var provider = new ServiceCollection()
            .AddScoped<OvermindDbContext>(_ => _temp.CreateContext())
            .AddScoped<AppSettingsService>()
            .AddScoped<TelegramTransportOptionsProvider>()
            .AddSingleton(_cache)
            .AddSingleton<TelegramReplySender>()
            .AddScoped<TelegramNotificationDeliveryStore>()
            .BuildServiceProvider();
        _disposables.Add(provider);
        _logs = [];

        return new TelegramNotificationFanout(
            provider.GetRequiredService<IServiceScopeFactory>(),
            () => Origin,
            new CollectingLogger<TelegramNotificationFanout>(_logs));
    }

    private async Task<AgentNotification> SaveNotificationAsync(
        NotificationKind kind, string text, string? featureSlug, string? tab)
    {
        await using var context = _temp.CreateContext();
        var notification = AgentNotification.Create(kind, text, null, featureSlug, tab);
        context.AgentNotifications.Add(notification);
        await context.SaveChangesAsync();
        return notification;
    }

    private async Task ConfigureTransportAsync(string allowlist, bool enabled = true)
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.TelegramEnabled, enabled ? "true" : "false", CancellationToken.None);
        await settings.SetAsync(SettingKeys.TelegramAllowedChatIds, allowlist, CancellationToken.None);
        await settings.SetAsync(SettingKeys.TelegramBotToken, "123456:TESTSECRETTOKEN", CancellationToken.None);
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
        using var document = System.Text.Json.JsonDocument.Parse(captured.Body);
        return document.RootElement.TryGetProperty("text", out var text)
            ? text.GetString() ?? string.Empty
            : string.Empty;
    }
}
