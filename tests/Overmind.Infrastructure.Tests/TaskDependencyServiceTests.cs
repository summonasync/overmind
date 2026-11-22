using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class TaskDependencyServiceTests : IDisposable, IAsyncLifetime
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;
    private readonly Guid _featureId;

    public TaskDependencyServiceTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));
        _materializer.ClearAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));

        var project = Project.Create($"test-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        var feature = PlanningFeature.Create(project.Id, $"feature-{Guid.NewGuid():N}", "Feature", string.Empty);
        _context.Projects.Add(project);
        _context.PlanningFeatures.Add(feature);
        _context.SaveChanges();
        _projectId = project.Id;
        _featureId = feature.Id;
    }


    public async Task InitializeAsync() =>
        await TestPlanningDispatcher.EnableSyncModeAsync(_context);

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    private TaskDependencyService CreateService() =>
        new(_context, TestPlanningDispatcher.Create(_context, _materializer));

    private PlanningArtifact SeedTask(int order)
    {
        var task = PlanningArtifact.Create(
            _featureId, PlanningArtifactKind.Task, $"task body {order}", order);
        _context.PlanningArtifacts.Add(task);
        _context.SaveChanges();
        return task;
    }

    private PlanningArtifact Tracked(Guid id) =>
        _context.PlanningArtifacts.Single(a => a.Id == id);

    private IReadOnlyList<int> BlockerOrders(PlanningArtifact task) =>
        task.BlockedBy
            .Select(d => Tracked(d.BlockingId).Order)
            .OrderBy(o => o)
            .ToList();

    [Fact]
    public async Task ReplaceEdgesAsync_ResolvesOrdersToCommittedEdges()
    {
        var blocked = SeedTask(0);
        var blocker = SeedTask(1);

        var view = await CreateService().ReplaceEdgesAsync(blocked.Id, [1]);

        view.Single().Order.ShouldBe(1);
        BlockerOrders(Tracked(blocked.Id)).ShouldBe([1]);
        await _materializer.Received(1).SyncAsync(
            _projectId, Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplaceEdgesAsync_Reupsert_ReplacesEdgeSet()
    {
        var blocked = SeedTask(0);
        SeedTask(1);
        SeedTask(2);
        var service = CreateService();

        await service.ReplaceEdgesAsync(blocked.Id, [1]);
        await service.ReplaceEdgesAsync(blocked.Id, [2]);

        BlockerOrders(Tracked(blocked.Id)).ShouldBe([2]);
    }

    [Fact]
    public async Task ReplaceEdgesAsync_EmptyArray_ClearsEdgeSet()
    {
        var blocked = SeedTask(0);
        SeedTask(1);
        var service = CreateService();

        await service.ReplaceEdgesAsync(blocked.Id, [1]);
        await service.ReplaceEdgesAsync(blocked.Id, []);

        Tracked(blocked.Id).BlockedBy.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceEdgesAsync_UnchangedSet_DoesNotSyncMaterialization()
    {
        var blocked = SeedTask(0);
        SeedTask(1);
        var service = CreateService();

        await service.ReplaceEdgesAsync(blocked.Id, [1]);
        _materializer.ClearReceivedCalls();
        await service.ReplaceEdgesAsync(blocked.Id, [1]);

        await _materializer.DidNotReceiveWithAnyArgs().SyncAsync(
            default, default, default);
    }

    [Fact]
    public async Task ReplaceEdgesAsync_DirectCycle_ThrowsWithOrderPath()
    {
        var first = SeedTask(0);
        var second = SeedTask(1);
        var service = CreateService();
        await service.ReplaceEdgesAsync(first.Id, [1]);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.ReplaceEdgesAsync(second.Id, [0]));

        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Code.ShouldBe(PlanningFailureCode.TaskDependencyCycle);
        ex.Message.ShouldContain("1 -> 0 -> 1");
        BlockerOrders(Tracked(second.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceEdgesAsync_TransitiveCycle_ChainNamesFullPath()
    {
        var first = SeedTask(0);
        var second = SeedTask(1);
        var third = SeedTask(2);
        var service = CreateService();
        await service.ReplaceEdgesAsync(third.Id, [0]);
        await service.ReplaceEdgesAsync(second.Id, [2]);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.ReplaceEdgesAsync(first.Id, [1]));

        ex.Code.ShouldBe(PlanningFailureCode.TaskDependencyCycle);
        ex.Message.ShouldContain("0 -> 1 -> 2 -> 0");
        BlockerOrders(Tracked(first.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceEdgesAsync_UnknownOrder_ThrowsNamingTheOrder()
    {
        var blocked = SeedTask(0);
        SeedTask(1);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => CreateService().ReplaceEdgesAsync(blocked.Id, [7]));

        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Code.ShouldBe(PlanningFailureCode.BlockedByOrderUnknown);
        ex.Message.ShouldContain("7");
        ex.Message.ShouldContain("0, 1");
    }

    [Fact]
    public async Task ReplaceEdgesAsync_SelfReference_Throws()
    {
        var blocked = SeedTask(0);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => CreateService().ReplaceEdgesAsync(blocked.Id, [0]));

        ex.Code.ShouldBe(PlanningFailureCode.BlockedBySelfReference);
    }

    [Fact]
    public async Task ReplaceEdgesAsync_DuplicateOrder_Throws()
    {
        var blocked = SeedTask(0);
        SeedTask(1);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => CreateService().ReplaceEdgesAsync(blocked.Id, [1, 1]));

        ex.Code.ShouldBe(PlanningFailureCode.BlockedByDuplicateOrder);
    }

    [Fact]
    public async Task ReplaceEdgesAsync_MissingTask_Throws()
    {
        var ex = await Should.ThrowAsync<PlanningException>(
            () => CreateService().ReplaceEdgesAsync(Guid.NewGuid(), []));

        ex.Code.ShouldBe(PlanningFailureCode.MissingArtifact);
    }

    [Fact]
    public async Task UnclosedBlockersAsync_ClosureFollowsBlockerStatus()
    {
        var blocked = SeedTask(0);
        var blocker = SeedTask(1);
        var service = CreateService();
        await service.ReplaceEdgesAsync(blocked.Id, [1]);

        (await service.UnclosedBlockersAsync(blocked.Id)).Select(e => e.Order).ShouldBe([1]);

        Tracked(blocker.Id).SetStatus(PlanningArtifactStatus.Review);
        await _context.SaveChangesAsync();
        (await service.UnclosedBlockersAsync(blocked.Id)).ShouldBeEmpty();

        Tracked(blocker.Id).SetStatus(PlanningArtifactStatus.Done);
        await _context.SaveChangesAsync();
        (await service.UnclosedBlockersAsync(blocked.Id)).ShouldBeEmpty();

        Tracked(blocker.Id).SetStatus(PlanningArtifactStatus.InProgress, "rolled back");
        await _context.SaveChangesAsync();
        (await service.UnclosedBlockersAsync(blocked.Id)).Select(e => e.Order).ShouldBe([1]);
    }

    [Fact]
    public async Task BlockedOrdersAsync_ReturnsFlagAndSortedBlockerOrders()
    {
        var first = SeedTask(0);
        var second = SeedTask(1);
        var third = SeedTask(2);
        var service = CreateService();
        await service.ReplaceEdgesAsync(third.Id, [1, 0]);

        Tracked(second.Id).SetStatus(PlanningArtifactStatus.Review);
        await _context.SaveChangesAsync();

        var states = await service.BlockedOrdersAsync(_featureId);

        states.Count.ShouldBe(3);
        states[0].Order.ShouldBe(0);
        states[0].Blocked.ShouldBeFalse();
        states[1].Order.ShouldBe(1);
        states[1].Blocked.ShouldBeFalse();
        states[2].Order.ShouldBe(2);
        states[2].Blocked.ShouldBeTrue();
        states[2].BlockedBy.ShouldBe([0, 1]);
    }

    [Fact]
    public async Task BlockedOrdersAsync_MissingFeature_Throws()
    {
        var ex = await Should.ThrowAsync<PlanningException>(
            () => CreateService().BlockedOrdersAsync(Guid.NewGuid()));

        ex.Code.ShouldBe(PlanningFailureCode.MissingFeature);
    }

    [Fact]
    public async Task DeleteArtifact_RefusedWhileDependentsHoldEdges()
    {
        var middle = SeedTask(0);
        var waiter = SeedTask(1);
        var blocker = SeedTask(2);
        var service = CreateService();
        var artifactService = new PlanningArtifactService(
            _context, TestPlanningDispatcher.Create(_context, _materializer), service);
        await service.ReplaceEdgesAsync(waiter.Id, [0]);
        await service.ReplaceEdgesAsync(middle.Id, [2]);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.DeleteAsync(middle.Id, byHuman: true));

        ex.Code.ShouldBe(PlanningFailureCode.DeletionBlockedByDependents);
        ex.Message.ShouldContain("Clean up dependencies first");
        ex.Message.ShouldContain("task 01");
        _context.PlanningTaskDependencies.Count().ShouldBe(2);
    }

    [Fact]
    public async Task DeleteArtifact_DependentLeafDropsItsOwnEdgesFirst()
    {
        var middle = SeedTask(0);
        var waiter = SeedTask(1);
        var blocker = SeedTask(2);
        var service = CreateService();
        var artifactService = new PlanningArtifactService(
            _context, TestPlanningDispatcher.Create(_context, _materializer), service);
        await service.ReplaceEdgesAsync(waiter.Id, [0]);
        await service.ReplaceEdgesAsync(middle.Id, [2]);

        await artifactService.DeleteAsync(waiter.Id, byHuman: true);
        await artifactService.DeleteAsync(middle.Id, byHuman: true);

        _context.PlanningTaskDependencies.ShouldBeEmpty();
    }
}
