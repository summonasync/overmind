using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wake;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

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
        var api = await _wake.PendingAsync(alias, lastSeen, null, null, CancellationToken.None);

        snapshot.ShouldNotBeNull();
        snapshot.ShouldBeEquivalentTo(api);
        snapshot.Signals.Select(s => s.Kind).ShouldBe([
            AgentWakeService.ReplyKind,
            AgentWakeService.MentionKind
        ]);
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
