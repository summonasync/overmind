using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Overmind.Infrastructure.Wake;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ReplyAutoActWorkerTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;

    public ReplyAutoActWorkerTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer
            .SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], [], []));

        var project = Project.Create($"worker-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        project.SetAutoActOnReplies(true);
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
    public async Task Execute_EnqueuedReply_ValidatesArtifactEndToEnd()
    {
        var feature = PlanningFeature.Create(_projectId, "woken", "woken", string.Empty);
        var artifact = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Spec, "spec body");
        var notification = AgentNotification.Restore(
            Guid.NewGuid(), _projectId, NotificationKind.Approval, "woken", "spec",
            "approve woken", "agent", DateTime.UtcNow.AddMinutes(-1),
            replyText: "approved", repliedAt: DateTime.UtcNow);
        _context.AddRange(feature, artifact, notification);
        await _context.SaveChangesAsync();

        var (worker, queue, logs) = BuildWorker();
        queue.TryEnqueue(notification.Id).ShouldBeTrue();
        await RunUntilAsync(worker, logs, () => _context.AgentNotifications
            .AnyAsync(n => n.Kind == NotificationKind.Info));

        (await _context.PlanningArtifacts
                .AsNoTracking()
                .SingleAsync(a => a.Id == artifact.Id))
            .Status.ShouldBe(PlanningArtifactStatus.Validated);
        (await _context.AgentNotifications
                .AsNoTracking()
                .SingleAsync(n => n.Kind == NotificationKind.Info))
            .Agent.ShouldBe("agent");
    }

    [Fact]
    public async Task Execute_SkippedReply_LogsSkipReasonWithoutSideEffects()
    {
        var notification = AgentNotification.Restore(
            Guid.NewGuid(), _projectId, NotificationKind.Approval, "ghost", "spec",
            "approve ghost", "agent", DateTime.UtcNow.AddMinutes(-1),
            replyText: "approved", repliedAt: DateTime.UtcNow);
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();

        var (worker, queue, logs) = BuildWorker();
        queue.TryEnqueue(notification.Id).ShouldBeTrue();
        await RunUntilAsync(worker, logs, () => Task.FromResult(
            logs.Any(l => l.Contains("skipped") && l.Contains(notification.Id.ToString()))));

        var skip = logs.Single(l => l.Contains("skipped"));
        skip.ShouldContain("not found");
        queue.TryDequeue(out _).ShouldBeFalse();
        (await _context.AgentNotifications
                .AnyAsync(n => n.Kind == NotificationKind.Info))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_ProcessorFails_RetriesBoundedThenDrops()
    {
        var attempts = 0;
        var exhausted = new TaskCompletionSource();
        var processor = Substitute.For<IReplyAutoActProcessor>();
        processor
            .ProcessAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("disk exploded"))
            .AndDoes(_ =>
            {
                if (++attempts == 3)
                {
                    exhausted.TrySetResult();
                }
            });

        var (worker, queue, _) = BuildWorker(processor);
        queue.TryEnqueue(Guid.NewGuid()).ShouldBeTrue();

        using var cts = new CancellationTokenSource();
        var run = Task.Run(() => worker.ExecuteAsync(cts.Token));
        await exhausted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cts.Cancel();
        await run;

        attempts.ShouldBe(3);
        queue.TryDequeue(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_RequeueWhileQueueFull_DropsItemAndRestoresAttemptBudget()
    {
        var dropped = Guid.NewGuid();
        var spare = Guid.NewGuid();
        var firstAttempt = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var spareDone = new TaskCompletionSource();
        var droppedFailures = 0;

        var processor = Substitute.For<IReplyAutoActProcessor>();
        processor
            .ProcessAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var id = call.Arg<Guid>();
                if (id == spare)
                {
                    spareDone.TrySetResult();
                    return ReplyAutoActResult.Performed("spare closed");
                }

                if (id == dropped)
                {
                    droppedFailures++;
                    if (droppedFailures == 1)
                    {
                        firstAttempt.TrySetResult();
                        await release.Task;
                    }

                    throw new InvalidOperationException("flaky storage");
                }

                return ReplyAutoActResult.Performed("ok");
            });

        var queue = new ReplyAutoActQueue(capacity: 1);
        var (worker, _, logs) = BuildWorker(processor, queue);
        queue.TryEnqueue(dropped).ShouldBeTrue();

        using var cts = new CancellationTokenSource();
        var run = Task.Run(() => worker.ExecuteAsync(cts.Token));

        await firstAttempt.Task.WaitAsync(TimeSpan.FromSeconds(10));
        queue.TryEnqueue(spare).ShouldBeTrue();
        release.TrySetResult();
        await spareDone.Task.WaitAsync(TimeSpan.FromSeconds(10));

        queue.TryEnqueue(dropped).ShouldBeTrue();
        var droppedId = dropped.ToString();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline
               && !logs.Any(l => l.Contains("gave up") && l.Contains(droppedId)))
        {
            await Task.Delay(50, CancellationToken.None);
        }

        cts.Cancel();
        await run;

        droppedFailures.ShouldBe(4);
        logs.Count(l => l.Contains("requeued") && l.Contains(droppedId)).ShouldBe(2);
        logs.Count(l => l.Contains("Error") && l.Contains("dropped") && l.Contains(droppedId))
            .ShouldBe(1);
        queue.TryDequeue(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_EmptyQueue_Cancelled_ReturnsPromptly()
    {
        var (worker, _, _) = BuildWorker();

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await worker.ExecuteAsync(cts.Token);
    }

    private (ReplyAutoActWorker Worker, ReplyAutoActQueue Queue, List<string> Logs) BuildWorker(
        IReplyAutoActProcessor? processor = null, ReplyAutoActQueue? queue = null)
    {
        queue ??= new ReplyAutoActQueue();
        var logs = new List<string>();
        var workerContext = _db.CreateContext();
        var dispatcher = TestPlanningDispatcher.Create(workerContext, _materializer);
        var services = new ServiceCollection()
            .AddSingleton(workerContext)
            .AddSingleton(new AppSettingsService(workerContext))
            .AddSingleton(new PlanningPolicy(workerContext, new AppSettingsService(workerContext)))
            .AddSingleton(_materializer)
            .AddSingleton(new TaskDependencyService(workerContext, dispatcher))
            .AddSingleton(new PlanningArtifactService(
                workerContext, dispatcher, new TaskDependencyService(workerContext, dispatcher)))
            .AddSingleton(new PlanningFeatureService(
                workerContext,
                _materializer,
                new BacklogImportSeeder(
                    workerContext, new PlanningPolicy(workerContext, new AppSettingsService(workerContext))),
                new TaskDependencyService(workerContext, dispatcher),
                dispatcher,
                NullLogger<PlanningFeatureService>.Instance))
            .AddSingleton(new FeatureDependencyService(workerContext, dispatcher))
            .AddSingleton(new NotificationsService(
                workerContext,
                new ReplyAutoActFanout(workerContext, queue, NullLogger<ReplyAutoActFanout>.Instance),
                new WakeDropFanout(
                    new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                    NullLogger<WakeDropFanout>.Instance),
                []))
            .AddSingleton<ILogger<ReplyAutoActProcessor>>(NullLogger<ReplyAutoActProcessor>.Instance)
            .AddSingleton<ReplyAutoActProcessor>();

        if (processor is null)
        {
            services.AddSingleton<IReplyAutoActProcessor>(
                provider => provider.GetRequiredService<ReplyAutoActProcessor>());
        }
        else
        {
            services.AddSingleton(processor);
        }

        services.AddSingleton(queue);
        services.AddSingleton<ILogger<ReplyAutoActWorker>>(new CollectingLogger<ReplyAutoActWorker>("worker", logs));
        services.AddSingleton<ReplyAutoActWorker>();

        var worker = services
            .BuildServiceProvider()
            .GetRequiredService<ReplyAutoActWorker>();
        return (worker, queue, logs);
    }

    private static async Task RunUntilAsync(
        ReplyAutoActWorker worker, List<string> logs, Func<Task<bool>> until)
    {
        using var cts = new CancellationTokenSource();
        var run = Task.Run(() => worker.ExecuteAsync(cts.Token));
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                if (await until())
                {
                    return;
                }

                await Task.Delay(50, CancellationToken.None);
            }

            throw new TimeoutException(
                "auto-act worker did not settle in time. Worker log:\n" + string.Join("\n", logs));
        }
        finally
        {
            cts.Cancel();
            await run;
        }
    }

    private sealed class CollectingLogger<T>(string name, List<string>? sink = null) : ILogger<T>
        where T : notnull
    {
        private readonly List<string> _lines = sink ?? [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_lines)
            {
                _lines.Add($"{name} {logLevel}: {formatter(state, exception)} :: {exception?.GetType().Name}");
            }
        }
    }
}
