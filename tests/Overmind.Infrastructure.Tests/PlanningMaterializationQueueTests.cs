using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningMaterializationQueueTests
{
    [Fact]
    public void TryEnqueue_TwoProjects_SecondIsNotLost()
    {
        var queue = new PlanningMaterializationQueue();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        queue.TryEnqueue(first).ShouldBeTrue();
        queue.TryEnqueue(second).ShouldBeTrue();

        queue.PendingCount.ShouldBe(2);
    }

    [Fact]
    public void Reader_ItemsLeaveInEnqueueOrder()
    {
        var queue = new PlanningMaterializationQueue();
        var ordered = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        foreach (var id in ordered)
        {
            queue.TryEnqueue(id).ShouldBeTrue();
        }

        var drained = new List<Guid>();
        while (queue.Reader.TryRead(out var id))
        {
            drained.Add(id);
        }

        drained.ShouldBe(ordered);
    }

    [Fact]
    public void TryEnqueue_FullQueue_ReturnsFalse()
    {
        var queue = new PlanningMaterializationQueue(capacity: 1);
        queue.TryEnqueue(Guid.NewGuid()).ShouldBeTrue();

        queue.TryEnqueue(Guid.NewGuid()).ShouldBeFalse();
    }
}
