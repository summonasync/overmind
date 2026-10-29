using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ReplyAutoActFanoutTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly ReplyAutoActQueue _queue;
    private readonly Guid _projectId;

    public ReplyAutoActFanoutTests()
    {
        _context = _db.CreateContext();
        _queue = new ReplyAutoActQueue();
        var project = Project.Create($"fanout-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
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
    public async Task Dispatch_FlagOnApprovalWithSlug_EnqueuesNotification()
    {
        await SetAutoActAsync(true);
        var notification = CreateNotification(NotificationKind.Approval, "wake");

        await CreateFanout().TryDispatchAsync(notification, CancellationToken.None);

        _queue.PendingCount.ShouldBe(1);
    }

    [Fact]
    public async Task Dispatch_FlagOff_DoesNotEnqueue()
    {
        var notification = CreateNotification(NotificationKind.Approval, "sleepy");

        await CreateFanout().TryDispatchAsync(notification, CancellationToken.None);

        _queue.PendingCount.ShouldBe(0);
    }

    [Fact]
    public async Task Dispatch_QuestionKind_DoesNotEnqueue()
    {
        await SetAutoActAsync(true);
        var notification = CreateNotification(NotificationKind.Question, "asked");

        await CreateFanout().TryDispatchAsync(notification, CancellationToken.None);

        _queue.PendingCount.ShouldBe(0);
    }

    [Fact]
    public async Task Dispatch_ApprovalWithoutSlug_DoesNotEnqueue()
    {
        await SetAutoActAsync(true);
        var notification = CreateNotification(NotificationKind.Approval, slug: null);

        await CreateFanout().TryDispatchAsync(notification, CancellationToken.None);

        _queue.PendingCount.ShouldBe(0);
    }

    [Fact]
    public async Task Dispatch_UnknownProject_DoesNotThrow()
    {
        var notification = AgentNotification.Restore(
            Guid.NewGuid(), null, NotificationKind.Approval, "any", "spec",
            "approve", "agent", DateTime.UtcNow,
            replyText: "approved", repliedAt: DateTime.UtcNow);

        await CreateFanout().TryDispatchAsync(notification, CancellationToken.None);

        _queue.PendingCount.ShouldBe(0);
    }

    [Fact]
    public async Task Dispatch_QueueFull_DoesNotThrowAndLogsDrop()
    {
        await SetAutoActAsync(true);
        var full = new ReplyAutoActQueue(capacity: 1);
        full.TryEnqueue(Guid.NewGuid()).ShouldBeTrue();
        var logs = new List<string>();
        var notification = CreateNotification(NotificationKind.Approval, "overflow");

        await CreateFanout(full, logs).TryDispatchAsync(notification, CancellationToken.None);

        full.PendingCount.ShouldBe(1);
        var error = logs.Single(l => l.Contains("Error"));
        error.ShouldContain(notification.Id.ToString());
        error.ShouldContain("nothing is persisted");
    }

    [Fact]
    public async Task Dispatch_DatabaseFailure_IsSwallowed()
    {
        var broken = _db.CreateContext();
        broken.Dispose();
        var notification = CreateNotification(NotificationKind.Approval, "wrecked");

        await new ReplyAutoActFanout(broken, _queue, NullLogger<ReplyAutoActFanout>.Instance)
            .TryDispatchAsync(notification, CancellationToken.None);

        _queue.PendingCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReplyAsync_FanoutFailure_StillPersistsReply()
    {
        var broken = _db.CreateContext();
        broken.Dispose();
        var notification = AgentNotification.Create(
            NotificationKind.Approval, "approve resilient", _projectId, "resilient", "spec");
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();
        var service = new NotificationsService(
            _context,
            new ReplyAutoActFanout(broken, _queue, NullLogger<ReplyAutoActFanout>.Instance));

        var replied = await service.ReplyAsync(notification.Id, "approved", CancellationToken.None);

        replied.ShouldNotBeNull();
        replied.ReplyText.ShouldBe("approved");
        replied.RepliedAt.ShouldNotBeNull();
    }

    private AgentNotification CreateNotification(NotificationKind kind, string? slug) =>
        AgentNotification.Restore(
            Guid.NewGuid(), _projectId, kind, slug, "spec",
            "approve", "agent", DateTime.UtcNow.AddMinutes(-1),
            replyText: "approved", repliedAt: DateTime.UtcNow);

    private async Task SetAutoActAsync(bool enabled)
    {
        var project = await _context.Projects.SingleAsync(p => p.Id == _projectId);
        project.SetAutoActOnReplies(enabled);
        await _context.SaveChangesAsync();
    }

    private ReplyAutoActFanout CreateFanout(ReplyAutoActQueue? queue = null, List<string>? logs = null) =>
        new(
            _context,
            queue ?? _queue,
            logs is null
                ? NullLogger<ReplyAutoActFanout>.Instance
                : new CollectingLogger<ReplyAutoActFanout>("fanout", logs));

    private sealed class CollectingLogger<T>(string name, List<string> sink) : ILogger<T>
        where T : notnull
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (sink)
            {
                sink.Add($"{name} {logLevel}: {formatter(state, exception)}");
            }
        }
    }
}
