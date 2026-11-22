using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wake;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class NotificationsReplyCursorTests : IDisposable
{
    private static readonly DateTime Basis = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly NotificationsService _service;
    private readonly AgentSession _session;
    private readonly ServiceProvider _wakeServices;

    public NotificationsReplyCursorTests()
    {
        _context = _db.CreateContext();
        (_service, _wakeServices) = CreateService();
        _session = AgentSession.Create("dan", "claude");
        _context.AgentSessions.Add(_session);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _wakeServices.Dispose();
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task ListReplies_PagedWalk_AdvancesOnlyOnTheFinalPage()
    {
        var seeded = SeedSpread(NotificationsService.ListCapacity * 2 + 20);

        var pageOne = await CallAsync(null);

        pageOne.Count.ShouldBe(NotificationsService.ListCapacity);
        pageOne.Select(n => n.Id).ShouldBe(seeded.TakeLast(NotificationsService.ListCapacity).Reverse().Select(n => n.Id));
        _session.ReplyCursorAt.ShouldBeNull("a full page is mid-walk and must not advance");

        var boundary = pageOne[^1].RepliedAt!.Value;
        var pageTwo = await CallAsync(boundary);

        pageTwo.Select(n => n.Id).ShouldBe(
            seeded.Where(n => n.RepliedAt > boundary)
                .OrderByDescending(n => n.RepliedAt)
                .Select(n => n.Id));
        pageTwo.ShouldNotContain(n => n.RepliedAt <= boundary);
        _session.ReplyCursorAt.ShouldBe(pageTwo[0].RepliedAt);
        _session.ReplyCursorAt.ShouldBe(seeded[^1].RepliedAt);
    }

    [Fact]
    public async Task ListReplies_ReadFilterHoldsOnEveryPage_AndYieldsOnlyWhenRequested()
    {
        var seeded = SeedSpread(NotificationsService.ListCapacity * 2 + 30, i => i % 3 == 0);
        var readIds = seeded.Where(n => n.ReadAt is not null).Select(n => n.Id).ToHashSet();
        readIds.Count.ShouldBeGreaterThan(0);

        var mid = seeded[seeded.Count / 2].RepliedAt!.Value;
        var aboveMid = seeded.Count(n => n.RepliedAt > mid);
        var unreadAboveMid = seeded.Count(n => n.ReadAt is null && n.RepliedAt > mid);

        var withoutSince = await CallAsync(null, includeRead: false);
        var withSince = await CallAsync(mid, includeRead: false);
        var readable = await CallAsync(mid, includeRead: true);

        foreach (var page in new[] { withoutSince, withSince })
        {
            page.ShouldNotContain(n => readIds.Contains(n.Id));
            page.All(n => n.ReplyText != null).ShouldBeTrue();
        }

        withoutSince.Count.ShouldBe(Math.Min(NotificationsService.ListCapacity, seeded.Count(n => n.ReadAt is null)));
        withSince.Count.ShouldBe(unreadAboveMid);
        readable.Count.ShouldBe(Math.Min(NotificationsService.ListCapacity, aboveMid));
        readable.Count(n => readIds.Contains(n.Id)).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ListReplies_EmptyPage_DoesNotAdvance()
    {
        SeedReply(minutesAgo: 30);
        SeedReply(minutesAgo: 10);
        await _context.SaveChangesAsync();

        var empty = await CallAsync(Basis.AddMinutes(-5));

        empty.ShouldBeEmpty();
        _session.ReplyCursorAt.ShouldBeNull();

        await SetCursorAsync(Basis.AddMinutes(-60));
        empty = await CallAsync(Basis.AddMinutes(-5));

        empty.ShouldBeEmpty();
        _session.ReplyCursorAt.ShouldBe(Basis.AddMinutes(-60));
    }

    [Fact]
    public async Task ListReplies_FinalPartialPage_AdvancesToMaxDeliveredRepliedAt()
    {
        var older = SeedReply(minutesAgo: 40);
        var newest = SeedReply(minutesAgo: 10);
        await _context.SaveChangesAsync();

        var replies = await CallAsync(null);

        replies.Select(n => n.Id).ShouldBe([newest.Id, older.Id]);
        _session.ReplyCursorAt.ShouldBe(newest.RepliedAt);

        var consumed = await CallAsync(newest.RepliedAt!.Value);

        consumed.ShouldBeEmpty();
        _session.ReplyCursorAt.ShouldBe(newest.RepliedAt);
    }

    [Fact]
    public async Task ListReplies_OlderDelivery_CannotMoveTheCursorBack()
    {
        SeedReply(minutesAgo: 40);
        var newest = SeedReply(minutesAgo: 10);
        await _context.SaveChangesAsync();

        await CallAsync(null);
        _session.ReplyCursorAt.ShouldBe(newest.RepliedAt);

        var stale = await CallAsync(Basis.AddMinutes(-30));

        stale.Select(n => n.Id).ShouldBe([newest.Id]);
        _session.ReplyCursorAt.ShouldBe(newest.RepliedAt);
    }

    [Fact]
    public async Task ListReplies_WithSince_AdvancesByDeliveredRowsNotByTheRequestWindow()
    {
        SeedReply(minutesAgo: 90);
        var middle = SeedReply(minutesAgo: 60);
        var newest = SeedReply(minutesAgo: 10);
        await _context.SaveChangesAsync();

        var narrowed = await CallAsync(Basis.AddMinutes(-70));

        narrowed.Select(n => n.Id).ShouldBe([newest.Id, middle.Id]);
        _session.ReplyCursorAt.ShouldBe(newest.RepliedAt);
        _session.ReplyCursorAt.ShouldNotBe(Basis.AddMinutes(-70));
    }

    [Fact]
    public async Task ListReplies_WithoutACallerSession_DoesNotAdvanceAnything()
    {
        var other = AgentSession.Create("guldan", "qwen");
        _context.AgentSessions.Add(other);
        SeedReply(minutesAgo: 30);
        await _context.SaveChangesAsync();

        var headerless = await _service.ListRepliesAsync(
            null, null, includeRead: true, since: null, kind: null, featureSlug: null, CancellationToken.None);
        headerless.Count.ShouldBe(1);

        var unknown = await CallAsync(null, sessionId: Guid.NewGuid());

        unknown.Count.ShouldBe(1);
        _session.ReplyCursorAt.ShouldBeNull();
        other.ReplyCursorAt.ShouldBeNull();
    }

    [Fact]
    public async Task ListReplies_FreshSession_FirstConsumingReadAdvancesFromNull()
    {
        var ancient = SeedReply(minutesAgo: 60 * 24 * 9);
        var recent = SeedReply(minutesAgo: 60 * 24);
        await _context.SaveChangesAsync();

        var wake = new AgentWakeService(_context);
        var before = await wake.PendingAsync("dan", DateTime.UtcNow, null, null, null, CancellationToken.None);
        before.Signals.ShouldBeEmpty();

        var replies = await CallAsync(null);

        replies.Select(n => n.Id).ShouldBe([recent.Id, ancient.Id]);
        _session.ReplyCursorAt.ShouldBe(recent.RepliedAt);

        var after = await wake.PendingAsync("dan", DateTime.UtcNow, null, null, null, CancellationToken.None);
        after.Signals.ShouldBeEmpty();
    }

    private (NotificationsService Service, ServiceProvider Services) CreateService()
    {
        var broken = _db.CreateContext();
        broken.Dispose();
        var (wakeFanout, services) = TestWakeFanout.Create(_db);
        var service = new NotificationsService(
            _context,
            new ReplyAutoActFanout(broken, new ReplyAutoActQueue(), NullLogger<ReplyAutoActFanout>.Instance),
            wakeFanout,
            []);

        return (service, services);
    }

    private List<AgentNotification> SeedSpread(int total, Func<int, bool>? read = null)
    {
        var seeded = new List<AgentNotification>();
        for (var i = 1; i <= total; i++)
        {
            seeded.Add(SeedReply(minutesAgo: total + 1 - i, read: read?.Invoke(i) ?? false));
        }

        _context.SaveChanges();

        return seeded;
    }

    private async Task<IReadOnlyList<AgentNotification>> CallAsync(
        DateTime? since, bool includeRead = true, Guid? sessionId = null) =>
        await _service.ListRepliesAsync(
            sessionId ?? _session.Id,
            projectId: null,
            includeRead,
            since,
            kind: null,
            featureSlug: null,
            CancellationToken.None);

    private async Task SetCursorAsync(DateTime at)
    {
        var row = await _context.AgentSessions.SingleAsync(s => s.Id == _session.Id);
        row.AdvanceReplyCursor(at);
        await _context.SaveChangesAsync();
    }

    private AgentNotification SeedReply(int minutesAgo, bool read = false)
    {
        var repliedAt = Basis.AddMinutes(-minutesAgo);
        var notification = AgentNotification.Restore(
            Guid.NewGuid(), null, NotificationKind.Question, null, null,
            $"ask {minutesAgo}", "dan", repliedAt.AddMinutes(-1),
            readAt: read ? repliedAt.AddSeconds(1) : null,
            replyText: $"answer {minutesAgo}",
            repliedAt: repliedAt);
        _context.AgentNotifications.Add(notification);

        return notification;
    }
}
