using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ReplyAutoActProcessorTests : IDisposable
{
    private const string AgentAlias = "van-darkholme";

    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;

    public ReplyAutoActProcessorTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer
            .SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], [], []));

        var project = Project.Create($"auto-act-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Process_FreshReplyOnDraftSpec_ValidatesArtifactAuditsAndNotifies()
    {
        await EnableAutoActAsync();
        var feature = await CreateFeatureAsync("specs");
        var artifact = await CreateArtifactAsync(feature.Id, PlanningArtifactKind.Spec);
        var notification = await CreateApprovalAsync("specs", "spec", DateTime.UtcNow);

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeTrue();
        var validated = await _context.PlanningArtifacts.SingleAsync(a => a.Id == artifact.Id);
        validated.Status.ShouldBe(PlanningArtifactStatus.Validated);
        validated.Body.ShouldContain($"[auto-act] by human reply {notification.Id}");
        validated.Body.ShouldContain($"notification {notification.Id}");
        var info = await _context.AgentNotifications.SingleAsync(n => n.Kind == NotificationKind.Info);
        info.Agent.ShouldBe(AgentAlias);
        info.ProjectId.ShouldBe(_projectId);
        info.FeatureSlug.ShouldBe("specs");
    }

    [Fact]
    public async Task Process_StaleReply_SkipsValidation()
    {
        await EnableAutoActAsync();
        var feature = await CreateFeatureAsync("late-spec");
        var notification = await CreateApprovalAsync("late-spec", "spec", DateTime.UtcNow.AddMinutes(-5));
        await CreateArtifactAsync(feature.Id, PlanningArtifactKind.Spec);

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeFalse();
        var draft = await _context.PlanningArtifacts.SingleAsync(a => a.FeatureId == feature.Id);
        draft.Status.ShouldBe(PlanningArtifactStatus.Draft);
        (await _context.AgentNotifications.AnyAsync(n => n.Kind == NotificationKind.Info)).ShouldBeFalse();
    }

    [Fact]
    public async Task Process_DraftResultWithResultTab_ValidatesResult()
    {
        await EnableAutoActAsync();
        var feature = await CreateFeatureAsync("close-out");
        await CreateArtifactAsync(feature.Id, PlanningArtifactKind.Result);
        var notification = await CreateApprovalAsync("close-out", "result", DateTime.UtcNow);

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeTrue();
        (await _context.PlanningArtifacts.SingleAsync(a => a.FeatureId == feature.Id))
            .Status.ShouldBe(PlanningArtifactStatus.Validated);
    }

    [Fact]
    public async Task Process_UnknownSlug_SkipsWithLog()
    {
        await EnableAutoActAsync();
        var notification = await CreateApprovalAsync("ghost-feature", "spec", DateTime.UtcNow);

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeFalse();
        result.Reason.ShouldContain("ghost-feature");
    }

    [Fact]
    public async Task Process_AutoActDisabled_Skips()
    {
        var feature = await CreateFeatureAsync("muted");
        await CreateArtifactAsync(feature.Id, PlanningArtifactKind.Spec);
        var notification = await CreateApprovalAsync("muted", "spec", DateTime.UtcNow);

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeFalse();
        result.Reason.ShouldContain("disabled");
        (await _context.PlanningArtifacts.SingleAsync(a => a.FeatureId == feature.Id))
            .Status.ShouldBe(PlanningArtifactStatus.Draft);
    }

    [Fact]
    public async Task Process_QuestionReply_Skips()
    {
        await EnableAutoActAsync();
        var feature = await CreateFeatureAsync("asked");
        await CreateArtifactAsync(feature.Id, PlanningArtifactKind.Spec);
        var notification = AgentNotification.Restore(
            Guid.NewGuid(), _projectId, NotificationKind.Question, "asked", "spec",
            "which shape?", AgentAlias, DateTime.UtcNow.AddMinutes(-1),
            replyText: "the flat one", repliedAt: DateTime.UtcNow);
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeFalse();
        (await _context.PlanningArtifacts.SingleAsync(a => a.FeatureId == feature.Id))
            .Status.ShouldBe(PlanningArtifactStatus.Draft);
    }

    [Fact]
    public async Task Process_DoneEligibleFeature_FlipsDoneWithProof()
    {
        await EnableAutoActAsync();
        var feature = await CreateFeatureAsync("closing");
        await CreateValidatedResultAsync(feature.Id);
        var notification = await CreateApprovalAsync("closing", tab: null, DateTime.UtcNow);

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeTrue();
        var closed = await _context.PlanningFeatures.SingleAsync(f => f.Id == feature.Id);
        closed.Status.ShouldBe(PlanningFeatureStatus.Done);
        closed.Body.ShouldContain($"[auto-act] by human reply {notification.Id}");
        closed.Body.ShouldContain("approved by human tap");
        (await _context.AgentNotifications.AnyAsync(n => n.Kind == NotificationKind.Info)).ShouldBeTrue();
    }

    [Fact]
    public async Task Process_DoneFlipWithOpenBlocker_Skips()
    {
        await EnableAutoActAsync();
        var blocker = await CreateFeatureAsync("blocker");
        var feature = await CreateFeatureAsync("chained");
        await CreateValidatedResultAsync(feature.Id);
        feature.AddBlockedBy(blocker);
        await _context.SaveChangesAsync();
        var notification = await CreateApprovalAsync("chained", tab: null, DateTime.UtcNow);

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeFalse();
        result.Reason.ShouldContain("blocker");
        (await _context.PlanningFeatures.SingleAsync(f => f.Id == feature.Id))
            .Status.ShouldBe(PlanningFeatureStatus.Draft);
    }

    [Fact]
    public async Task Process_DoneFlipWithoutValidatedResult_Skips()
    {
        await EnableAutoActAsync();
        await CreateFeatureAsync("no-result");
        var notification = await CreateApprovalAsync("no-result", tab: null, DateTime.UtcNow);

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeFalse();
        result.Reason.ShouldContain("validated result");
    }

    [Fact]
    public async Task Process_DoneFlipStaleAgainstResult_Skips()
    {
        await EnableAutoActAsync();
        var feature = await CreateFeatureAsync("stale-close");
        var notification = await CreateApprovalAsync("stale-close", tab: null, DateTime.UtcNow.AddMinutes(-5));
        await CreateValidatedResultAsync(feature.Id);

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeFalse();
        (await _context.PlanningFeatures.SingleAsync(f => f.Id == feature.Id))
            .Status.ShouldBe(PlanningFeatureStatus.Draft);
    }

    [Fact]
    public async Task Process_AlreadyDoneFeature_Skips()
    {
        await EnableAutoActAsync();
        var feature = await CreateFeatureAsync("closed");
        await CreateValidatedResultAsync(feature.Id);
        feature.SetStatus(PlanningFeatureStatus.Done, "manual close", PlanningLogSource.Ui);
        await _context.SaveChangesAsync();
        var notification = await CreateApprovalAsync("closed", tab: null, DateTime.UtcNow);

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeFalse();
        result.Reason.ShouldContain("already done");
    }

    [Fact]
    public async Task Process_SpecReplyOnDoneEligibleFeature_ValidatesSpecWithoutClose()
    {
        await EnableAutoActAsync();
        var feature = await CreateFeatureAsync("two-doors");
        await CreateArtifactAsync(feature.Id, PlanningArtifactKind.Spec);
        await CreateValidatedResultAsync(feature.Id);
        var notification = await CreateApprovalAsync("two-doors", "spec", DateTime.UtcNow);

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeTrue();
        (await _context.PlanningArtifacts
                .SingleAsync(a => a.FeatureId == feature.Id && a.Kind == PlanningArtifactKind.Spec))
            .Status.ShouldBe(PlanningArtifactStatus.Validated);
        (await _context.PlanningFeatures.SingleAsync(f => f.Id == feature.Id))
            .Status.ShouldNotBe(PlanningFeatureStatus.Done);
    }

    [Fact]
    public async Task Process_ValidatedSpecReplyOnDoneEligibleFeature_SkipsWithoutClose()
    {
        await EnableAutoActAsync();
        var feature = await CreateFeatureAsync("second-tap");
        var spec = await CreateArtifactAsync(feature.Id, PlanningArtifactKind.Spec);
        spec.Validate();
        await _context.SaveChangesAsync();
        await CreateValidatedResultAsync(feature.Id);
        var notification = await CreateApprovalAsync("second-tap", "spec", DateTime.UtcNow);

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeFalse();
        result.Reason.ShouldContain("no draft");
        (await _context.PlanningFeatures.SingleAsync(f => f.Id == feature.Id))
            .Status.ShouldNotBe(PlanningFeatureStatus.Done);
    }

    [Fact]
    public async Task Process_TasksTabReplyOnDoneEligibleFeature_SkipsWithoutValidationOrClose()
    {
        await EnableAutoActAsync();
        var feature = await CreateFeatureAsync("task-page");
        await CreateArtifactAsync(feature.Id, PlanningArtifactKind.Spec);
        await CreateValidatedResultAsync(feature.Id);
        var notification = await CreateApprovalAsync("task-page", "tasks", DateTime.UtcNow);

        var result = await CreateProcessor().ProcessAsync(notification.Id, CancellationToken.None);

        result.Acted.ShouldBeFalse();
        result.Reason.ShouldContain("no draft");
        (await _context.PlanningArtifacts
                .SingleAsync(a => a.FeatureId == feature.Id && a.Kind == PlanningArtifactKind.Spec))
            .Status.ShouldBe(PlanningArtifactStatus.Draft);
        (await _context.PlanningFeatures.SingleAsync(f => f.Id == feature.Id))
            .Status.ShouldNotBe(PlanningFeatureStatus.Done);
    }

    [Fact]
    public async Task Process_MissingNotification_Skips()
    {
        var result = await CreateProcessor()
            .ProcessAsync(Guid.NewGuid(), CancellationToken.None);

        result.Acted.ShouldBeFalse();
        result.Reason.ShouldContain("no longer exists");
    }

    private async Task EnableAutoActAsync()
    {
        var project = await _context.Projects.SingleAsync(p => p.Id == _projectId);
        project.SetAutoActOnReplies(true);
        await _context.SaveChangesAsync();
    }

    private async Task<PlanningFeature> CreateFeatureAsync(string slug)
    {
        var feature = PlanningFeature.Create(_projectId, slug, slug, string.Empty);
        _context.PlanningFeatures.Add(feature);
        await _context.SaveChangesAsync();
        return feature;
    }

    private async Task<PlanningArtifact> CreateArtifactAsync(Guid featureId, PlanningArtifactKind kind)
    {
        var artifact = PlanningArtifact.Create(featureId, kind, $"{kind} body for {featureId}");
        _context.PlanningArtifacts.Add(artifact);
        await _context.SaveChangesAsync();
        return artifact;
    }

    private async Task<PlanningArtifact> CreateValidatedResultAsync(Guid featureId)
    {
        var result = PlanningArtifact.Create(featureId, PlanningArtifactKind.Result, "close-out body");
        result.Validate();
        _context.PlanningArtifacts.Add(result);
        await _context.SaveChangesAsync();
        return result;
    }

    private async Task<AgentNotification> CreateApprovalAsync(string slug, string? tab, DateTime repliedAt)
    {
        var notification = AgentNotification.Restore(
            Guid.NewGuid(), _projectId, NotificationKind.Approval, slug, tab,
            $"approve {slug}", AgentAlias, repliedAt.AddMinutes(-10),
            replyText: "approved", repliedAt: repliedAt);
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    private ReplyAutoActProcessor CreateProcessor()
    {
        var dispatcher = TestPlanningDispatcher.Create(_context, _materializer);
        return new ReplyAutoActProcessor(
            _context,
            new PlanningArtifactService(
                _context, dispatcher, new TaskDependencyService(_context, dispatcher)),
            new PlanningFeatureService(
                _context,
                _materializer,
                new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
                new TaskDependencyService(_context, dispatcher),
                dispatcher,
                NullLogger<PlanningFeatureService>.Instance),
            new FeatureDependencyService(_context, dispatcher),
            NullLogger<ReplyAutoActProcessor>.Instance);
    }
}
