using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wake;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

[Collection(nameof(ProcessEnvironment))]
public sealed class WakeReplyConsumersTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"wake-{Guid.NewGuid():N}");
    private readonly AgentWakeService _wake;
    private readonly WakeDropFanout _fanout;
    private readonly NotificationsService _notifications;
    private readonly ServiceProvider _services;
    private readonly AgentSession _session;

    public WakeReplyConsumersTests()
    {
        _context = _db.CreateContext();
        (_fanout, _services) = TestWakeFanout.Create(_db, _root);
        _wake = new AgentWakeService(_context);
        _notifications = new NotificationsService(
            _context,
            new ReplyAutoActFanout(
                _context, new ReplyAutoActQueue(), NullLogger<ReplyAutoActFanout>.Instance),
            _fanout,
            []);
        _session = AgentSession.Create("billy", "claude");
        _context.AgentSessions.Add(_session);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _services.Dispose();
        _context.Dispose();
        _db.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public async Task ApprovalReply_LightsPeekWatcherAndDrop_ThenTheConsumingReadDarkensAllThree()
    {
        var stale = SeedReply(minutesAgo: 90);
        await _context.SaveChangesAsync();

        var birth = await PeekAsync();
        birth.Signals.ShouldBeEmpty("a fresh session's window starts at now");

        var established = await ConsumeAsync();
        established.Select(n => n.Id).ShouldBe([stale.Id]);
        _session.ReplyCursorAt.ShouldBe(stale.RepliedAt);

        var approval = SeedApproval(minutesAgo: 2);
        await _context.SaveChangesAsync();
        await _notifications.ReplyAsync(approval.Id, "approved", CancellationToken.None);

        var lit = await PeekAsync();
        var reply = lit.Signals.Single();
        reply.Kind.ShouldBe(AgentWakeService.ReplyKind);
        reply.RefId.ShouldBe(approval.Id);
        reply.From.ShouldBe(AgentWakeService.ReplyFrom);

        var watcherPage = await _wake.SnapshotAsync(_session.Alias, CancellationToken.None);
        watcherPage.ShouldNotBeNull();
        watcherPage.Signals.Select(s => s.RefId).ShouldBe([approval.Id]);

        var file = Path.Combine(_root, "billy.json");
        await WaitUntilAsync(() => File.Exists(file));
        var drop = JsonSerializer.Deserialize<WakeDrop>(await File.ReadAllTextAsync(file), JsonOptions);
        drop.ShouldNotBeNull();
        var dropSignal = drop.Signals.Single();
        dropSignal.RefId.ShouldBe(approval.Id);
        drop.Cursor.ShouldBe($"{dropSignal.CreatedAt:O}|{dropSignal.RefId}");

        var consumed = await ConsumeAsync();
        consumed.Select(n => n.Id).ShouldBe([approval.Id, stale.Id]);
        _session.ReplyCursorAt.ShouldBe(approval.RepliedAt);

        (await PeekAsync()).Signals.ShouldBeEmpty();

        var dark = await _wake.SnapshotAsync(_session.Alias, CancellationToken.None);
        dark.ShouldNotBeNull();
        dark.Signals.ShouldBeEmpty();

        await _fanout.WakeAsync(_session.Alias);
        var drained = JsonSerializer.Deserialize<WakeDrop>(await File.ReadAllTextAsync(file), JsonOptions);
        drained.ShouldNotBeNull();
        drained.Signals.ShouldBeEmpty();
        drained.Cursor.ShouldNotContain("|");

        (await PeekAsync()).Signals.ShouldBeEmpty();
    }

    private async Task<AgentWakePage> PeekAsync() =>
        await _wake.PendingAsync(
            _session.Alias, _session.LastSeenAt, null, null, null, CancellationToken.None);

    private async Task<IReadOnlyList<AgentNotification>> ConsumeAsync() =>
        await _notifications.ListRepliesAsync(
            _session.Id, projectId: null, includeRead: true, since: null,
            kind: null, featureSlug: null, CancellationToken.None);

    private AgentNotification SeedReply(int minutesAgo)
    {
        var repliedAt = DateTime.UtcNow.AddMinutes(-minutesAgo);
        var notification = AgentNotification.Restore(
            Guid.NewGuid(), null, NotificationKind.Question, null, null,
            "wake noise", _session.Alias, repliedAt.AddMinutes(-1),
            replyText: "stale answer",
            repliedAt: repliedAt);
        _context.AgentNotifications.Add(notification);

        return notification;
    }

    private AgentNotification SeedApproval(int minutesAgo)
    {
        var createdAt = DateTime.UtcNow.AddMinutes(-minutesAgo);
        var notification = AgentNotification.Restore(
            Guid.NewGuid(), null, NotificationKind.Approval, null, null,
            "gate needs you", _session.Alias, createdAt);
        _context.AgentNotifications.Add(notification);

        return notification;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }
}
