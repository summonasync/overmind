using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Telegram;
using Telegram.Bot;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramPollingRunnerTests : IDisposable
{
    private const string TestToken = "123456:TESTSECRETTOKEN";

    private readonly TempDatabase _temp = new();

    [Fact]
    public async Task RunAsync_Disabled_DoesNotCallApi()
    {
        var rig = CreateRig();

        var (run, cts) = StartRig(rig);
        try
        {
            await Task.Delay(300);
            rig.Api.RequestCount.ShouldBe(0);
        }
        finally
        {
            await StopRig(run, cts);
        }
    }

    [Fact]
    public async Task RunAsync_EnabledWithoutHandler_DoesNotCallApiAndWarns()
    {
        var rig = CreateRig(wireHandler: false);
        await ConfigureAsync(enabled: true, allowlist: "42");

        var (run, cts) = StartRig(rig);
        try
        {
            await Task.Delay(300);
            rig.Api.RequestCount.ShouldBe(0);
            rig.LogSnapshot().ShouldContain(line => line.Contains("no message handler is wired"));
        }
        finally
        {
            await StopRig(run, cts);
        }
    }

    [Fact]
    public async Task RunAsync_AllowlistedTextMessage_IsDispatchedToHandler()
    {
        var rig = CreateRig();
        await ConfigureAsync(enabled: true, allowlist: "42");
        rig.Api.QueueUpdate(FakeTelegramApi.TextUpdate(42, "privet", 1));

        var (run, cts) = StartRig(rig);
        try
        {
            (await rig.Handler.Received.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();

            var message = rig.Handler.Messages.Single();
            message.ChatId.ShouldBe(42L);
            message.Text.ShouldBe("privet");
        }
        finally
        {
            await StopRig(run, cts);
        }
    }

    [Fact]
    public async Task RunAsync_NonAllowlistedChat_IsIgnoredButConfirmed()
    {
        var rig = CreateRig();
        await ConfigureAsync(enabled: true, allowlist: "42");
        rig.Api.QueueUpdate(FakeTelegramApi.TextUpdate(999, "stranger", 7));

        var (run, cts) = StartRig(rig);
        try
        {
            (await rig.Api.RequestArrived.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
            (await rig.Api.RequestArrived.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();

            rig.Handler.Messages.ShouldBeEmpty();
            OffsetOf(rig.Api.Request(0)).ShouldBeNull();
            OffsetOf(rig.Api.Request(1)).ShouldBe(8);
        }
        finally
        {
            await StopRig(run, cts);
        }
    }

    [Fact]
    public async Task RunAsync_TextlessMessage_IsIgnoredButConfirmed()
    {
        var rig = CreateRig();
        await ConfigureAsync(enabled: true, allowlist: "42");
        rig.Api.QueueUpdate(FakeTelegramApi.TextlessUpdate(42, 3));

        var (run, cts) = StartRig(rig);
        try
        {
            (await rig.Api.RequestArrived.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
            (await rig.Api.RequestArrived.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();

            rig.Handler.Messages.ShouldBeEmpty();
            OffsetOf(rig.Api.Request(1)).ShouldBe(4);
        }
        finally
        {
            await StopRig(run, cts);
        }
    }

    [Fact]
    public async Task RunAsync_ReplyToBotMessage_CarriesRepliedToMessageId()
    {
        var rig = CreateRig();
        await ConfigureAsync(enabled: true, allowlist: "42");
        rig.Api.QueueUpdate(FakeTelegramApi.ReplyTextUpdate(42, "approve", 9, 5));

        var (run, cts) = StartRig(rig);
        try
        {
            (await rig.Handler.Received.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();

            var message = rig.Handler.Messages.Single();
            message.RepliedToMessageId.ShouldBe(5L);
        }
        finally
        {
            await StopRig(run, cts);
        }
    }

    [Fact]
    public async Task RunAsync_ConfirmedUpdatesAreNotRedelivered()
    {
        var rig = CreateRig();
        await ConfigureAsync(enabled: true, allowlist: "42");
        rig.Api.QueueUpdate(FakeTelegramApi.TextUpdate(42, "once", 1));

        var (run, cts) = StartRig(rig);
        try
        {
            (await rig.Handler.Received.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
            await Task.Delay(300);

            rig.Handler.Messages.Count.ShouldBe(1);
            OffsetOf(rig.Api.Request(1)).ShouldBe(2);
        }
        finally
        {
            await StopRig(run, cts);
        }
    }

    [Fact]
    public async Task RunAsync_CancellationStopsTheLoopPromptly()
    {
        var rig = CreateRig();
        await ConfigureAsync(enabled: true, allowlist: "42");

        var (run, cts) = StartRig(rig);
        try
        {
            (await rig.Api.RequestArrived.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
        }
        finally
        {
            await StopRig(run, cts);
        }

        run.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_NeverLogsTheBotToken()
    {
        var rig = CreateRig();
        await ConfigureAsync(enabled: true, allowlist: "42");
        rig.Api.QueueResponse("""{"ok":false,"error_code":401,"description":"Unauthorized"}""");
        rig.Api.QueueUpdate(FakeTelegramApi.TextUpdate(42, "privet", 1));

        var (run, cts) = StartRig(rig);
        try
        {
            (await rig.Api.RequestArrived.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
            (await rig.Api.RequestArrived.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
        }
        finally
        {
            await StopRig(run, cts);
        }

        rig.LogSnapshot().ShouldNotContain(line => line.Contains("TESTSECRETTOKEN"));
    }

    [Fact]
    public async Task RunAsync_TokenChange_RecreatesClient()
    {
        var rig = CreateRig();
        await ConfigureAsync(enabled: true, allowlist: "42");

        var (run, cts) = StartRig(rig);
        try
        {
            (await rig.Api.RequestArrived.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
            rig.Factory.CreatedCount.ShouldBe(1);

            await ConfigureAsync(enabled: true, allowlist: "42", token: "654321:OTHERSECRETTOKEN");
            (await WaitForAsync(() => rig.Factory.CreatedCount == 2)).ShouldBeTrue();
        }
        finally
        {
            await StopRig(run, cts);
        }
    }

    [Fact]
    public async Task RunAsync_MalformedToken_DoesNotCrashTheLoop()
    {
        var rig = CreateRig();
        await ConfigureAsync(enabled: true, allowlist: "42", token: "not-a-token");

        var (run, cts) = StartRig(rig);
        try
        {
            await Task.Delay(300);
            rig.Api.RequestCount.ShouldBe(0);
            rig.LogSnapshot().ShouldContain(line => line.Contains("polling cycle failed"));
            rig.LogSnapshot().ShouldNotContain(line => line.Contains("not-a-token"));
        }
        finally
        {
            await StopRig(run, cts);
        }
    }

    [Fact]
    public async Task RunAsync_ThrowingHandler_KeepsPolling()
    {
        var rig = CreateRig(wireHandler: false, throwingHandler: true);
        await ConfigureAsync(enabled: true, allowlist: "42");
        rig.Api.QueueUpdate(FakeTelegramApi.TextUpdate(42, "first", 1));
        rig.Api.QueueUpdate(FakeTelegramApi.TextUpdate(42, "second", 2));

        var (run, cts) = StartRig(rig);
        try
        {
            (await rig.ThrowingHandler!.Invocations.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
            (await rig.ThrowingHandler.Invocations.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
        }
        finally
        {
            await StopRig(run, cts);
        }
    }

    public void Dispose() => _temp.Dispose();

    private static async Task<bool> WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(20);
        }

        return condition();
    }

    private static int? OffsetOf(FakeTelegramApi.Captured captured)
    {
        var match = Regex.Match(captured.Query + " " + captured.Body, "offset\"?\\s*[:=]\\s*(\\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private Rig CreateRig(bool wireHandler = true, bool throwingHandler = false)
    {
        var api = new FakeTelegramApi();
        var factory = new StubTelegramBotClientFactory(api);
        var messages = new RecordingTelegramMessageHandler();
        var throwing = new ThrowingTelegramMessageHandler();
        var logs = new List<string>();

        var services = new ServiceCollection();
        services.AddScoped<OvermindDbContext>(_ => _temp.CreateContext());
        services.AddScoped<AppSettingsService>();
        services.AddScoped<TelegramTransportOptionsProvider>();
        services.AddSingleton<ITelegramBotClientFactory>(factory);
        if (wireHandler)
        {
            services.AddSingleton<ITelegramMessageHandler>(messages);
        }
        else if (throwingHandler)
        {
            services.AddSingleton<ITelegramMessageHandler>(throwing);
        }

        services.AddSingleton<ILogger<TelegramPollingRunner>>(new CollectingLogger<TelegramPollingRunner>(logs));

        var provider = services.BuildServiceProvider();
        var runner = new TelegramPollingRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new TelegramBotClientCache(factory),
            provider.GetRequiredService<ILogger<TelegramPollingRunner>>());

        return new Rig(runner, api, messages, throwingHandler ? throwing : null, factory, provider, logs);
    }

    private async Task ConfigureAsync(bool enabled, string allowlist, string? token = TestToken)
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.TelegramEnabled, enabled ? "true" : "false", CancellationToken.None);
        await settings.SetAsync(SettingKeys.TelegramAllowedChatIds, allowlist, CancellationToken.None);
        if (token is not null)
        {
            await settings.SetAsync(SettingKeys.TelegramBotToken, token, CancellationToken.None);
        }
    }

    private static (Task Run, CancellationTokenSource Cts) StartRig(Rig rig)
    {
        var cts = new CancellationTokenSource();
        return (rig.Runner.RunAsync(cts.Token), cts);
    }

    private static async Task StopRig(Task run, CancellationTokenSource cts)
    {
        cts.Cancel();
        await run;
        cts.Dispose();
    }

    private sealed record Rig(
        TelegramPollingRunner Runner,
        FakeTelegramApi Api,
        RecordingTelegramMessageHandler Handler,
        ThrowingTelegramMessageHandler? ThrowingHandler,
        StubTelegramBotClientFactory Factory,
        ServiceProvider Provider,
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

    private sealed class RecordingTelegramMessageHandler : ITelegramMessageHandler
    {
        public ConcurrentQueue<TelegramIncomingMessage> Messages { get; } = new();
        public SemaphoreSlim Received { get; } = new(0);

        public Task HandleAsync(TelegramIncomingMessage message, CancellationToken ct)
        {
            Messages.Enqueue(message);
            Received.Release();
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingTelegramMessageHandler : ITelegramMessageHandler
    {
        public SemaphoreSlim Invocations { get; } = new(0);

        public Task HandleAsync(TelegramIncomingMessage message, CancellationToken ct)
        {
            Invocations.Release();
            throw new InvalidOperationException("the handler is broken on purpose");
        }
    }
}
