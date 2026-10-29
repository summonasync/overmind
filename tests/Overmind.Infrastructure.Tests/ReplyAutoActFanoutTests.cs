using Microsoft.EntityFrameworkCore;
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

    private ReplyAutoActFanout CreateFanout() =>
        new(
            _context,
            _queue,
            NullLogger<ReplyAutoActFanout>.Instance);
}
