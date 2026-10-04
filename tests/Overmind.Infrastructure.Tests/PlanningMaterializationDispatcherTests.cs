using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningMaterializationDispatcherTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;

    public PlanningMaterializationDispatcherTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], [], []));
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Dispatch_DefaultFlag_EnqueuesWithoutRunningThePass()
    {
        var queue = new PlanningMaterializationQueue();
        var dispatcher = CreateDispatcher(queue);
        var projectId = Guid.NewGuid();

        await dispatcher.DispatchAsync(projectId);

        queue.PendingCount.ShouldBe(1);
        await _materializer.DidNotReceiveWithAnyArgs().SyncAsync(default, default, default);
    }

    [Fact]
    public async Task Dispatch_SyncFlagOn_RunsInlineAndEnqueuesNothing()
    {
        await TestPlanningDispatcher.EnableSyncModeAsync(_context);
        var queue = new PlanningMaterializationQueue();
        var dispatcher = CreateDispatcher(queue);
        var projectId = Guid.NewGuid();

        await dispatcher.DispatchAsync(projectId);

        queue.PendingCount.ShouldBe(0);
        await _materializer.Received(1).SyncAsync(projectId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispatch_QueueFull_FallsBackToInline()
    {
        var queue = new PlanningMaterializationQueue(capacity: 1);
        queue.TryEnqueue(Guid.NewGuid()).ShouldBeTrue();
        var dispatcher = CreateDispatcher(queue);
        var projectId = Guid.NewGuid();

        await dispatcher.DispatchAsync(projectId);

        queue.PendingCount.ShouldBe(1);
        await _materializer.Received(1).SyncAsync(projectId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchInline_AlwaysRunsThePass_EvenWhenAsyncIsDefault()
    {
        var queue = new PlanningMaterializationQueue();
        var dispatcher = CreateDispatcher(queue);
        var projectId = Guid.NewGuid();

        await dispatcher.DispatchInlineAsync(projectId);

        queue.PendingCount.ShouldBe(0);
        await _materializer.Received(1).SyncAsync(projectId, false, Arg.Any<CancellationToken>());
    }

    private PlanningMaterializationDispatcher CreateDispatcher(PlanningMaterializationQueue queue) =>
        TestPlanningDispatcher.Create(_context, _materializer, queue);
}
