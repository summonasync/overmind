using System.Text.Json;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramWaveCallbackHandlerTests : IDisposable
{
    private const long OwnerChatId = 42;
    private const string TestToken = "123456:TESTSECRETTOKEN";

    private readonly TempDatabase _temp = new();
    private readonly FakeTelegramApi _api = new();
    private readonly TelegramBotClientCache _cache;
    private List<string> _logs = [];

    public TelegramWaveCallbackHandlerTests()
    {
        _cache = new TelegramBotClientCache(new StubTelegramBotClientFactory(_api));
    }

    [Theory]
    [InlineData("wave:cancel")]
    [InlineData("wave:proj:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111")]
    [InlineData("wave:room:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:telegram-bridge")]
    public async Task Handle_WellFormedData_AnswersNotWiredYetOnce(string data)
    {
        await ConfigureTransportAsync();

        await CreateHandler().HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-1", 5, "@owner", "picker", data), CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        _api.Request(0).Path.ShouldContain("answerCallbackQuery");
        AnswerText(_api.Request(0)).ShouldBe(TelegramWaveCallbackHandler.NotWiredYetAnswer);
    }

    [Theory]
    [InlineData("wave:")]
    [InlineData("wave:nonsense")]
    [InlineData("wave:proj:123")]
    public async Task Handle_GarbageData_AnswersInvalidButton(string data)
    {
        await ConfigureTransportAsync();

        await CreateHandler().HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-2", 5, "@owner", "picker", data), CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.InvalidButtonAnswer);
    }

    [Fact]
    public async Task Handle_ForeignChat_AnswersNotAllowed()
    {
        await ConfigureTransportAsync();

        await CreateHandler().HandleAsync(
            new TelegramCallback(999, "cq-3", 5, "@stranger", "picker", "wave:cancel"), CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.NotAllowedAnswer);
        LogSnapshot().ShouldContain(line => line.Contains("not allowlisted; denied"));
    }

    [Fact]
    public async Task Handle_TransportNotRunnable_AnswersNothing()
    {
        await CreateHandler().HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-4", 5, "@owner", "picker", "wave:cancel"), CancellationToken.None);

        _api.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_AnswerFailure_LogsAndNeverThrows()
    {
        await ConfigureTransportAsync();
        _api.FailPath(
            "answerCallbackQuery",
            """{"ok":false,"error_code":400,"description":"Bad Request: query too old"}""");

        await CreateHandler().HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-5", 5, "@owner", "picker", "wave:cancel"), CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        LogSnapshot().ShouldContain(line => line.Contains("Answering telegram wave callback cq-5 failed"));
    }

    public void Dispose()
    {
        _cache.Dispose();
        _temp.Dispose();
    }

    private TelegramWaveCallbackHandler CreateHandler()
    {
        _logs = [];
        return new TelegramWaveCallbackHandler(
            new TelegramTransportOptionsProvider(new AppSettingsService(_temp.CreateContext())),
            _cache,
            new CollectingLogger<TelegramWaveCallbackHandler>(_logs));
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
