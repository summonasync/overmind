using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Overmind.Infrastructure.Telegram;
using Overmind.Infrastructure.Wake;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramNotificationBridgeTests : IDisposable
{
    private const long OwnerChatId = 42;
    private const string AgentAlias = "van-darkholme";
    private const string FeatureSlug = "bridge-tests";

    private readonly TempDatabase _temp = new();
    private readonly OvermindDbContext _context;
    private readonly FakeTelegramApi _api = new();
    private readonly TelegramBotClientCache _cache;
    private readonly ReplyAutoActQueue _autoActQueue = new();
    private readonly List<IDisposable> _disposables = [];
    private IEnumerable<INotificationDeliveryChannel> _deliveryChannels = [];
    private static readonly Uri Origin = new("http://127.0.0.1:5099/");

    public TelegramNotificationBridgeTests()
    {
        _cache = new TelegramBotClientCache(new StubTelegramBotClientFactory(_api));
        _context = _temp.CreateContext();
    }

    [Fact]
    public async Task GateNotification_DeliversDeepLinkAndReplyBridgesToAutoAct()
    {
        await ConfigureTransportAsync();
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse(messageId: 5));
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse(messageId: 6));
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse(messageId: 7));

        var notifications = CreateNotifications();
        var projectId = await CreateProjectAsync();
        var feature = await CreateFeatureAsync(projectId, FeatureSlug);
        await CreateArtifactAsync(feature.Id, PlanningArtifactKind.Spec, "bridge spec body");
        await SetAutoActAsync(projectId, true);
        var handler = CreateHandler();

        var notification = await notifications.CreateAsync(
            NotificationKind.Approval, $"approve {FeatureSlug}", projectId, FeatureSlug, "spec", AgentAlias,
            CancellationToken.None);
        notification.ShouldNotBeNull();
        (await WaitForDeliveryAsync(notification.Id)).ShouldBeTrue();

        await handler.Handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "approve", RepliedToMessageId: 5), CancellationToken.None);

        var stored = await _context.AgentNotifications.SingleAsync(n => n.Id == notification.Id);
        stored.ReplyText.ShouldBe("approve");
        stored.RepliedAt.ShouldNotBeNull();
        SentTexts().ShouldContain(text => text == TelegramAgentChatHandler.ReplyRegisteredAck);
        _autoActQueue.PendingCount.ShouldBe(1);

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeTrue();
        var validated = await _context.PlanningArtifacts.SingleAsync(
            a => a.FeatureId == feature.Id && a.Kind == PlanningArtifactKind.Spec);
        validated.Status.ShouldBe(PlanningArtifactStatus.Validated);
        validated.Body.ShouldContain($"[auto-act] by human reply {notification.Id}");

        var outcomes = await _context.AgentNotifications.Where(n => n.Kind == NotificationKind.Info).ToListAsync();
        outcomes.ShouldContain(row => row.Text.Contains("validated by your reply"));
        (await WaitForSentTextAsync(text => text.Contains("[info] auto-act:"))).ShouldBeTrue();
    }

    [Fact]
    public async Task ReplyToUnknownBotMessage_FallsThroughToTheChatTurn()
    {
        await ConfigureTransportAsync();
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse(messageId: 5));
        var handler = CreateHandler();

        await handler.Handler.HandleAsync(
            new TelegramIncomingMessage(OwnerChatId, "hello there", RepliedToMessageId: 999), CancellationToken.None);

        handler.Stream.Invocations.ShouldBe(1);
        var roles = await _context.ConversationMessages
            .Where(row => row.Role == ConversationMessage.UserRole)
            .Select(row => row.Role)
            .ToListAsync();
        roles.ShouldBe([ConversationMessage.UserRole]);
    }

    [Fact]
    public async Task Store_MapsBotMessageToNotification_UntilTheNotificationIsDeleted()
    {
        var store = new TelegramNotificationDeliveryStore(_context);
        var notification = AgentNotification.Create(NotificationKind.Info, "kept", null, null, null);
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();
        await store.SaveAsync(OwnerChatId, 77, notification.Id, CancellationToken.None);

        (await store.FindNotificationIdAsync(OwnerChatId, 77, CancellationToken.None)).ShouldBe(notification.Id);
        (await store.FindNotificationIdAsync(OwnerChatId, 78, CancellationToken.None)).ShouldBeNull();

        await _context.AgentNotifications
            .Where(n => n.Id == notification.Id)
            .ExecuteDeleteAsync(CancellationToken.None);

        (await store.FindNotificationIdAsync(OwnerChatId, 77, CancellationToken.None)).ShouldBeNull();
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _cache.Dispose();
        _context.Dispose();
        _temp.Dispose();
    }

    private NotificationsService CreateNotifications()
    {
        var services = new ServiceCollection();
        services.AddScoped<OvermindDbContext>(_ => _temp.CreateContext());
        services.AddScoped<AppSettingsService>();
        services.AddScoped<TelegramTransportOptionsProvider>();
        services.AddSingleton(_cache);
        services.AddSingleton<TelegramReplySender>();
        services.AddScoped<TelegramNotificationDeliveryStore>();
        services.AddSingleton<INotificationDeliveryChannel>(provider => new TelegramNotificationFanout(
            provider.GetRequiredService<IServiceScopeFactory>(),
            () => Origin,
            NullLogger<TelegramNotificationFanout>.Instance));
        services.AddSingleton(new ReplyAutoActFanout(_context, _autoActQueue, NullLogger<ReplyAutoActFanout>.Instance));
        services.AddSingleton(sp => new WakeDropFanout(
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WakeDropFanout>.Instance));
        services.AddScoped<NotificationsService>();

        var provider = services.BuildServiceProvider();
        _disposables.Add(provider);
        _deliveryChannels = provider.GetRequiredService<IEnumerable<INotificationDeliveryChannel>>();
        return provider.GetRequiredService<NotificationsService>();
    }

    private HandlerRig CreateHandler()
    {
        var provider = new ServiceCollection()
            .AddScoped<OvermindDbContext>(_ => _temp.CreateContext())
            .AddScoped<AppSettingsService>()
            .AddScoped<TelegramTransportOptionsProvider>()
            .AddSingleton(_cache)
            .AddSingleton<TelegramReplySender>()
            .AddScoped<TelegramNotificationDeliveryStore>()
            .AddScoped<TelegramChatStore>()
            .AddScoped<ConversationMessageStore>()
            .AddSingleton<ConversationTurnGate>()
            .AddSingleton(new ReplyAutoActFanout(_context, _autoActQueue, NullLogger<ReplyAutoActFanout>.Instance))
            .AddSingleton(sp => new WakeDropFanout(
                sp.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<WakeDropFanout>.Instance))
            .AddScoped<NotificationsService>()
            .BuildServiceProvider();
        _disposables.Add(provider);

        var scoped = provider.CreateScope();
        _disposables.Add(scoped);
        var logs = new List<string>();
        var stream = new RecordingStreamSource();
        var handler = new TelegramAgentChatHandler(
            scoped.ServiceProvider.GetRequiredService<TelegramChatStore>(),
            scoped.ServiceProvider.GetRequiredService<ConversationMessageStore>(),
            stream,
            scoped.ServiceProvider.GetRequiredService<ConversationTurnGate>(),
            scoped.ServiceProvider.GetRequiredService<TelegramReplySender>(),
            scoped.ServiceProvider.GetRequiredService<TelegramNotificationDeliveryStore>(),
            scoped.ServiceProvider.GetRequiredService<NotificationsService>(),
            new CollectingLogger<TelegramAgentChatHandler>(logs));

        return new HandlerRig(handler, stream, logs);
    }

    private ReplyAutoActProcessor CreateProcessor()
    {
        var materializer = Substitute.For<IPlanningMaterializer>();
        materializer
            .SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], [], []));

        var dispatcher = TestPlanningDispatcher.Create(_context, materializer);
        return new ReplyAutoActProcessor(
            _context,
            new NotificationsService(
                _context,
                new ReplyAutoActFanout(_context, _autoActQueue, NullLogger<ReplyAutoActFanout>.Instance),
                new WakeDropFanout(
                    new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                    NullLogger<WakeDropFanout>.Instance),
                _deliveryChannels),
            new PlanningArtifactService(
                _context, dispatcher, new TaskDependencyService(_context, dispatcher)),
            new PlanningFeatureService(
                _context,
                materializer,
                new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
                new TaskDependencyService(_context, dispatcher),
                dispatcher,
                NullLogger<PlanningFeatureService>.Instance),
            new FeatureDependencyService(_context, dispatcher),
            NullLogger<ReplyAutoActProcessor>.Instance);
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var project = Project.Create($"bridge-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        return project.Id;
    }

    private async Task<PlanningFeature> CreateFeatureAsync(Guid projectId, string slug)
    {
        var feature = PlanningFeature.Create(projectId, slug, slug, string.Empty);
        _context.PlanningFeatures.Add(feature);
        await _context.SaveChangesAsync();
        return feature;
    }

    private async Task<PlanningArtifact> CreateArtifactAsync(
        Guid featureId, PlanningArtifactKind kind, string body)
    {
        var artifact = PlanningArtifact.Create(featureId, kind, body);
        _context.PlanningArtifacts.Add(artifact);
        await _context.SaveChangesAsync();
        return artifact;
    }

    private async Task SetAutoActAsync(Guid projectId, bool enabled)
    {
        var project = await _context.Projects.SingleAsync(p => p.Id == projectId);
        project.SetAutoActOnReplies(enabled);
        await _context.SaveChangesAsync();
    }

    private async Task ConfigureTransportAsync()
    {
        var settings = new AppSettingsService(_temp.CreateContext());
        await settings.SetAsync(SettingKeys.TelegramEnabled, "true", CancellationToken.None);
        await settings.SetAsync(SettingKeys.TelegramAllowedChatIds, "42", CancellationToken.None);
        await settings.SetAsync(SettingKeys.TelegramBotToken, "123456:TESTSECRETTOKEN", CancellationToken.None);
    }

    private async Task<bool> WaitForDeliveryAsync(Guid notificationId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var mapped = await _context.TelegramNotificationDeliveries
                .AsNoTracking()
                .AnyAsync(d => d.NotificationId == notificationId);
            if (mapped)
            {
                return true;
            }

            await Task.Delay(20);
        }

        return false;
    }

    private async Task<bool> WaitForSentTextAsync(Func<string, bool> predicate)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (SentTexts().Any(predicate))
            {
                return true;
            }

            await Task.Delay(20);
        }

        return false;
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
        RecordingStreamSource Stream,
        List<string> Logs);

    private sealed class RecordingStreamSource : IConversationStreamSource
    {
        public int Invocations { get; private set; }

        public async IAsyncEnumerable<ConversationStreamEvent> StreamAsync(
            Conversation conversation,
            IReadOnlyList<ConversationMessage> history,
            string userText,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default,
            RoutingMissContext? missContext = null)
        {
            Invocations++;
            await Task.Yield();
            yield return ConversationStreamEvent.Completed();
        }
    }
}
