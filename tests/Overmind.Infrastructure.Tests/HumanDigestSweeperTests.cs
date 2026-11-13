using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Overmind.Infrastructure.Wake;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class HumanDigestSweeperTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly HumanInboxService _inbox;
    private readonly NotificationsService _notifications;
    private readonly Guid _projectId;

    private static readonly Func<Guid, string, string> TestLink =
        (projectId, slug) => $"https://ui.test/dashboard?project={projectId}&feature={slug}&tab=result";

    public HumanDigestSweeperTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer
            .SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));

        var project = Project.Create($"digest-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;

        var claims = new FeatureClaimService(_context, TestPlanningDispatcher.Create(_context, _materializer));
        _inbox = new HumanInboxService(_context, claims);
        _notifications = CreateNotificationsService();
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Sweep_FlagOff_NeverPosts()
    {
        await SeedAwaitingHumanAsync("wait-a");
        var now = DateTime.UtcNow;

        var result = await CreateSweeper().SweepAsync(TestLink, true, now);

        result.ProjectsScanned.ShouldBe(0);
        result.DigestsPosted.ShouldBe(0);
        (await _context.AgentNotifications.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Sweep_FlagOnEmptyInbox_PostsNothing()
    {
        EnableDigest();
        var now = DateTime.UtcNow;

        var result = await CreateSweeper().SweepAsync(TestLink, true, now);

        result.DigestsPosted.ShouldBe(0);
        (await _context.AgentNotifications.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Sweep_FlagOnPendingTap_PostsOneInfoNotificationWithDeepLinks()
    {
        EnableDigest();
        await SeedAwaitingHumanAsync("wait-a");
        var now = DateTime.UtcNow;

        var result = await CreateSweeper().SweepAsync(TestLink, true, now);

        result.DigestsPosted.ShouldBe(1);
        var notification = await _context.AgentNotifications.SingleAsync();
        notification.Kind.ShouldBe(NotificationKind.Info);
        notification.Agent.ShouldBe(HumanDigestSweeper.Agent);
        notification.ProjectId.ShouldBe(_projectId);
        notification.FeatureSlug.ShouldBeNull();
        notification.Text.ShouldContain("wait-a");
        notification.Text.ShouldContain("pending since");
        notification.Text.ShouldContain($"https://ui.test/dashboard?project={_projectId}&feature=wait-a&tab=result");
    }

    [Fact]
    public async Task Sweep_RerunInsideTheInterval_IsSilent()
    {
        EnableDigest();
        await SeedAwaitingHumanAsync("wait-a");
        var sweeper = CreateSweeper();

        await sweeper.SweepAsync(TestLink, true, DateTime.UtcNow);
        var rerun = await sweeper.SweepAsync(TestLink, true, DateTime.UtcNow.AddHours(1));

        rerun.DigestsPosted.ShouldBe(0);
        (await _context.AgentNotifications.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Sweep_AfterIntervalElapsed_PostsAgain()
    {
        EnableDigest();
        await SeedAwaitingHumanAsync("wait-a");
        var sweeper = CreateSweeper();
        var now = DateTime.UtcNow;
        await sweeper.SweepAsync(TestLink, true, now);

        var rerun = await sweeper.SweepAsync(TestLink, true, now.AddHours(25));

        rerun.DigestsPosted.ShouldBe(1);
        (await _context.AgentNotifications.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Sweep_FeatureWithOpenTasks_IsNotCounted()
    {
        EnableDigest();
        var feature = await CreateFeatureAsync("still-working");
        feature.SetStatus(PlanningFeatureStatus.InProgress);
        _context.PlanningArtifacts.Add(PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Task, "open task"));
        var validated = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Result, "close-out");
        validated.Validate();
        _context.PlanningArtifacts.Add(validated);
        await _context.SaveChangesAsync();

        var result = await CreateSweeper().SweepAsync(TestLink, true, DateTime.UtcNow);

        result.DigestsPosted.ShouldBe(0);
    }

    [Fact]
    public async Task Sweep_TwoDigestEnabledProjects_PostsOneDigestPerProject()
    {
        var other = Project.Create($"digest-other-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(other);
        await _context.SaveChangesAsync();
        EnableDigest(_projectId);
        EnableDigest(other.Id);
        await SeedAwaitingHumanAsync("wait-a");
        await SeedAwaitingHumanAsync("wait-b", other.Id);

        var result = await CreateSweeper().SweepAsync(TestLink, true, DateTime.UtcNow);

        result.ProjectsScanned.ShouldBe(2);
        result.DigestsPosted.ShouldBe(2);
        var notifications = await _context.AgentNotifications.ToListAsync();
        notifications.Select(n => n.ProjectId).ShouldBe([_projectId, other.Id], ignoreOrder: true);
        notifications.Count(n => n.Text.Contains("wait-a")).ShouldBe(1);
        notifications.Count(n => n.Text.Contains("wait-b")).ShouldBe(1);
    }

    [Theory]
    [InlineData(false, true, true, 0)]
    [InlineData(true, false, true, 0)]
    [InlineData(true, true, true, 1)]
    [InlineData(true, true, false, 0)]
    public async Task Sweep_GlobalAndProjectFlags_PostsOnlyWhenBothEnabled(
        bool globalEnabled, bool projectEnabled, bool seedInbox, int expectedPosts)
    {
        if (projectEnabled)
        {
            EnableDigest();
        }

        if (seedInbox)
        {
            await SeedAwaitingHumanAsync("wait-a");
        }

        var result = await CreateSweeper().SweepAsync(TestLink, globalEnabled, DateTime.UtcNow);

        result.ProjectsScanned.ShouldBe(globalEnabled && projectEnabled ? 1 : 0);
        result.DigestsPosted.ShouldBe(expectedPosts);
        (await _context.AgentNotifications.CountAsync()).ShouldBe(expectedPosts);
    }

    [Fact]
    public void IsDue_NoDigestYet_IsDue()
    {
        HumanDigestSweeper.IsDue(null, DateTime.UtcNow).ShouldBeTrue();
    }

    [Fact]
    public void IsDue_InsideTheInterval_Waits()
    {
        var now = DateTime.UtcNow;
        HumanDigestSweeper.IsDue(now.AddHours(-23), now).ShouldBeFalse();
    }

    [Fact]
    public void IsDue_IntervalReached_IsDue()
    {
        var now = DateTime.UtcNow;
        HumanDigestSweeper.IsDue(now.AddHours(-24), now).ShouldBeTrue();
        HumanDigestSweeper.IsDue(now.AddDays(-30), now).ShouldBeTrue();
    }

    private HumanDigestSweeper CreateSweeper() => new(_context, _inbox, _notifications);

    private NotificationsService CreateNotificationsService()
    {
        var scopeFactory = new ServiceCollection().BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        return new NotificationsService(
            _context,
            new ReplyAutoActFanout(_context, new ReplyAutoActQueue(), NullLogger<ReplyAutoActFanout>.Instance),
            new WakeDropFanout(scopeFactory, NullLogger<WakeDropFanout>.Instance),
            []);
    }

    private void EnableDigest(Guid? projectId = null)
    {
        var project = _context.Projects.Single(p => p.Id == (projectId ?? _projectId));
        project.SetHumanDigest(true);
        _context.SaveChanges();
    }

    private async Task<PlanningFeature> CreateFeatureAsync(string slug, Guid? projectId = null)
    {
        var dispatcher = TestPlanningDispatcher.Create(_context, _materializer);
        var service = new PlanningFeatureService(
            _context,
            _materializer,
            new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
            new TaskDependencyService(_context, dispatcher),
            dispatcher,
            NullLogger<PlanningFeatureService>.Instance);

        return await service.CreateAsync(projectId ?? _projectId, slug, "Waiting", "body");
    }

    private async Task SeedAwaitingHumanAsync(string slug, Guid? projectId = null)
    {
        var feature = await CreateFeatureAsync(slug, projectId);
        feature.SetStatus(PlanningFeatureStatus.Review);
        var validated = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Result, "close-out");
        validated.Validate();
        _context.PlanningArtifacts.Add(validated);
        await _context.SaveChangesAsync();
    }
}
