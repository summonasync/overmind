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
    [MemberData(nameof(WaveBranchCases.UnparsableCallbackShapes), MemberType = typeof(WaveBranchCases))]
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
        _logs.ShouldContain(line =>
            line.StartsWith("Information:", StringComparison.Ordinal)
            && line.Contains("not allowlisted; denied", StringComparison.Ordinal));
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
    [MemberData(nameof(WaveBranchCases.WaveActions), MemberType = typeof(WaveBranchCases))]
    public async Task EveryWaveBranch_AnswersItsCallbackQueryExactlyOnceAndSendsNothingElse(
        WaveCallbackAction action)
    {
        await ConfigureTransportAsync();
        ArrangeFlowAnswer(action);

        await CreateHandler().HandleAsync(
            new TelegramCallback(OwnerChatId, $"cq-{action}", 5, "@owner", "picker",
                WaveBranchCases.CallbackData[action]),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        var answer = AnswerText(_api.Request(0));
        answer.ShouldBe(WaveBranchCases.FlowAnswer[action]);
        answer.Length.ShouldNotBe(0);
        answer.Length.ShouldBeLessThanOrEqualTo(TelegramNotificationCallbackHandler.AnswerTextLimit);
    }

    [Fact]
    public async Task FlowAnswerOverTheCallbackBudget_IsTruncatedOnTheWire()
    {
        await ConfigureTransportAsync();
        _commands.Cancel(OwnerChatId)
            .Returns(new string('x', TelegramNotificationCallbackHandler.AnswerTextLimit + 40));

        await CreateHandler().HandleAsync(
            new TelegramCallback(OwnerChatId, "cq-long", 5, "@owner", "picker",
                WaveBranchCases.CallbackData[WaveCallbackAction.Cancel]),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        var answer = AnswerText(_api.Request(0));
        answer.Length.ShouldBe(TelegramNotificationCallbackHandler.AnswerTextLimit);
        answer.ShouldEndWith("…");
    }

    [Fact]
    public void WaveBranchMaps_CoverEveryWaveCallbackAction()
    {
        var actions = Enum.GetValues<WaveCallbackAction>();

        WaveBranchCases.CallbackData.Count.ShouldBe(actions.Length);
        WaveBranchCases.FlowAnswer.Count.ShouldBe(actions.Length);
        foreach (var action in actions)
        {
            WaveBranchCases.CallbackData.ContainsKey(action)
                .ShouldBeTrue($"{action} has no callback data to drive the door");
            WaveBranchCases.FlowAnswer.ContainsKey(action)
                .ShouldBeTrue($"{action} has no expected answer to pin the door");
        }
    }

    [Theory]
    [MemberData(nameof(WaveBranchCases.EveryCallbackShape), MemberType = typeof(WaveBranchCases))]
    public async Task NonAllowlistedChat_IsDeniedOnEverySchemeWithoutTouchingAFlow(string data)
    {
        await ConfigureTransportAsync();

        await CreateHandler().HandleAsync(
            new TelegramCallback(999, $"cq-{data}", 5, "@stranger", "picker", data),
            CancellationToken.None);

        _api.RequestCount.ShouldBe(1);
        AnswerText(_api.Request(0)).ShouldBe(TelegramNotificationCallbackHandler.NotAllowedAnswer);
        _commands.DidNotReceiveWithAnyArgs().Cancel(default);
        await _commands.DidNotReceiveWithAnyArgs().PickProjectAsync(default, default, default);
        await _commands.DidNotReceiveWithAnyArgs().PickRoomAsync(
            default, Arg.Any<string>(), default, default);
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

    private void ArrangeFlowAnswer(WaveCallbackAction action)
    {
        var answer = WaveBranchCases.FlowAnswer[action];
        switch (action)
        {
            case WaveCallbackAction.Cancel:
                _commands.Cancel(OwnerChatId).Returns(answer);
                break;
            case WaveCallbackAction.Project:
                _commands.PickProjectAsync(OwnerChatId, WaveBranchCases.PickerProjectId, Arg.Any<CancellationToken>())
                    .Returns(answer);
                break;
            case WaveCallbackAction.Room:
                _commands.PickRoomAsync(
                        OwnerChatId, WaveBranchCases.PickerDraftToken, 0, Arg.Any<CancellationToken>())
                    .Returns(answer);
                break;
            case WaveCallbackAction.SnapshotProject:
                _snapshot.PickProjectAsync(
                        OwnerChatId, WaveBranchCases.PickerProjectId, Arg.Any<CancellationToken>())
                    .Returns(answer);
                break;
            case WaveCallbackAction.SnapshotCancel:
                break;
        }
    }

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
