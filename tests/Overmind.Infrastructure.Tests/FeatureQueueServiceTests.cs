using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class FeatureQueueServiceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;

    public FeatureQueueServiceTests()
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

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    private FeatureQueueService CreateQueueService() => new(_context);

    private PlanningFeatureService CreateFeatureService() =>
        new(
            _context,
            _materializer,
            new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
            new TaskDependencyService(_context, _materializer, NullLogger<TaskDependencyService>.Instance),
            NullLogger<PlanningFeatureService>.Instance);

    private async Task CreateFeatureAsync(
        string slug,
        FeaturePriority priority = FeaturePriority.P2,
        PlanningFeatureStatus? status = null)
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, slug, slug, string.Empty, priority);
        if (status.HasValue && status.Value != PlanningFeatureStatus.Draft)
        {
            foreach (var step in PathTo(status.Value))
            {
                await service.SetStatusAsync(_projectId, slug, step, "test move");
            }
        }
    }

    private static IEnumerable<PlanningFeatureStatus> PathTo(PlanningFeatureStatus target)
    {
        return target switch
        {
            PlanningFeatureStatus.Ready => [PlanningFeatureStatus.Ready],
            PlanningFeatureStatus.InProgress => [PlanningFeatureStatus.InProgress],
            PlanningFeatureStatus.Review => [PlanningFeatureStatus.InProgress, PlanningFeatureStatus.Review],
            PlanningFeatureStatus.Done => [PlanningFeatureStatus.Done],
            _ => []
        };
    }

    private async Task<List<(string Slug, int Position)>> SnapshotAsync()
    {
        await using var context = _db.CreateContext();
        var rows = await context.PlanningFeatures
            .AsNoTracking()
            .Where(f => f.ProjectId == _projectId)
            .OrderBy(f => f.QueuePosition)
            .ThenBy(f => f.Slug)
            .Select(f => new { f.Slug, f.QueuePosition })
            .ToListAsync();
        return rows.Select(f => (f.Slug, f.QueuePosition)).ToList();
    }

    [Fact]
    public async Task MoveUp_SwapsWithAdjacentNonDoneFeature()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma");

        await CreateQueueService().MoveUpAsync(_projectId, "beta");

        (await SnapshotAsync()).ShouldBe([("beta", 1), ("alpha", 2), ("gamma", 3)]);
    }

    [Fact]
    public async Task MoveDown_SwapsWithAdjacentNonDoneFeature()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma");

        await CreateQueueService().MoveDownAsync(_projectId, "beta");

        (await SnapshotAsync()).ShouldBe([("alpha", 1), ("gamma", 2), ("beta", 3)]);
    }

    [Fact]
    public async Task MoveUp_AtBandTop_IsNoOpSuccess()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");

        await CreateQueueService().MoveUpAsync(_projectId, "alpha");

        (await SnapshotAsync()).ShouldBe([("alpha", 1), ("beta", 2)]);
    }

    [Fact]
    public async Task MoveDown_AtBandBottom_IsNoOpSuccess()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");

        await CreateQueueService().MoveDownAsync(_projectId, "beta");

        (await SnapshotAsync()).ShouldBe([("alpha", 1), ("beta", 2)]);
    }

    [Fact]
    public async Task MoveFirst_LandsAtHeadAndShiftsTheRest()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma");

        await CreateQueueService().MoveFirstAsync(_projectId, "gamma");

        (await SnapshotAsync()).ShouldBe([("gamma", 1), ("alpha", 2), ("beta", 3)]);
    }

    [Fact]
    public async Task MoveLast_LandsAtTail_AndStaysPutWhenAlreadyLast()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma");

        var service = CreateQueueService();
        await service.MoveLastAsync(_projectId, "alpha");
        (await SnapshotAsync()).ShouldBe([("beta", 1), ("gamma", 2), ("alpha", 3)]);

        await service.MoveLastAsync(_projectId, "alpha");
        (await SnapshotAsync()).ShouldBe([("beta", 1), ("gamma", 2), ("alpha", 3)]);
    }

    [Fact]
    public async Task MoveToPosition_LandsAtRequestedSpot()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma");
        await CreateFeatureAsync("delta");

        await CreateQueueService().MoveToPositionAsync(_projectId, "alpha", 3);

        (await SnapshotAsync()).ShouldBe([("beta", 1), ("gamma", 2), ("alpha", 3), ("delta", 4)]);
    }

    [Fact]
    public async Task MoveToPosition_OutsideBand_RejectsWithValidRange()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma");
        var service = CreateQueueService();

        var below = await Should.ThrowAsync<PlanningException>(
            () => service.MoveToPositionAsync(_projectId, "alpha", 0));
        var above = await Should.ThrowAsync<PlanningException>(
            () => service.MoveToPositionAsync(_projectId, "alpha", 4));

        below.Code.ShouldBe(PlanningFailureCode.InvalidQueuePosition);
        below.Message.ShouldContain("valid range is 1..3");
        above.Code.ShouldBe(PlanningFailureCode.InvalidQueuePosition);
        above.Message.ShouldContain("valid range is 1..3");
    }

    [Fact]
    public async Task ReorderBand_RenumbersTheFullBand()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma");

        await CreateQueueService().ReorderBandAsync(_projectId, FeaturePriority.P2, ["gamma", "alpha", "beta"]);

        (await SnapshotAsync()).ShouldBe([("gamma", 1), ("alpha", 2), ("beta", 3)]);
    }

    [Fact]
    public async Task ReorderBand_PartialList_RejectedNamingMissingSlugs()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => CreateQueueService().ReorderBandAsync(_projectId, FeaturePriority.P2, ["alpha"]));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidQueueReorder);
        ex.Message.ShouldContain("the list is missing: beta, gamma");
    }

    [Fact]
    public async Task ReorderBand_ForeignSlug_Rejected()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => CreateQueueService().ReorderBandAsync(_projectId, FeaturePriority.P2, ["gamma", "alpha", "beta", "ghost"]));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidQueueReorder);
        ex.Message.ShouldContain("not on this project's board: ghost");
    }

    [Fact]
    public async Task ReorderBand_CrossPrioritySlug_RejectedNamingItsBand()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma", FeaturePriority.P1);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => CreateQueueService().ReorderBandAsync(_projectId, FeaturePriority.P2, ["alpha", "beta", "gamma"]));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidQueueReorder);
        ex.Message.ShouldContain("'gamma' carries priority 'p1'");
    }

    [Fact]
    public async Task ReorderBand_DuplicateSlugs_Rejected()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => CreateQueueService().ReorderBandAsync(_projectId, FeaturePriority.P2, ["alpha", "alpha", "beta"]));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidQueueReorder);
        ex.Message.ShouldContain("duplicate slugs: alpha");
    }

    [Fact]
    public async Task ReorderBand_DoneFeatureInList_Rejected()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta", status: PlanningFeatureStatus.Done);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => CreateQueueService().ReorderBandAsync(_projectId, FeaturePriority.P2, ["alpha", "beta"]));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidQueueReorder);
        ex.Message.ShouldContain("done features cannot re-enter the queue: beta");
    }

    [Fact]
    public async Task ReorderBand_SlugSharedWithAnotherProject_ResolvesThisProjectsFeature()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");

        var other = Project.Create($"other-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(other);
        _context.SaveChanges();
        await CreateFeatureService().CreateAsync(other.Id, "beta", "beta", string.Empty);

        await CreateQueueService().ReorderBandAsync(_projectId, FeaturePriority.P2, ["beta", "alpha"]);

        (await SnapshotAsync()).ShouldBe([("beta", 1), ("alpha", 2)]);

        await using var context = _db.CreateContext();
        var otherBeta = await context.PlanningFeatures
            .AsNoTracking()
            .SingleAsync(f => f.ProjectId == other.Id && f.Slug == "beta");
        otherBeta.QueuePosition.ShouldBe(1);
    }

    [Fact]
    public async Task MoveUpAsync_SlugFromAnotherProjectOnly_IsMissingFeature()
    {
        await CreateFeatureAsync("alpha");

        var other = Project.Create($"other-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(other);
        _context.SaveChanges();
        await CreateFeatureService().CreateAsync(other.Id, "ghost", "ghost", string.Empty);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => CreateQueueService().MoveUpAsync(_projectId, "ghost"));

        ex.Code.ShouldBe(PlanningFailureCode.MissingFeature);
    }

    [Fact]
    public async Task MoveUpAsync_SkipDoneNeighbors_AndLeaveTheirStoredPositionsUntouched()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma");
        await CreateFeatureService().SetStatusAsync(_projectId, "alpha", PlanningFeatureStatus.Done, "shipped");

        await CreateQueueService().MoveUpAsync(_projectId, "gamma");

        (await SnapshotAsync()).ShouldBe([("alpha", 1), ("gamma", 1), ("beta", 2)]);
    }

    [Fact]
    public async Task MoveUpAsync_DoneFeature_RejectedAsNotInQueue()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureService().SetStatusAsync(_projectId, "beta", PlanningFeatureStatus.Done, "shipped");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => CreateQueueService().MoveUpAsync(_projectId, "beta"));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureNotInQueue);
    }

    [Fact]
    public async Task MoveUpAsync_AreScopedToThePriorityBand()
    {
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma", FeaturePriority.P1);

        await CreateQueueService().MoveUpAsync(_projectId, "gamma");

        (await SnapshotAsync()).ShouldBe([("alpha", 1), ("gamma", 1), ("beta", 2)]);
    }

    [Fact]
    public async Task Moves_AtDuplicatePositions_SwapWithTheSlugOrderedNeighbor()
    {
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("alpha");
        foreach (var slug in new[] { "beta", "alpha" })
        {
            _context.PlanningFeatures
                .Single(f => f.ProjectId == _projectId && f.Slug == slug)
                .SetQueuePosition(1);
        }

        await _context.SaveChangesAsync();

        var service = CreateQueueService();
        await service.MoveDownAsync(_projectId, "alpha");
        (await SnapshotAsync()).ShouldBe([("beta", 1), ("alpha", 2)]);

        await service.MoveUpAsync(_projectId, "alpha");
        (await SnapshotAsync()).ShouldBe([("alpha", 1), ("beta", 2)]);
    }
}
