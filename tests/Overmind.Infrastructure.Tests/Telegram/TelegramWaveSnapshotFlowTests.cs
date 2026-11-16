using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Planning;
using Overmind.Infrastructure.Telegram;
using Overmind.Infrastructure.Wake;
using Overmind.Infrastructure.Wave;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramWaveSnapshotFlowTests : IAsyncLifetime
{
    private const long OwnerChatId = 42;
    private const string TestToken = "123456:TESTSECRETTOKEN";
    private const string CancelLabel = "✖️ Cancel";

    private readonly TempDatabase _temp = new();
    private readonly FakeTelegramApi _api = new();
    private readonly TelegramBotClientCache _cache;
    private readonly ServiceProvider _wakeServices;
    private readonly WakeDropFanout _wakeFanout;
    private readonly StubWaveChatBroadcaster _broadcaster = new();
    private readonly List<string> _logs = [];
    private readonly TelegramWaveDraftStore _drafts = new(TimeProvider.System);
    private readonly WaveChatService _chat;
    private readonly TelegramWaveSnapshotService _snapshots;
    private readonly TelegramWaveCommandService _commands;
    private readonly TelegramWaveCallbackHandler _callbacks;
    private Guid _projectId = Guid.Empty;

    public TelegramWaveSnapshotFlowTests()
    {
        _cache = new TelegramBotClientCache(new StubTelegramBotClientFactory(_api));
        (_wakeFanout, _wakeServices) = TestWakeFanout.Create(_temp);
        var sender = new TelegramReplySender(_cache, CreateOptionsProvider());
        _chat = new WaveChatService(_temp.CreateContext(), _wakeFanout, _broadcaster);
        _snapshots = new TelegramWaveSnapshotService(
            sender,
            new WaveSnapshotService(
                _temp.CreateContext(),
                new FeatureClaimService(_temp.CreateContext(), Dispatcher()),
                new DirectiveService(_temp.CreateContext())),
            _chat,
            _temp.CreateContext(),
            new CollectingLogger<TelegramWaveSnapshotService>(_logs));
        _commands = new TelegramWaveCommandService(
            sender, _drafts, new TelegramWavePromptTombstone(TimeProvider.System), _chat,
            _temp.CreateContext(), _snapshots,
            new CollectingLogger<TelegramWaveCommandService>(_logs));
        _callbacks = new TelegramWaveCallbackHandler(
            CreateOptionsProvider(), _cache, _commands, _snapshots,
            new CollectingLogger<TelegramWaveCallbackHandler>(_logs));
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
    public async Task WaveSnapshot_SingleProject_SendsTheDigestWithoutAPicker()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);

        await RunSnapshotAsync();

        var digest = SentRequests().ShouldHaveSingleItem();
        digest.Body.ShouldNotContain("reply_markup");
        SentText(digest).ShouldBe($"Wave · Overmind\n{TelegramWaveSnapshotFormatter.IdleLine}");
    }

    [Fact]
    public async Task WaveSnapshot_MultipleProjects_OfferTheStatelessPickerThenTheDigestOnPress()
    {
        await SeedProjectAsync("Alpha", []);
        var beta = await SeedProjectAsync("Beta", []);

        await RunSnapshotAsync();

        var picker = SentRequests().ShouldHaveSingleItem();
        SentText(picker).ShouldBe(TelegramWaveSnapshotService.PickerText);
        Labels(picker).ShouldBe(["Alpha", "Beta", CancelLabel]);
        Data(picker).ShouldBe([
            $"wave:sproj:{_firstProjectId:D}",
            $"wave:sproj:{beta:D}",
            TelegramWaveCallbackData.SnapshotCancelData,
        ]);

        await PressAsync(11, $"wave:sproj:{_projectId:D}");

        Answers().ShouldContain(TelegramWaveSnapshotService.SnapshotAnswer);
        SentTexts().ShouldContain(text => text.StartsWith("Wave · Beta", StringComparison.Ordinal));
        _drafts.Current(OwnerChatId).ShouldBeNull();
    }

    [Fact]
    public async Task WaveSnapshot_NoProjects_SendsTheNoticeInsteadOfAnEmptyPicker()
    {
        await RunSnapshotAsync();

        SentTexts().ShouldBe([TelegramWaveSnapshotService.NoProjectsReply]);
        SentRequests().ShouldHaveSingleItem().Body.ShouldNotContain("reply_markup");
    }

    [Fact]
    public async Task SchemeSeparation_SnapshotPressOpensNoDraftAndSendPressRendersNoDigest()
    {
        await SeedProjectAsync("Alpha", []);
        var beta = await SeedProjectAsync("Beta", []);

        await PressAsync(11, $"wave:sproj:{beta:D}");

        Answers().ShouldContain(TelegramWaveSnapshotService.SnapshotAnswer);
        SentTexts().ShouldContain(text => text.StartsWith("Wave · Beta", StringComparison.Ordinal));
        _drafts.Current(OwnerChatId).ShouldBeNull();

        await _commands.HandleAsync(
            TelegramWaveCommand.SendToWave,
            new TelegramIncomingMessage(OwnerChatId, "/send_to_wave", From: "Owner"),
            CancellationToken.None);
        var capturedToken = _drafts.Current(OwnerChatId)!.Token;

        await PressAsync(12, $"wave:proj:{beta:D}");

        Answers().ShouldContain(TelegramWaveCommandService.RoomPickAnswer);
        _drafts.Current(OwnerChatId)!.Token.ShouldBe(capturedToken);
        _drafts.Current(OwnerChatId)!.ProjectId.ShouldBe(beta);
        SentTexts().Count(text => text.StartsWith("Wave · ", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task Digest_CarriesAllFourSections_AgainstSeededWaveState()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge", "frontend"]);
        await BindLaneAsync("telegram-bridge", "milk-toast");
        var alive = await SeedSessionAsync("tucker", "qwen");
        var stale = await SeedSessionAsync("oldboy", "claude", lastSeenAt: DateTime.UtcNow - TimeSpan.FromMinutes(30));
        await SeedFeatureAsync("215-wave", FeaturePriority.P1, PlanningFeatureStatus.InProgress, 1, alive, "telegram-bridge");
        var gate = await SeedFeatureAsync("216-gate", FeaturePriority.P3, PlanningFeatureStatus.Draft, 3);
        await ClaimFeatureAsync("216-gate", stale);
        await SeedFeatureAsync("217-next", FeaturePriority.P2, PlanningFeatureStatus.Ready, 2, blockedBy: ["216-gate"]);
        await PostSignalAsync("all", "standup note, @tucker read it");
        await PostSignalAsync("telegram-bridge", "@milk-toast the lane is contested");

        await RunSnapshotAsync();

        var digest = SentText(SentRequests().ShouldHaveSingleItem());
        digest.ShouldContain("all · 1 mentions");
        digest.ShouldContain("telegram-bridge · 1 mentions");
        digest.ShouldContain("215-wave · tucker/qwen [alive] lane telegram-bridge (bound milk-toast)");
        digest.ShouldContain("216-gate · oldboy/claude [stale]");
        digest.ShouldContain("1. p1 215-wave [in progress]");
        digest.ShouldContain("2. p2 217-next [ready]");
        digest.ShouldContain("3. p3 216-gate [draft]");
        digest.ShouldContain("217-next waits on 216-gate [draft]");
        digest.ShouldNotContain("  none");
    }

    [Fact]
    public async Task Digest_CountsOnlyMentionsInsideTheWindow()
    {
        await SeedProjectAsync("Overmind", []);
        await PostSignalAsync("all", "@tucker inside the window");
        await PostSignalAsync("all", "@tucker outside the window");
        await PostSignalAsync("all", "nobody was mentioned here");
        await AgeMessageAsync("@tucker outside the window", TimeSpan.FromHours(26));

        await RunSnapshotAsync();

        var digest = SentText(SentRequests().ShouldHaveSingleItem());
        digest.ShouldContain("all · 1 mentions");
    }

    [Fact]
    public async Task Digest_ReadsNeverAdvanceWatermarksOrOpenConversationState()
    {
        await SeedProjectAsync("Overmind", ["telegram-bridge"]);
        await PostSignalAsync("telegram-bridge", "@tucker ping");
        var watermarkAt = DateTime.UtcNow - TimeSpan.FromHours(1);
        await using (var context = _temp.CreateContext())
        {
            context.WaveWatermarks.Add(WaveWatermark.Create(_projectId, "tucker", watermarkAt));
            await context.SaveChangesAsync();
        }

        await RunSnapshotAsync();
        await PressAsync(11, TelegramWaveCallbackData.SnapshotCancelData);

        await using var proof = _temp.CreateContext();
        var stored = await proof.WaveWatermarks.SingleAsync();
        stored.LastReadAt.ShouldBe(watermarkAt);
        proof.Conversations.Any().ShouldBeFalse();
        proof.ConversationMessages.Any().ShouldBeFalse();
        proof.TelegramChatMappings.Any().ShouldBeFalse();
        (await proof.WaveMessages.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task DeletedProjectBetweenPickerAndPress_AnswersFailureAndSendsNoDigest()
    {
        var alpha = await SeedProjectAsync("Alpha", []);
        await SeedProjectAsync("Beta", []);

        await using (var context = _temp.CreateContext())
        {
            context.Projects.Remove(await context.Projects.SingleAsync(p => p.Id == alpha));
            await context.SaveChangesAsync();
        }

        await PressAsync(11, $"wave:sproj:{alpha:D}");

        Answers().ShouldContain(TelegramNotificationCallbackHandler.FailureAnswer);
        SentRequests().ShouldBeEmpty();
    }

    [Fact]
    public async Task SnapshotPicker_CountsTheProjectsItLeftOut()
    {
        for (var index = 0; index < TelegramWaveCommandService.MaxProjectButtons + 5; index++)
        {
            await SeedProjectAsync($"p{index:D2}", []);
        }

        await RunSnapshotAsync();

        SentTexts()[0].ShouldBe(string.Format(
            TelegramWaveSnapshotService.PickerTruncationFormat,
            TelegramWaveCommandService.MaxProjectButtons,
            5));
        SentRequests().ShouldHaveSingleItem().Body.ShouldNotContain("p44");
    }

    [Fact]
    public async Task SnapshotCancelPress_AnswersAndKeepsThePendingSendDraftIntact()
    {
        await SeedProjectAsync("Alpha", []);
        await SeedProjectAsync("Beta", []);
        await _commands.HandleAsync(
            TelegramWaveCommand.SendToWave,
            new TelegramIncomingMessage(OwnerChatId, "/send_to_wave", From: "Owner"),
            CancellationToken.None);
        var capturedToken = _drafts.Current(OwnerChatId)!.Token;

        await PressAsync(11, TelegramWaveCallbackData.SnapshotCancelData);

        Answers().ShouldContain(ITelegramWaveSnapshotService.DismissAnswer);
        _drafts.Current(OwnerChatId)!.Token.ShouldBe(capturedToken);
        SentTexts().Count(text => text.StartsWith("Wave · ", StringComparison.Ordinal)).ShouldBe(0);
    }

    [Fact]
    public async Task OrdinaryTextReplyToTheSnapshotDigest_IsLeftToTheConversationPath()
    {
        await SeedProjectAsync("Overmind", []);

        await RunSnapshotAsync();
        var consumed = await _commands.TryHandleDraftReplyAsync(
            new TelegramIncomingMessage(OwnerChatId, "thanks", 11, "Owner"),
            CancellationToken.None);

        consumed.ShouldBeFalse();
    }

    public Task DisposeAsync()
    {
        _cache.Dispose();
        _wakeServices.Dispose();
        _temp.Dispose();
        return Task.CompletedTask;
    }

    private Guid _firstProjectId = Guid.Empty;

    private TelegramTransportOptionsProvider CreateOptionsProvider() =>
        new(new AppSettingsService(_temp.CreateContext()));

    private PlanningMaterializationDispatcher Dispatcher() =>
        TestPlanningDispatcher.Create(_temp.CreateContext(), Substitute.For<IPlanningMaterializer>());

    private Task RunSnapshotAsync() =>
        _commands.HandleAsync(
            TelegramWaveCommand.WaveSnapshot,
            new TelegramIncomingMessage(OwnerChatId, "/wave_snapshot", From: "Owner"),
            CancellationToken.None);

    private Task PressAsync(int messageId, string data) =>
        _callbacks.HandleAsync(
            new TelegramCallback(OwnerChatId, $"cq-{messageId}-{data}", messageId, "@owner", "picker", data),
            CancellationToken.None);

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
        if (_firstProjectId == Guid.Empty)
        {
            _firstProjectId = project.Id;
        }

        _projectId = project.Id;
        return project.Id;
    }

    private async Task BindLaneAsync(string lane, string boundAlias)
    {
        await using var context = _temp.CreateContext();
        var row = await context.PlanningLanes.SingleAsync(l => l.ProjectId == _projectId && l.Name == lane);
        row.Update(row.Name, row.Contour, row.SerialOrder, row.Priority, row.LiveSurfaces, boundAlias);
        await context.SaveChangesAsync();
    }

    private async Task<AgentSession> SeedSessionAsync(
        string alias, string vendor, DateTime? lastSeenAt = null)
    {
        await using var context = _temp.CreateContext();
        var session = lastSeenAt is null
            ? AgentSession.Create(alias, vendor)
            : AgentSession.Restore(Guid.NewGuid(), alias, vendor, lastSeenAt.Value, lastSeenAt.Value);
        context.AgentSessions.Add(session);
        await context.SaveChangesAsync();
        return session;
    }

    private async Task<PlanningFeature> SeedFeatureAsync(
        string slug,
        FeaturePriority priority,
        PlanningFeatureStatus status,
        int queuePosition,
        AgentSession? claimedBy = null,
        string? lane = null,
        IReadOnlyList<string>? blockedBy = null)
    {
        await using var context = _temp.CreateContext();
        var feature = PlanningFeature.Create(_projectId, slug, slug, string.Empty, priority);
        feature.SetStatus(status);
        feature.SetQueuePosition(queuePosition);
        if (claimedBy is not null)
        {
            feature.Claim(claimedBy.Alias, claimedBy.Vendor, claimedBy.Id, DateTime.UtcNow);
        }

        if (lane is not null)
        {
            feature.SetLane(lane);
        }

        if (blockedBy is not null)
        {
            foreach (var blockerSlug in blockedBy)
            {
                var blocker = await context.PlanningFeatures.SingleAsync(
                    f => f.ProjectId == _projectId && f.Slug == blockerSlug);
                feature.AddBlockedBy(blocker);
            }
        }

        context.PlanningFeatures.Add(feature);
        await context.SaveChangesAsync();
        return feature;
    }

    private async Task ClaimFeatureAsync(string slug, AgentSession session)
    {
        await using var context = _temp.CreateContext();
        var feature = await context.PlanningFeatures.SingleAsync(
            f => f.ProjectId == _projectId && f.Slug == slug);
        feature.Claim(session.Alias, session.Vendor, session.Id, DateTime.UtcNow);
        await context.SaveChangesAsync();
    }

    private async Task PostSignalAsync(string room, string text)
    {
        var posted = await _chat.PostAsync(
            _projectId, room, "boss", WaveMessage.OwnerRole, text, CancellationToken.None);
        posted.ProjectId.ShouldBe(_projectId);
    }

    private async Task AgeMessageAsync(string text, TimeSpan age)
    {
        await using var context = _temp.CreateContext();
        await context.WaveMessages
            .Where(m => m.Text == text)
            .ExecuteUpdateAsync(setter => setter.SetProperty(m => m.CreatedAt, DateTime.UtcNow - age));
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
}
