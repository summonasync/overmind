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

    [Fact]
    public void PendingCount_PerProject_CountsOnlyThatProjectsMutations()
    {
        var queue = new PlanningMaterializationQueue();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        queue.TryEnqueue(first).ShouldBeTrue();
        queue.TryEnqueue(first).ShouldBeTrue();
        queue.TryEnqueue(second).ShouldBeTrue();

        queue.PendingCountFor(first).ShouldBe(2);
        queue.PendingCountFor(second).ShouldBe(1);
        queue.PendingCountFor(Guid.NewGuid()).ShouldBe(0);
    }

    [Fact]
    public void TryDequeue_DecrementsOnlyTheConsumedProject()
    {
        var queue = new PlanningMaterializationQueue();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        queue.TryEnqueue(first).ShouldBeTrue();
        queue.TryEnqueue(first).ShouldBeTrue();
        queue.TryEnqueue(second).ShouldBeTrue();

        queue.TryDequeue(out var dequeued).ShouldBeTrue();
        dequeued.ShouldBe(first);
        queue.PendingCountFor(first).ShouldBe(1);
        queue.PendingCountFor(second).ShouldBe(1);

        while (queue.TryDequeue(out _))
        {
        }

        queue.PendingCountFor(first).ShouldBe(0);
        queue.PendingCountFor(second).ShouldBe(0);
    }

    [Fact]
    public async Task DequeueAllAsync_DecrementsAsItemsAreConsumed()
    {
        var queue = new PlanningMaterializationQueue();
        var projectId = Guid.NewGuid();
        queue.TryEnqueue(projectId).ShouldBeTrue();
        queue.TryEnqueue(projectId).ShouldBeTrue();

        await using var enumerator = queue.DequeueAllAsync().GetAsyncEnumerator();
        for (var consumed = 1; consumed <= 2; consumed++)
        {
            (await enumerator.MoveNextAsync()).ShouldBeTrue();
            enumerator.Current.ShouldBe(projectId);
            queue.PendingCountFor(projectId).ShouldBe(2 - consumed);
        }

        queue.PendingCountFor(projectId).ShouldBe(0);
    }
}
