using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Overmind.Infrastructure.Wake;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningBatchServiceTests : IDisposable, IAsyncLifetime
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly PlanningBatchScope _batchScope = new();
    private readonly RecordingDeliveryChannel _delivery = new();
    private readonly Guid _projectId;

    public PlanningBatchServiceTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));
        _materializer.ClearAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));

        var project = Project.Create($"test-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;
    }

    public async Task InitializeAsync() =>
        await TestPlanningDispatcher.EnableSyncModeAsync(_context);

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    private PlanningFeatureService CreateFeatureService() =>
        new(
            _context,
            _materializer,
            new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
            new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)),
            TestPlanningDispatcher.Create(_context, _materializer),
            NullLogger<PlanningFeatureService>.Instance);

    private PlanningArtifactService CreateArtifactService() =>
        new(
            _context,
            TestPlanningDispatcher.Create(_context, _materializer),
            new TaskDependencyService(
                _context, TestPlanningDispatcher.Create(_context, _materializer)),
            _batchScope);

    private NotificationsService CreateNotificationsService() =>
        new(
            _context,
            new ReplyAutoActFanout(
                _context, new ReplyAutoActQueue(), NullLogger<ReplyAutoActFanout>.Instance),
            new WakeDropFanout(
                new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                NullLogger<WakeDropFanout>.Instance),
            [_delivery],
            _batchScope);

    private PlanningBatchService CreateBatchService() =>
        new(
            _context,
            CreateArtifactService(),
            CreateNotificationsService(),
            TestPlanningDispatcher.Create(_context, _materializer),
            _batchScope);

    [Fact]
    public async Task Batch_PatchAppendLogSetStatus_MatchesTheThreeStandaloneCalls()
    {
        var featureService = CreateFeatureService();
        var artifacts = CreateArtifactService();
        var standalone = await featureService.CreateAsync(_projectId, "parity-standalone", "A", "body");
        var batched = await featureService.CreateAsync(_projectId, "parity-batched", "B", "body");
        var taskA = await artifacts.UpsertAsync(
            standalone.Id, PlanningArtifactKind.Task, TaskBody(), null, null, null, default);
        var taskB = await artifacts.UpsertAsync(
            batched.Id, PlanningArtifactKind.Task, TaskBody(), null, null, null, default);

        await artifacts.PatchAsync(
            taskA.Id,
            new PlanningArtifactPatch(TickIndex: 1, LogNote: "ticked first item"),
            default,
            PlanningLogSource.Agent);
        await artifacts.AppendLogAsync(taskA.Id, "closing the task", default, PlanningLogSource.Agent);
        await artifacts.SetStatusAsync(
            taskA.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task closed", default);

        var batchService = CreateBatchService();
        var result = await batchService.ApplyAsync(_projectId, "parity-batched",
        [
            new PlanningBatchOp(
                PlanningBatchOpName.Patch,
                ArtifactId: taskB.Id,
                Patch: new PlanningArtifactPatch(TickIndex: 1, LogNote: "ticked first item")),
            new PlanningBatchOp(PlanningBatchOpName.AppendLog, ArtifactId: taskB.Id, Note: "closing the task"),
            new PlanningBatchOp(
                PlanningBatchOpName.SetStatus,
                ArtifactId: taskB.Id,
                Status: PlanningArtifactStatus.Done,
                LogNote: "task closed"),
        ]);

        result.FeatureSlug.ShouldBe("parity-batched");
        result.Results.Count.ShouldBe(3);
        result.Results[2].Payload.ShouldBeOfType<PlanningArtifactStatusResult>();

        using var verify = _db.CreateContext();
        var savedA = await verify.PlanningArtifacts.SingleAsync(a => a.Id == taskA.Id);
        var savedB = await verify.PlanningArtifacts.SingleAsync(a => a.Id == taskB.Id);
        savedB.Status.ShouldBe(savedA.Status);
        savedB.Version.ShouldBe(savedA.Version);
        savedB.Body.ShouldContain("[x] first item done");
        savedB.Body.ShouldContain("[ ] second item done");
        PlanningLog.Parse(savedB.Body).Select(l => l.Text)
            .ShouldBe(PlanningLog.Parse(savedA.Body).Select(l => l.Text));

        var featureA = await verify.PlanningFeatures.SingleAsync(f => f.Id == standalone.Id);
        var featureB = await verify.PlanningFeatures.SingleAsync(f => f.Id == batched.Id);
        featureB.Status.ShouldBe(featureA.Status);
        featureB.Version.ShouldBe(featureA.Version);
        PlanningLog.Parse(featureB.Body).Select(l => l.Text)
            .ShouldBe(PlanningLog.Parse(featureA.Body).Select(l => l.Text));
    }

    [Fact]
    public async Task Batch_FailureAtSecondOp_PersistsNothingAndNamesOps1()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "rollback", "R", "body");
        var artifacts = CreateArtifactService();
        var task = await artifacts.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, TaskBody(), null, null, null, default);
        var versionBefore = task.Version;

        var batchService = CreateBatchService();
        var ex = await Should.ThrowAsync<PlanningException>(() => batchService.ApplyAsync(_projectId, "rollback",
        [
            new PlanningBatchOp(
                PlanningBatchOpName.AppendLog,
                ArtifactId: task.Id,
                Note: "a note that must not survive"),
            new PlanningBatchOp(
                PlanningBatchOpName.SetStatus,
                ArtifactId: task.Id,
                Status: PlanningArtifactStatus.Done,
                LogNote: ""),
        ]));

        ex.Message.ShouldStartWith("ops[1]:");
        ex.Code.ShouldBe(PlanningFailureCode.ChangeNoteRequired);

        using var verify = _db.CreateContext();
        var saved = await verify.PlanningArtifacts.SingleAsync(a => a.Id == task.Id);
        saved.Version.ShouldBe(versionBefore);
        saved.Body.ShouldNotContain("a note that must not survive");

        var savedFeature = await verify.PlanningFeatures.SingleAsync(f => f.Id == feature.Id);
        PlanningLog.Parse(savedFeature.Body)
            .ShouldNotContain(l => l.Text.Contains("a note that must not survive"));
    }

    [Fact]
    public async Task Batch_SetStatusValidatedWithoutProof_RefusesTheWholeBatch()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "proof-gate", "G", "body");
        var artifacts = CreateArtifactService();
        var spec = await artifacts.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec body", null, null, null, default);

        var batchService = CreateBatchService();
        var ex = await Should.ThrowAsync<PlanningException>(() => batchService.ApplyAsync(_projectId, "proof-gate",
        [
            new PlanningBatchOp(
                PlanningBatchOpName.AppendLog,
                ArtifactId: spec.Id,
                Note: "pre-validate note"),
            new PlanningBatchOp(
                PlanningBatchOpName.SetStatus,
                ArtifactId: spec.Id,
                Status: PlanningArtifactStatus.Validated,
                LogNote: "self-approved"),
        ]));

        ex.Message.ShouldStartWith("ops[1]:");
        ex.Code.ShouldBe(PlanningFailureCode.ValidationProofRequired);

        using var verify = _db.CreateContext();
        var saved = await verify.PlanningArtifacts.SingleAsync(a => a.Id == spec.Id);
        saved.Status.ShouldBe(PlanningArtifactStatus.Draft);
        saved.Body.ShouldNotContain("pre-validate note");
    }

    [Fact]
    public async Task Batch_DispatchesMaterializationOncePerCommittedBatch()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "one-dispatch", "D", "body");
        var artifacts = CreateArtifactService();
        var task = await artifacts.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, TaskBody(), null, null, null, default);
        _materializer.ClearReceivedCalls();

        var batchService = CreateBatchService();
        await batchService.ApplyAsync(_projectId, "one-dispatch",
        [
            new PlanningBatchOp(
                PlanningBatchOpName.Patch,
                ArtifactId: task.Id,
                Patch: new PlanningArtifactPatch(TickIndex: 1, LogNote: "ticked")),
            new PlanningBatchOp(PlanningBatchOpName.AppendLog, ArtifactId: task.Id, Note: "note"),
            new PlanningBatchOp(
                PlanningBatchOpName.SetStatus,
                ArtifactId: task.Id,
                Status: PlanningArtifactStatus.Done,
                LogNote: "task closed"),
        ]);

        await _materializer.Received(1).SyncAsync(_projectId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Batch_NotifyOp_PersistsTheNotificationInsideTheBatchTransaction()
    {
        var featureService = CreateFeatureService();
        await featureService.CreateAsync(_projectId, "notify", "N", "body");

        var batchService = CreateBatchService();
        var result = await batchService.ApplyAsync(_projectId, "notify",
        [
            new PlanningBatchOp(
                PlanningBatchOpName.Notify,
                Kind: NotificationKind.Question,
                Text: "ship it?",
                Tab: "tasks"),
        ]);

        var notification = (AgentNotification)result.Results.Single().Payload;
        using var verify = _db.CreateContext();
        var saved = await verify.AgentNotifications.SingleAsync(n => n.Id == notification.Id);
        saved.ProjectId.ShouldBe(_projectId);
        saved.FeatureSlug.ShouldBe("notify");
        saved.Tab.ShouldBe("tasks");
        _delivery.Delivered.ShouldHaveSingleItem().ShouldBe(notification.Id);
    }

    [Fact]
    public async Task Batch_NotifyOpWithLaterFailure_RollsTheNotificationBack()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "notify-rollback", "N", "body");
        var artifacts = CreateArtifactService();
        var task = await artifacts.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, TaskBody(), null, null, null, default);

        var batchService = CreateBatchService();
        await Should.ThrowAsync<PlanningException>(() => batchService.ApplyAsync(_projectId, "notify-rollback",
        [
            new PlanningBatchOp(
                PlanningBatchOpName.Notify,
                Kind: NotificationKind.Approval,
                Text: "approve the close?"),
            new PlanningBatchOp(
                PlanningBatchOpName.SetStatus,
                ArtifactId: task.Id,
                Status: PlanningArtifactStatus.Done,
                LogNote: ""),
        ]));

        using var verify = _db.CreateContext();
        verify.AgentNotifications.Any(n => n.FeatureSlug == "notify-rollback").ShouldBeFalse();
        _delivery.Delivered.ShouldBeEmpty();
    }

    [Fact]
    public async Task Batch_TwoUpsertTasksOps_StageDistinctAutoOrders()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "auto-orders", "U", "body");

        var batchService = CreateBatchService();
        await batchService.ApplyAsync(_projectId, "auto-orders",
        [
            new PlanningBatchOp(
                PlanningBatchOpName.UpsertTasks,
                Tasks: [new PlanningTaskUpsert("# First\n\nfirst task body\n")]),
            new PlanningBatchOp(
                PlanningBatchOpName.UpsertTasks,
                Tasks: [new PlanningTaskUpsert("# Second\n\nsecond task body\n")]),
        ]);

        using var verify = _db.CreateContext();
        var saved = await verify.PlanningArtifacts
            .Where(a => a.FeatureId == feature.Id && a.Kind == PlanningArtifactKind.Task)
            .OrderBy(a => a.Order)
            .ToListAsync();
        saved.Count.ShouldBe(2);
        saved[0].Order.ShouldBe(0);
        saved[1].Order.ShouldBe(1);
        saved[0].Body.ShouldContain("first task body");
        saved[1].Body.ShouldContain("second task body");
    }

    [Fact]
    public async Task Batch_OpOnAnotherFeatureArtifact_RefusesAndPersistsNothing()
    {
        var featureService = CreateFeatureService();
        var owner = await featureService.CreateAsync(_projectId, "cross-owner", "O", "body");
        var other = await featureService.CreateAsync(_projectId, "cross-other", "X", "body");
        var artifacts = CreateArtifactService();
        var task = await artifacts.UpsertAsync(
            owner.Id, PlanningArtifactKind.Task, TaskBody(), null, null, null, default);

        var batchService = CreateBatchService();
        var ex = await Should.ThrowAsync<PlanningException>(() => batchService.ApplyAsync(_projectId, "cross-other",
        [
            new PlanningBatchOp(
                PlanningBatchOpName.SetStatus,
                ArtifactId: task.Id,
                Status: PlanningArtifactStatus.Done,
                LogNote: "borrowed artifact"),
        ]));

        ex.Code.ShouldBe(PlanningFailureCode.BatchArtifactOutsideFeature);
        ex.Message.ShouldStartWith("ops[0]:");
        ex.Message.ShouldContain("cross-other");

        using var verify = _db.CreateContext();
        var saved = await verify.PlanningArtifacts.SingleAsync(a => a.Id == task.Id);
        saved.Status.ShouldBe(PlanningArtifactStatus.Todo);
    }

    [Fact]
    public async Task Batch_EmptyOps_ThrowsBatchEmpty()
    {
        var batchService = CreateBatchService();
        var ex = await Should.ThrowAsync<PlanningException>(() =>
            batchService.ApplyAsync(_projectId, "whatever", []));

        ex.Code.ShouldBe(PlanningFailureCode.BatchEmpty);
    }

    [Fact]
    public async Task Batch_ConcurrentWriterClaimsTaskOrder_RollsBackWholeBatchWithRetryHint()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "race", "R", "body");

        var armed = false;
        var interceptor = new ConcurrentClaimInterceptor(() => armed);
        using var context = _db.CreateContext(interceptor);
        var batchService = new PlanningBatchService(
            context,
            new PlanningArtifactService(
                context,
                TestPlanningDispatcher.Create(context, _materializer),
                new TaskDependencyService(context, TestPlanningDispatcher.Create(context, _materializer)),
                _batchScope),
            new NotificationsService(
                context,
                new ReplyAutoActFanout(
                    context, new ReplyAutoActQueue(), NullLogger<ReplyAutoActFanout>.Instance),
                new WakeDropFanout(
                    new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                    NullLogger<WakeDropFanout>.Instance),
                [_delivery],
                _batchScope),
            TestPlanningDispatcher.Create(context, _materializer),
            _batchScope);

        armed = true;
        var ex = await Should.ThrowAsync<PlanningException>(() => batchService.ApplyAsync(_projectId, "race",
        [
            new PlanningBatchOp(
                PlanningBatchOpName.Notify,
                Kind: NotificationKind.Approval,
                Text: "approve the close?"),
            new PlanningBatchOp(
                PlanningBatchOpName.UpsertTasks,
                Tasks: [new PlanningTaskUpsert("# First\n\nfirst task body\n")]),
        ]));
        armed = false;

        ex.Kind.ShouldBe(PlanningFailureKind.Duplicate);
        ex.Message.ShouldContain("retry the call");
        _batchScope.Active.ShouldBeFalse();
        _batchScope.StagedNotifications.ShouldBeEmpty();
        _delivery.Delivered.ShouldBeEmpty();

        using var verify = _db.CreateContext();
        var persisted = await verify.PlanningArtifacts
            .Where(a => a.FeatureId == feature.Id && a.Kind == PlanningArtifactKind.Task)
            .ToListAsync();
        persisted.ShouldBeEmpty();
        verify.AgentNotifications.Any(n => n.FeatureSlug == "race").ShouldBeFalse();
    }

    private static string TaskBody() =>
        "# T0 — batch parity\n\n<acceptance-criteria>\n- [ ] first item done\n- [ ] second item done\n</acceptance-criteria>\n";

    private sealed class ConcurrentClaimInterceptor(Func<bool> armed) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (armed())
            {
                throw SimulatedConcurrentClaim();
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static DbUpdateException SimulatedConcurrentClaim() => new(
            "An error occurred while saving the entity changes. See the inner exception for details.",
            new SqliteException(
                "SQLite Error 19: 'UNIQUE constraint failed: PlanningArtifacts.FeatureId, "
                + "PlanningArtifacts.Kind, PlanningArtifacts.Order'", 19));
    }

    private sealed class RecordingDeliveryChannel : INotificationDeliveryChannel
    {
        public List<Guid> Delivered { get; } = [];

        public Task TryDeliverAsync(AgentNotification notification, CancellationToken ct)
        {
            Delivered.Add(notification.Id);
            return Task.CompletedTask;
        }
    }
}
