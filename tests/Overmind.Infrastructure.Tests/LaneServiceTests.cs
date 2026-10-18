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

public sealed class LaneServiceTests : IDisposable
{
    private static readonly Guid SessionId = Guid.NewGuid();

    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;

    public LaneServiceTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));

        var project = Project.Create($"lanes-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    private LaneService CreateService() =>
        new(_context, new FeatureClaimService(_context, TestPlanningDispatcher.Create(_context, _materializer)));

    private FeatureClaimService CreateClaimService() =>
        new(_context, TestPlanningDispatcher.Create(_context, _materializer));

    private PlanningFeatureService CreateFeatureService() =>
        new(
            _context,
            _materializer,
            new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
            new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)),
            TestPlanningDispatcher.Create(_context, _materializer),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PlanningFeatureService>.Instance);

    [Fact]
    public async Task SetLanes_OnEmptyRoster_InsertsAll()
    {
        var roster = await CreateService().SetLanesAsync(_projectId,
            [new LaneUpsert("frontend", "web/src", 1), new LaneUpsert("backend", null, null)]);

        roster.Count.ShouldBe(2);
        roster[0].Name.ShouldBe("frontend");
        roster[0].Contour.ShouldBe("web/src");
        roster[0].SerialOrder.ShouldBe(1);
        roster[1].Name.ShouldBe("backend");
        roster[1].Contour.ShouldBeNull();
        roster[1].SerialOrder.ShouldBeNull();
    }

    [Fact]
    public async Task SetLanes_ReplacesRemoved_UpdatesKnown_InsertsNew()
    {
        var service = CreateService();
        await service.SetLanesAsync(_projectId,
            [new LaneUpsert("frontend", "old contour", 2), new LaneUpsert("obsolete", "gone", null)]);
        await CreateFeatureAsync("010-old", "obsolete");

        var roster = await service.SetLanesAsync(_projectId,
        [
            new LaneUpsert("frontend", "web/src", null),
            new LaneUpsert("backend", "api/src", null)
        ]);

        roster.Select(l => l.Name).ShouldBe(["backend", "frontend"]);
        roster.Single(l => l.Name == "frontend").Contour.ShouldBe("web/src");
        roster.Single(l => l.Name == "frontend").SerialOrder.ShouldBeNull();
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(2);
        (await _context.PlanningFeatures.SingleAsync(f => f.Slug == "010-old")).Lane.ShouldBeNull();
    }

    [Fact]
    public async Task SetLanes_CaseOnlyRename_UpdatesInPlaceWithoutViolation()
    {
        var service = CreateService();
        await service.SetLanesAsync(_projectId, [new LaneUpsert("frontend", null, null)]);

        var roster = await service.SetLanesAsync(_projectId, [new LaneUpsert("Frontend", null, null)]);

        roster.Single().Name.ShouldBe("Frontend");
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(1);
    }

    [Fact]
    public async Task SetLanes_EmptyArray_ClearsTheRoster()
    {
        var service = CreateService();
        await service.SetLanesAsync(_projectId, [new LaneUpsert("frontend", null, null)]);

        var roster = await service.SetLanesAsync(_projectId, []);

        roster.ShouldBeEmpty();
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(0);
    }

    [Fact]
    public async Task SetLanes_DuplicateNamesInRequest_Throws()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() => CreateService()
            .SetLanesAsync(_projectId, [new LaneUpsert("frontend", null, null), new LaneUpsert("Frontend", null, null)]));

        ex.Code.ShouldBe(PlanningFailureCode.LaneNameDuplicate);
        ex.Kind.ShouldBe(PlanningFailureKind.Duplicate);
    }

    [Fact]
    public async Task SetLanes_OverTheCap_Throws()
    {
        var roster = Enumerable.Range(0, LaneService.MaxLanesPerProject + 1)
            .Select(i => new LaneUpsert($"lane-{i}", null, null))
            .ToList();

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            CreateService().SetLanesAsync(_projectId, roster));

        ex.Code.ShouldBe(PlanningFailureCode.LaneRosterTooLarge);
    }

    [Fact]
    public async Task SetLanes_BlankName_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(() => CreateService()
            .SetLanesAsync(_projectId, [new LaneUpsert("   ", null, null)]));
    }

    [Fact]
    public async Task SetLanes_UnknownProject_ThrowsNotFound()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() =>
            CreateService().SetLanesAsync(Guid.NewGuid(), [new LaneUpsert("frontend", null, null)]));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
    }

    [Fact]
    public async Task ListLanes_ProjectionsCarryCountsHolderAndFeatures()
    {
        var service = CreateService();
        await service.SetLanesAsync(_projectId, [new LaneUpsert("frontend", "web/src", null)]);
        await CreateFeatureAsync("011-lane-a", "frontend");
        await CreateFeatureAsync("012-lane-b", "frontend");
        await CreateFeatureAsync("013-lane-c", null);

        await CreateClaimService().ClaimAsync(
            _projectId, "011-lane-a", "Van", "claude", SessionId, overrideLaneCheck: true);

        var roster = await service.ListLanesAsync(_projectId);

        var lane = roster.ShouldHaveSingleItem();
        lane.Name.ShouldBe("frontend");
        lane.OpenFeatureCount.ShouldBe(2);
        lane.Holder.ShouldNotBeNull();
        lane.Holder.Alias.ShouldBe("Van");
        lane.Holder.Vendor.ShouldBe("claude");
        lane.Holder.ClaimState.ShouldBe(AgentSessionLiveness.Alive);
        lane.Features.Select(f => f.Slug).ShouldBe(["011-lane-a", "012-lane-b"]);
        lane.Features.First().ClaimedByAlias.ShouldBe("Van");
        lane.Features.First().ClaimState.ShouldBe(AgentSessionLiveness.Alive);
        lane.Features.Last().ClaimedByAlias.ShouldBeNull();
    }

    [Fact]
    public async Task ListLanes_StaleClaimIsNotAHolder()
    {
        var service = CreateService();
        await service.SetLanesAsync(_projectId, [new LaneUpsert("frontend", null, null)]);
        await CreateFeatureAsync("011-lane-a", "frontend");
        await CreateClaimService().ClaimAsync(_projectId, "011-lane-a", "Van", "claude", SessionId);

        var session = await _context.AgentSessions.SingleAsync(s => s.Id == SessionId);
        _context.AgentSessions.Remove(session);
        _context.AgentSessions.Add(AgentSession.Restore(
            SessionId, "Van", "claude", DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddMinutes(-90)));
        await _context.SaveChangesAsync();

        var roster = await service.ListLanesAsync(_projectId);

        roster.Single().Holder.ShouldBeNull();
    }

    [Fact]
    public async Task ListLanes_LanesOrder_SerialFirstThenName()
    {
        var service = CreateService();
        await service.SetLanesAsync(_projectId,
        [
            new LaneUpsert("zeta", null, 2),
            new LaneUpsert("alpha", null, 1),
            new LaneUpsert("beta", null, null)
        ]);

        var roster = await service.ListLanesAsync(_projectId);

        roster.Select(l => l.Name).ShouldBe(["alpha", "zeta", "beta"]);
    }

    [Fact]
    public async Task SetLanes_PriorityPersistsAndRoundTripsThroughTheRoster()
    {
        var service = CreateService();

        var roster = await service.SetLanesAsync(_projectId,
            [new LaneUpsert("frontend", "web/src", 1, 0), new LaneUpsert("backend", null, null, 2)]);

        roster.Single(l => l.Name == "frontend").Priority.ShouldBe(0);
        roster.Single(l => l.Name == "backend").Priority.ShouldBe(2);
        (await _context.PlanningLanes.SingleAsync(l => l.Name == "frontend")).Priority.ShouldBe(0);
    }

    [Fact]
    public async Task SetLanes_OmittedPriority_ClearsTheStoredOne()
    {
        var service = CreateService();
        await service.SetLanesAsync(_projectId, [new LaneUpsert("frontend", null, null, 1)]);

        var roster = await service.SetLanesAsync(_projectId, [new LaneUpsert("frontend", null, null)]);

        roster.Single().Priority.ShouldBeNull();
    }

    [Fact]
    public async Task ListLanes_PriorityFirst_NullsLast_SerialThenNameWithinEqualPriority()
    {
        var service = CreateService();
        await service.SetLanesAsync(_projectId,
        [
            new LaneUpsert("zeta", null, null, null),
            new LaneUpsert("alpha", null, 2, 2),
            new LaneUpsert("beta", null, null, 0),
            new LaneUpsert("gamma", null, 1, 2)
        ]);

        var roster = await service.ListLanesAsync(_projectId);

        roster.Select(l => l.Name).ShouldBe(["beta", "gamma", "alpha", "zeta"]);
    }

    [Fact]
    public async Task SetLanes_PriorityOutOfRange_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(() => CreateService()
            .SetLanesAsync(_projectId, [new LaneUpsert("frontend", null, null, 4)]));
        await Should.ThrowAsync<ArgumentException>(() => CreateService()
            .SetLanesAsync(_projectId, [new LaneUpsert("frontend", null, null, -1)]));
    }

    [Fact]
    public async Task ListLanes_UnknownProject_ThrowsNotFound()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() =>
            CreateService().ListLanesAsync(Guid.NewGuid()));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
    }

    [Fact]
    public async Task SetLanes_RemovingOneLane_UnassignsOnlyItsFeatures()
    {
        var service = CreateService();
        await service.SetLanesAsync(_projectId,
        [
            new LaneUpsert("gone", "gone contour", null),
            new LaneUpsert("kept", "kept contour", 3)
        ]);
        await CreateFeatureAsync("011-gone-a", "gone");
        await CreateFeatureAsync("012-gone-b", "gone");
        await CreateFeatureAsync("013-kept-a", "kept");

        var roster = await service.SetLanesAsync(_projectId, [new LaneUpsert("kept", "kept contour", 3)]);

        var kept = roster.ShouldHaveSingleItem();
        kept.Name.ShouldBe("kept");
        kept.Contour.ShouldBe("kept contour");
        kept.SerialOrder.ShouldBe(3);
        kept.Features.Select(f => f.Slug).ShouldBe(["013-kept-a"]);

        var orphans = await _context.PlanningFeatures
            .Where(f => f.Slug == "011-gone-a" || f.Slug == "012-gone-b")
            .ToListAsync();
        orphans.Select(f => f.Slug).ShouldBe(["011-gone-a", "012-gone-b"]);
        orphans.ShouldAllBe(f => f.Lane == null);
    }

    [Fact]
    public async Task RosterNames_ReturnsSortedNames()
    {
        var service = CreateService();
        await service.SetLanesAsync(_projectId, [new LaneUpsert("zeta", null, null), new LaneUpsert("alpha", null, null)]);

        var names = await service.RosterNamesAsync(_projectId);

        names.ShouldBe(["alpha", "zeta"]);
    }

    private async Task CreateFeatureAsync(string slug, string? lane)
    {
        await CreateFeatureService().CreateAsync(_projectId, slug, "Title", "body");
        if (lane is null)
        {
            return;
        }

        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == slug);
        feature.SetLane(lane);
        await _context.SaveChangesAsync();
    }
}
