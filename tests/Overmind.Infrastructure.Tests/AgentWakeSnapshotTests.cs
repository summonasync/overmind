using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wake;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

[Collection(nameof(ProcessEnvironment))]
public sealed class AgentWakeSnapshotTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly AgentWakeService _wake;
    private readonly Guid _projectId;

    public AgentWakeSnapshotTests()
    {
        _context = _db.CreateContext();
        _wake = new AgentWakeService(_context);
        var project = Project.Create($"wake-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(WakeReplyLookback.EnvironmentVariable, null);
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Snapshot_ReturnsThePendingUnionTheApiServes()
    {
        var alias = "billy";
        var lastSeen = DateTime.UtcNow.AddHours(-2);
        _context.AgentSessions.Add(AgentSession.Restore(
            Guid.NewGuid(), alias, "claude", lastSeen.AddHours(-1), lastSeen));
        _context.AgentNotifications.Add(AgentNotification.Restore(
            Guid.NewGuid(), _projectId, NotificationKind.Question, null, null,
            "gate needs you", alias, DateTime.UtcNow.AddMinutes(-30),
            replyText: "approved", repliedAt: DateTime.UtcNow.AddMinutes(-20)));
        _context.AgentNotifications.Add(AgentNotification.Restore(
            Guid.NewGuid(), _projectId, NotificationKind.Approval, null, null,
            "other gate", "dan", DateTime.UtcNow.AddMinutes(-30),
            replyText: "declined", repliedAt: DateTime.UtcNow.AddMinutes(-15)));
        _context.WaveMessages.Add(WaveMessage.Create(
            _projectId, "all", "dan", "worker", "ping @billy"));
        await _context.SaveChangesAsync();

        var snapshot = await _wake.SnapshotAsync(alias, CancellationToken.None);
        var api = await _wake.PendingAsync(alias, lastSeen, null, null, null, CancellationToken.None);

        snapshot.ShouldNotBeNull();
        snapshot.ShouldBeEquivalentTo(api);
        snapshot.Signals.Select(s => s.Kind).ShouldBe([
            AgentWakeService.ReplyKind,
            AgentWakeService.MentionKind
        ]);
    }

    [Fact]
    public async Task DefaultWindow_ExcludesConsumedReplies_AndSinceReplayReturnsThem()
    {
        var alias = "billy";
        var lastSeen = DateTime.UtcNow.AddHours(-2);
        _context.AgentSessions.Add(AgentSession.Restore(
            Guid.NewGuid(), alias, "claude", lastSeen.AddHours(-1), lastSeen));
        _context.AgentNotifications.Add(AgentNotification.Restore(
            Guid.NewGuid(), _projectId, NotificationKind.Approval, null, null,
            "pending gate", alias, DateTime.UtcNow.AddMinutes(-30),
            replyText: "approved", repliedAt: DateTime.UtcNow.AddMinutes(-20)));
        _context.AgentNotifications.Add(AgentNotification.Restore(
            Guid.NewGuid(), _projectId, NotificationKind.Approval, null, null,
            "consumed gate", alias, DateTime.UtcNow.AddMinutes(-30),
            readAt: DateTime.UtcNow.AddMinutes(-10),
            replyText: "approved earlier", repliedAt: DateTime.UtcNow.AddMinutes(-25)));
        await _context.SaveChangesAsync();

        var pending = await _wake.PendingAsync(alias, lastSeen, null, null, null, CancellationToken.None);
        pending.Signals.Select(s => s.Excerpt).ShouldBe(["approved"]);

        var replay = await _wake.PendingAsync(
            alias, lastSeen, DateTime.UtcNow.AddDays(-1), null, null, CancellationToken.None);
        replay.Signals.Select(s => s.Excerpt).ShouldBe(["approved earlier", "approved"]);
    }

    [Fact]
    public async Task Pending_ScopesRepliesToProjectId_AndStaysMachineWideWhenNull()
    {
        var other = Project.Create($"wake-other-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(other);
        var alias = "billy";
        var lastSeen = DateTime.UtcNow.AddHours(-2);
        _context.AgentSessions.Add(AgentSession.Restore(
            Guid.NewGuid(), alias, "claude", lastSeen.AddHours(-1), lastSeen));
        _context.AgentNotifications.Add(AgentNotification.Restore(
            Guid.NewGuid(), _projectId, NotificationKind.Approval, null, null,
            "home gate", alias, DateTime.UtcNow.AddMinutes(-30),
            replyText: "home reply", repliedAt: DateTime.UtcNow.AddMinutes(-20)));
        _context.AgentNotifications.Add(AgentNotification.Restore(
            Guid.NewGuid(), other.Id, NotificationKind.Approval, null, null,
            "foreign gate", alias, DateTime.UtcNow.AddMinutes(-30),
            replyText: "foreign reply", repliedAt: DateTime.UtcNow.AddMinutes(-15)));
        await _context.SaveChangesAsync();

        var scoped = await _wake.PendingAsync(
            alias, lastSeen, null, null, _projectId, CancellationToken.None);
        scoped.Signals.Select(s => s.Excerpt).ShouldBe(["home reply"]);

        var machineWide = await _wake.PendingAsync(alias, lastSeen, null, null, null, CancellationToken.None);
        machineWide.Signals.Select(s => s.Excerpt).ShouldBe(["home reply", "foreign reply"]);
    }

    [Fact]
    public async Task Pending_ReplyLookback_FollowsEnvironment_AndClampsToOneDay()
    {
        var alias = "billy";
        var lastSeen = DateTime.UtcNow.AddHours(-2);
        _context.AgentSessions.Add(AgentSession.Restore(
            Guid.NewGuid(), alias, "claude", lastSeen.AddHours(-1), lastSeen));
        _context.AgentNotifications.Add(AgentNotification.Restore(
            Guid.NewGuid(), _projectId, NotificationKind.Approval, null, null,
            "ancient gate", alias, DateTime.UtcNow.AddDays(-3),
            replyText: "three days old", repliedAt: DateTime.UtcNow.AddDays(-2)));
        _context.AgentNotifications.Add(AgentNotification.Restore(
            Guid.NewGuid(), _projectId, NotificationKind.Approval, null, null,
            "fresh gate", alias, DateTime.UtcNow.AddMinutes(-30),
            replyText: "still pending", repliedAt: DateTime.UtcNow.AddMinutes(-20)));
        await _context.SaveChangesAsync();

        var defaultWindow = await _wake.PendingAsync(alias, lastSeen, null, null, null, CancellationToken.None);
        defaultWindow.Signals.Select(s => s.Excerpt).ShouldBe(["three days old", "still pending"]);

        try
        {
            Environment.SetEnvironmentVariable(WakeReplyLookback.EnvironmentVariable, "1");
            var narrowed = await _wake.PendingAsync(alias, lastSeen, null, null, null, CancellationToken.None);
            narrowed.Signals.Select(s => s.Excerpt).ShouldBe(["still pending"]);

            Environment.SetEnvironmentVariable(WakeReplyLookback.EnvironmentVariable, "0");
            var clamped = await _wake.PendingAsync(alias, lastSeen, null, null, null, CancellationToken.None);
            clamped.Signals.Select(s => s.Excerpt).ShouldBe(["still pending"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(WakeReplyLookback.EnvironmentVariable, null);
        }
    }

    [Theory]
    [InlineData(null, 7)]
    [InlineData("", 7)]
    [InlineData("junk", 7)]
    [InlineData("2.5", 7)]
    [InlineData("3", 3)]
    [InlineData(" 4 ", 4)]
    [InlineData("0", 1)]
    [InlineData("-9", 1)]
    [InlineData("366", 365)]
    [InlineData("2147483647", 365)]
    public void ResolveDays_EnvironmentValue_DefaultsAndClamps(string? raw, int expectedDays)
    {
        Environment.SetEnvironmentVariable(WakeReplyLookback.EnvironmentVariable, raw);
        try
        {
            WakeReplyLookback.ResolveDays().ShouldBe(expectedDays);
            WakeReplyLookback.Resolve().ShouldBe(TimeSpan.FromDays(expectedDays));
        }
        finally
        {
            Environment.SetEnvironmentVariable(WakeReplyLookback.EnvironmentVariable, null);
        }
    }

    [Fact]
    public async Task Snapshot_UnknownAlias_ReturnsNull()
    {
        (await _wake.SnapshotAsync("ghost", CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Snapshot_LowercasedFanoutAlias_ResolvesTheVerbatimSession()
    {
        var lastSeen = DateTime.UtcNow.AddHours(-2);
        _context.AgentSessions.Add(AgentSession.Restore(
            Guid.NewGuid(), "Dan", "claude", lastSeen.AddHours(-1), lastSeen));
        _context.WaveMessages.Add(WaveMessage.Create(
            _projectId, "all", "erin", "worker", "ping @Dan"));
        await _context.SaveChangesAsync();

        var snapshot = await _wake.SnapshotAsync("dan", CancellationToken.None);

        snapshot.ShouldNotBeNull();
        snapshot.Signals.Select(s => s.Kind).ShouldBe([AgentWakeService.MentionKind]);
    }
}
