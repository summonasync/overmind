using Overmind.Infrastructure.Notifications;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ReplyAutoActQueueTests
{
    [Fact]
    public void TryEnqueue_ItemsLeaveInEnqueueOrder()
    {
        var queue = new ReplyAutoActQueue();
        var ordered = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        foreach (var id in ordered)
        {
            queue.TryEnqueue(id).ShouldBeTrue();
        }

        queue.PendingCount.ShouldBe(3);

        var drained = new List<Guid>();
        while (queue.TryDequeue(out var id))
        {
            drained.Add(id);
        }

        drained.ShouldBe(ordered);
        queue.PendingCount.ShouldBe(0);
    }

    [Fact]
    public void TryEnqueue_FullQueue_ReturnsFalse()
    {
        var queue = new ReplyAutoActQueue(capacity: 1);

        queue.TryEnqueue(Guid.NewGuid()).ShouldBeTrue();
        queue.TryEnqueue(Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public void TryDequeue_EmptyQueue_ReturnsFalse()
    {
        new ReplyAutoActQueue().TryDequeue(out _).ShouldBeFalse();
    }
}
