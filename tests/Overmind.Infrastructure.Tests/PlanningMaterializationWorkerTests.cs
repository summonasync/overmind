using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Overmind.Core;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningMaterializationWorkerTests
{
    [Fact]
    public async Task Execute_ProcessesPendingProjects_InEnqueueOrder()
    {
        var (worker, queue, materializer) = BuildWorker();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var processed = new List<Guid>();
        var bothDone = new TaskCompletionSource();

        materializer
            .SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], [], []))
            .AndDoes(call =>
            {
                lock (processed)
                {
                    processed.Add(call.ArgAt<Guid>(0));
                    if (processed.Count == 2)
                    {
                        bothDone.TrySetResult();
                    }
                }
            });

        queue.TryEnqueue(first).ShouldBeTrue();
        queue.TryEnqueue(second).ShouldBeTrue();

        using var cts = new CancellationTokenSource();
        var run = Task.Run(() => worker.ExecuteAsync(cts.Token));
        await bothDone.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cts.Cancel();
        await run;

        lock (processed)
        {
            processed.ShouldBe([first, second]);
        }
    }

    [Fact]
    public async Task Execute_PassFails_FailureLandsInRecordSyncFailure()
    {
        var (worker, queue, materializer) = BuildWorker();
        var projectId = Guid.NewGuid();
        var recorded = new TaskCompletionSource();
        var recordedMessage = string.Empty;

        materializer
            .SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("disk exploded"));
        materializer
            .RecordSyncFailureAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                recordedMessage = call.ArgAt<string>(1);
                recorded.TrySetResult();
            });

        queue.TryEnqueue(projectId).ShouldBeTrue();

        using var cts = new CancellationTokenSource();
        var run = Task.Run(() => worker.ExecuteAsync(cts.Token));
        await recorded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cts.Cancel();
        await run;

        recordedMessage.ShouldContain("disk exploded");
    }

    [Fact]
    public async Task Execute_CancelledWhileFirstPassRuns_StillDrainsPendingAtShutdown()
    {
        var (worker, queue, materializer) = BuildWorker();
        var firstStarted = new TaskCompletionSource();
        var releaseFirst = new TaskCompletionSource();
        var second = Guid.NewGuid();
        var secondDone = new TaskCompletionSource();

        materializer
            .SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], [], []))
            .AndDoes(call =>
            {
                if (call.ArgAt<Guid>(0) == second)
                {
                    secondDone.TrySetResult();
                    return;
                }

                firstStarted.TrySetResult();
                releaseFirst.Task.Wait();
            });

        queue.TryEnqueue(Guid.NewGuid()).ShouldBeTrue();
        queue.TryEnqueue(second).ShouldBeTrue();

        using var cts = new CancellationTokenSource();
        var run = Task.Run(() => worker.ExecuteAsync(cts.Token));
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cts.Cancel();
        releaseFirst.TrySetResult();
        await run;

        secondDone.Task.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_PassCancelledMidFlight_ShutdownReRunsItBeforeDraining()
    {
        var (worker, queue, materializer) = BuildWorker();
        var cancelledProject = Guid.NewGuid();
        var pending = Guid.NewGuid();
        var pendingDone = new TaskCompletionSource();
        var calls = new List<(Guid Project, CancellationToken Token)>();

        using var cts = new CancellationTokenSource();
        materializer
            .SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], [], []))
            .AndDoes(call =>
            {
                var project = call.ArgAt<Guid>(0);
                var token = call.ArgAt<CancellationToken>(3);
                lock (calls)
                {
                    calls.Add((project, token));
                    if (calls.Count == 1)
                    {
                        cts.Cancel();
                    }
                }

                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }

                if (project == pending)
                {
                    pendingDone.TrySetResult();
                }
            });

        queue.TryEnqueue(cancelledProject).ShouldBeTrue();
        queue.TryEnqueue(pending).ShouldBeTrue();

        var run = Task.Run(() => worker.ExecuteAsync(cts.Token));
        await pendingDone.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await run;

        lock (calls)
        {
            var uncancelled = calls
                .Where(c => !c.Token.IsCancellationRequested)
                .Select(c => c.Project)
                .ToList();
            uncancelled.Count.ShouldBe(2);
            uncancelled.ShouldContain(cancelledProject);
            uncancelled.ShouldContain(pending);
        }
    }

    [Fact]
    public async Task Execute_EmptyQueue_Cancelled_ReturnsPromptly()
    {
        var (worker, _, _) = BuildWorker();

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await worker.ExecuteAsync(cts.Token);
    }

    [Fact]
    public async Task Execute_AfterThePassLands_PerProjectPendingClears()
    {
        var (worker, queue, materializer) = BuildWorker();
        var projectId = Guid.NewGuid();
        var landed = new TaskCompletionSource();

        materializer
            .SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], [], []))
            .AndDoes(_ => landed.TrySetResult());

        queue.TryEnqueue(projectId).ShouldBeTrue();
        queue.PendingCountFor(projectId).ShouldBe(1);

        using var cts = new CancellationTokenSource();
        var run = Task.Run(() => worker.ExecuteAsync(cts.Token));
        await landed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        queue.PendingCountFor(projectId).ShouldBe(0);

        cts.Cancel();
        await run;
    }

    private static (
        PlanningMaterializationWorker Worker,
        PlanningMaterializationQueue Queue,
        IPlanningMaterializer Materializer) BuildWorker()
    {
        var materializer = Substitute.For<IPlanningMaterializer>();
        var scopeFactory = new ServiceCollection()
            .AddSingleton(materializer)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        var queue = new PlanningMaterializationQueue();
        var worker = new PlanningMaterializationWorker(
            queue, scopeFactory, NullLogger<PlanningMaterializationWorker>.Instance);
        return (worker, queue, materializer);
    }
}
