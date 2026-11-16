using System.Text.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
    private readonly ITelegramWaveCommandService _commands = Substitute.For<ITelegramWaveCommandService>();
    private readonly ITelegramWaveSnapshotService _snapshot = Substitute.For<ITelegramWaveSnapshotService>();
    private List<string> _logs = [];

    public TelegramWaveCallbackHandlerTests()
    {
        _cache = new TelegramBotClientCache(new StubTelegramBotClientFactory(_api));
    }

    [Theory]
    [InlineData("wave:")]
    [InlineData("wave:nonsense")]
    [InlineData("wave:proj:123")]
    [InlineData("wave:room:1f0aa0e0:1:extra")]
    public async Task Handle_GarbageData_AnswersInvalidButton(string data)
    {
        await ConfigureTransportAsync();

        await CreateHandler().HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-2", 5, "@owner", "picker", data), CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.InvalidButtonAnswer);
    }

    [Fact]
    public async Task Handle_ForeignChat_AnswersNotAllowedAndNeverReachesTheFlow()
    {
        await ConfigureTransportAsync();

        await CreateHandler().HandleAsync(
            new TelegramCallback(999, "cq-3", 5, "@stranger", "picker", "wave:cancel"), CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.NotAllowedAnswer);
        _logs.ShouldContain(line => line.Contains("not allowlisted; denied"));
    }

    [Fact]
    public async Task Handle_TransportNotRunnable_AnswersNothing()
    {
        await CreateHandler().HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-4", 5, "@owner", "picker", "wave:cancel"), CancellationToken.None);

        _api.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_FlowThrows_AnswersFailureNoticeOnce()
    {
        await ConfigureTransportAsync();
        _commands.Cancel(OwnerChatId).Throws(new InvalidOperationException("boom"));

        await CreateHandler().HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-6", 5, "@owner", "picker", "wave:cancel"), CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.FailureAnswer);
        _logs.ShouldContain(line => line.Contains("failed; answering with the failure notice"));
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
        _logs.ShouldContain(line => line.Contains("Answering telegram wave callback cq-5 failed"));
    }

    [Fact]
    public async Task Handle_SnapshotProjectPress_RoutesToTheSnapshotDoorNotTheSendFlow()
    {
        await ConfigureTransportAsync();
        var projectId = Guid.NewGuid();
        _snapshot.PickProjectAsync(OwnerChatId, projectId, Arg.Any<CancellationToken>())
            .Returns(TelegramWaveSnapshotService.SnapshotAnswer);

        await CreateHandler().HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-s1", 5, "@owner", "picker",
                $"wave:sproj:{projectId:D}"),
            CancellationToken.None);

        await _snapshot.Received(1).PickProjectAsync(OwnerChatId, projectId, Arg.Any<CancellationToken>());
        await _commands.DidNotReceive().PickProjectAsync(Arg.Any<long>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        AnswerText(_api.Request(0)).ShouldBe(TelegramWaveSnapshotService.SnapshotAnswer);
    }

    [Fact]
    public async Task Handle_SnapshotCancelPress_AnswersWithoutTouchingEitherFlow()
    {
        await ConfigureTransportAsync();

        await CreateHandler().HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-s2", 5, "@owner", "picker",
                TelegramWaveCallbackData.SnapshotCancelData),
            CancellationToken.None);

        AnswerText(_api.Request(0)).ShouldBe(ITelegramWaveSnapshotService.DismissAnswer);
        _commands.DidNotReceiveWithAnyArgs().Cancel(default);
        await _snapshot.DidNotReceiveWithAnyArgs().PickProjectAsync(default, default, default);
    }

    [Theory]
    [InlineData("wave:sproj")]
    [InlineData("wave:sproj:123")]
    [InlineData("wave:sproj:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:extra")]
    [InlineData("wave:SPROJ:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111")]
    [InlineData("wave:scancel:extra")]
    public async Task Handle_GarbageSnapshotData_AnswersInvalidButton(string data)
    {
        await ConfigureTransportAsync();

        await CreateHandler().HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-s3", 5, "@owner", "picker", data), CancellationToken.None);

        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.InvalidButtonAnswer);
        await _snapshot.DidNotReceiveWithAnyArgs().PickProjectAsync(default, default, default);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("proj")]
    [InlineData("room")]
    [InlineData("sproj")]
    [InlineData("scancel")]
    [InlineData("garbage")]
    [InlineData("oversize")]
    public async Task EveryWaveBranch_AnswersItsCallbackQueryExactlyOnceAndSendsNothingElse(
        string branch)
    {
        await ConfigureTransportAsync();
        var projectId = Guid.NewGuid();
        _commands.Cancel(OwnerChatId).Returns(TelegramWaveCommandService.CancelAnswer);
        _commands.PickProjectAsync(OwnerChatId, projectId, Arg.Any<CancellationToken>())
            .Returns(TelegramWaveCommandService.RoomPickAnswer);
        _commands.PickRoomAsync(OwnerChatId, "1f0aa0e0", 0, Arg.Any<CancellationToken>())
            .Returns(TelegramWaveCommandService.PromptAnswer);
        _snapshot.PickProjectAsync(OwnerChatId, projectId, Arg.Any<CancellationToken>())
            .Returns(TelegramWaveSnapshotService.SnapshotAnswer);

        await CreateHandler().HandleAsync(
            new TelegramCallback(OwnerChatId, $"cq-{branch}", 5, "@owner", "picker", DataFor(branch, projectId)),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        var answer = AnswerText(_api.Request(0));
        answer.ShouldBe(ExpectedAnswer(branch));
        answer.Length.ShouldNotBe(0);
        answer.Length.ShouldBeLessThanOrEqualTo(TelegramNotificationCallbackHandler.AnswerTextLimit);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("proj")]
    [InlineData("room")]
    [InlineData("sproj")]
    [InlineData("scancel")]
    [InlineData("garbage")]
    public async Task NonAllowlistedChat_IsDeniedOnEverySchemeWithoutTouchingAFlow(string branch)
    {
        await ConfigureTransportAsync();
        var projectId = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new TelegramCallback(999, $"cq-{branch}", 5, "@stranger", "picker", DataFor(branch, projectId)),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.NotAllowedAnswer);
        _commands.DidNotReceiveWithAnyArgs().Cancel(default);
        await _commands.DidNotReceiveWithAnyArgs().PickProjectAsync(default, default, default);
        await _commands.DidNotReceiveWithAnyArgs().PickRoomAsync(default, default, default, default);
        await _snapshot.DidNotReceiveWithAnyArgs().PickProjectAsync(default, default, default);
        await _snapshot.DidNotReceiveWithAnyArgs().HandleCommandAsync(default, default);
    }

    [Fact]
    public async Task FlowThatThrowsAfterPartialWork_StillAnswersTheFailureNoticeOnce()
    {
        await ConfigureTransportAsync();
        _commands.PickProjectAsync(OwnerChatId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("the picker broke"));

        await CreateHandler().HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-partial", 5, "@owner", "picker", "wave:proj:00000000-0000-0000-0000-000000000000"),
            CancellationToken.None);

        Answers().ShouldHaveSingleItem()
            .ShouldBe(TelegramNotificationCallbackHandler.FailureAnswer);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _temp.Dispose();
    }

    private static string DataFor(string branch, Guid projectId) => branch switch
    {
        "cancel" => TelegramWaveCallbackData.CancelData,
        "proj" => $"wave:proj:{projectId:D}",
        "room" => TelegramWaveCallbackData.RoomData("1f0aa0e0", 0),
        "sproj" => $"wave:sproj:{projectId:D}",
        "scancel" => TelegramWaveCallbackData.SnapshotCancelData,
        "garbage" => "wave:room:ZZZZZZZZ:1",
        _ => $"wave:cancel:{new string('x', 70)}",
    };

    private static string ExpectedAnswer(string branch) => branch switch
    {
        "cancel" => TelegramWaveCommandService.CancelAnswer,
        "proj" => TelegramWaveCommandService.RoomPickAnswer,
        "room" => TelegramWaveCommandService.PromptAnswer,
        "sproj" => TelegramWaveSnapshotService.SnapshotAnswer,
        "scancel" => ITelegramWaveSnapshotService.DismissAnswer,
        _ => TelegramNotificationCallbackHandler.InvalidButtonAnswer,
    };

    private List<string> Answers() =>
        [.. _api.Requests
            .Where(request => request.Path.Contains("answerCallbackQuery", StringComparison.Ordinal))
            .Select(AnswerText)];

    private TelegramWaveCallbackHandler CreateHandler()
    {
        _logs = [];
        return new TelegramWaveCallbackHandler(
            new TelegramTransportOptionsProvider(new AppSettingsService(_temp.CreateContext())),
            _cache,
            _commands,
            _snapshot,
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
}
