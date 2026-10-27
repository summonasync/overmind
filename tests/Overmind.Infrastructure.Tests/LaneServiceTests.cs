using Microsoft.EntityFrameworkCore;
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

    private async Task SeedLaneAsync(
        string name, string? contour = null, int? serialOrder = null, int? priority = null)
    {
        _context.PlanningLanes.Add(PlanningLane.Create(_projectId, name, contour, serialOrder, priority));
        await _context.SaveChangesAsync();
    }

    private async Task<PlanningFeature> SeedDoneReadyFeatureAsync(string slug, string lane)
    {
        await CreateFeatureAsync(slug, lane);
        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == slug);
        _context.PlanningArtifacts.Add(PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Result, "close-out"));
        await _context.SaveChangesAsync();
        return feature;
    }

    [Fact]
    public async Task SetLanes_PayloadMissingALane_LeavesItIntact()
    {
        var service = CreateService();
        await SeedLaneAsync("frontend", "web/src", 1, 0);
        await SeedLaneAsync("backend", "api/src", 2, 1);
        await CreateFeatureAsync("011-lane-a", "frontend");

        var roster = await service.SetLanesAsync(_projectId, [new LaneUpsert("backend", "api/src", 2, 1)]);

        roster.Select(l => l.Name).ShouldBe(["frontend", "backend"]);
        var frontend = roster.Single(l => l.Name == "frontend");
        frontend.Contour.ShouldBe("web/src");
        frontend.SerialOrder.ShouldBe(1);
        frontend.Priority.ShouldBe(0);
        frontend.Features.Select(f => f.Slug).ShouldBe(["011-lane-a"]);
    }

    [Fact]
    public async Task SetLanes_EmptyArray_IsANoOp()
    {
        var service = CreateService();
        await SeedLaneAsync("frontend", "web/src", 1);

        var roster = await service.SetLanesAsync(_projectId, []);

        roster.Select(l => l.Name).ShouldBe(["frontend"]);
        roster.Single().Contour.ShouldBe("web/src");
        roster.Single().SerialOrder.ShouldBe(1);
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(1);
    }

    [Fact]
    public async Task SetLanes_UnknownName_IsRefusedNamingTheBirthPath()
    {
        var service = CreateService();
        await SeedLaneAsync("frontend");

        var ex = await Should.ThrowAsync<PlanningException>(() => service.SetLanesAsync(_projectId,
            [new LaneUpsert("frontend", null, null), new LaneUpsert("ghost", null, null)]));

        ex.Code.ShouldBe(PlanningFailureCode.LaneBirthRestricted);
        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Message.ShouldContain("planning_update_feature");
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(1);
    }

    [Fact]
    public async Task SetLanes_CaseOnlyRename_UpdatesInPlaceWithoutViolation()
    {
        var service = CreateService();
        await SeedLaneAsync("frontend");

        var roster = await service.SetLanesAsync(_projectId, [new LaneUpsert("Frontend", null, null)]);

        roster.Single().Name.ShouldBe("Frontend");
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(1);
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
        await SeedLaneAsync("frontend", "web/src");
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
        await SeedLaneAsync("frontend");
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
        await SeedLaneAsync("zeta", null, 2);
        await SeedLaneAsync("alpha", null, 1);
        await SeedLaneAsync("beta");

        var roster = await service.ListLanesAsync(_projectId);

        roster.Select(l => l.Name).ShouldBe(["alpha", "zeta", "beta"]);
    }

    [Fact]
    public async Task SetLanes_PriorityPersistsAndRoundTripsThroughTheRoster()
    {
        var service = CreateService();
        await SeedLaneAsync("frontend");
        await SeedLaneAsync("backend");

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
        await SeedLaneAsync("frontend", null, null, 1);

        var roster = await service.SetLanesAsync(_projectId, [new LaneUpsert("frontend", null, null)]);

        roster.Single().Priority.ShouldBeNull();
    }

    [Fact]
    public async Task ListLanes_PriorityFirst_NullsLast_SerialThenNameWithinEqualPriority()
    {
        var service = CreateService();
        await SeedLaneAsync("zeta");
        await SeedLaneAsync("alpha", null, 2, 2);
        await SeedLaneAsync("beta", null, null, 0);
        await SeedLaneAsync("gamma", null, 1, 2);

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
    public async Task RosterNames_ReturnsSortedNames()
    {
        var service = CreateService();
        await SeedLaneAsync("zeta");
        await SeedLaneAsync("alpha");

        var names = await service.RosterNamesAsync(_projectId);

        names.ShouldBe(["alpha", "zeta"]);
    }

    [Fact]
    public async Task UpsertLane_Create_InsertsAndEchoesTheRoster()
    {
        var service = CreateService();
        await SeedLaneAsync("frontend", "web/src", 1, 0);

        var roster = await service.UpsertLaneAsync(_projectId, new LaneUpsert("backend", "api/src", 2, 1), LaneWriteMode.Create);

        roster.Select(l => l.Name).ShouldBe(["frontend", "backend"]);
        var created = roster.Single(l => l.Name == "backend");
        created.Contour.ShouldBe("api/src");
        created.SerialOrder.ShouldBe(2);
        created.Priority.ShouldBe(1);
    }

    [Fact]
    public async Task UpsertLane_Create_ExistingName_ThrowsLaneNameDuplicate()
    {
        var service = CreateService();
        await SeedLaneAsync("frontend");

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            service.UpsertLaneAsync(_projectId, new LaneUpsert("Frontend", null, null), LaneWriteMode.Create));

        ex.Code.ShouldBe(PlanningFailureCode.LaneNameDuplicate);
        ex.Kind.ShouldBe(PlanningFailureKind.Duplicate);
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(1);
    }

    [Fact]
    public async Task UpsertLane_Create_OverTheCap_Throws()
    {
        var service = CreateService();
        foreach (var index in Enumerable.Range(0, LaneService.MaxLanesPerProject))
        {
            await SeedLaneAsync($"lane-{index}");
        }

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            service.UpsertLaneAsync(_projectId, new LaneUpsert("overflow", null, null), LaneWriteMode.Create));

        ex.Code.ShouldBe(PlanningFailureCode.LaneRosterTooLarge);
    }

    [Fact]
    public async Task UpsertLane_Create_TheSixtyFourthLane_IsAccepted()
    {
        var service = CreateService();
        foreach (var index in Enumerable.Range(0, LaneService.MaxLanesPerProject - 1))
        {
            await SeedLaneAsync($"lane-{index}");
        }

        var roster = await service.UpsertLaneAsync(
            _projectId, new LaneUpsert("the-last-one", null, null), LaneWriteMode.Create);

        roster.Count.ShouldBe(LaneService.MaxLanesPerProject);
    }

    [Theory]
    [InlineData("A. Domain")]
    [InlineData("edge-pi")]
    [InlineData("Фронтенд")]
    [InlineData("a.b")]
    [InlineData("Q3.plan")]
    [InlineData("..edge")]
    public async Task UpsertLane_Create_AddressableNames_AreAccepted(string name)
    {
        var roster = await CreateService()
            .UpsertLaneAsync(_projectId, new LaneUpsert(name, null, null), LaneWriteMode.Create);

        roster.ShouldHaveSingleItem().Name.ShouldBe(name);
    }

    [Theory]
    [InlineData("web/src")]
    [InlineData(@"infra\win")]
    [InlineData("edge\ttail")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task UpsertLane_Create_NameTheRouteCannotAddress_ThrowsInvalidLaneName(string name)
    {
        var ex = await Should.ThrowAsync<PlanningException>(() => CreateService()
            .UpsertLaneAsync(_projectId, new LaneUpsert(name, null, null), LaneWriteMode.Create));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidLaneName);
        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Message.ShouldContain(name.Trim());
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(0);
    }

    [Theory]
    [InlineData("web/src")]
    [InlineData(@"infra\win")]
    [InlineData("..")]
    public async Task SetLanes_NameTheRouteCannotAddress_ThrowsInvalidLaneName_AndWritesNothing(string name)
    {
        var service = CreateService();
        await SeedLaneAsync("frontend");

        var ex = await Should.ThrowAsync<PlanningException>(() => service.SetLanesAsync(_projectId,
            [new LaneUpsert("frontend", null, null), new LaneUpsert(name, null, null)]));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidLaneName);
        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(1);
    }

    [Theory]
    [InlineData("web/src")]
    [InlineData(@"infra\win")]
    [InlineData("..")]
    public async Task UpsertLane_Update_NameTheRouteCannotAddress_ThrowsInvalidLaneNameBeforeTheRosterLookup(
        string name)
    {
        var service = CreateService();
        await SeedLaneAsync("frontend");

        var ex = await Should.ThrowAsync<PlanningException>(() => service.UpsertLaneAsync(
            _projectId, new LaneUpsert(name, "web/src", null), LaneWriteMode.Update));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidLaneName);
        (await _context.PlanningLanes.SingleAsync(l => l.ProjectId == _projectId)).Name.ShouldBe("frontend");
    }

    [Fact]
    public async Task UpsertLane_Update_EditsMutableFields_KeepsTheStoredName()
    {
        var service = CreateService();
        await SeedLaneAsync("frontend", "old contour", 1, 0);

        var roster = await service.UpsertLaneAsync(
            _projectId, new LaneUpsert("frontend", "web/src", 4, 2), LaneWriteMode.Update);

        var lane = roster.ShouldHaveSingleItem();
        lane.Name.ShouldBe("frontend");
        lane.Contour.ShouldBe("web/src");
        lane.SerialOrder.ShouldBe(4);
        lane.Priority.ShouldBe(2);
    }

    [Fact]
    public async Task UpsertLane_Update_OmittedFields_ClearThem()
    {
        var service = CreateService();
        await SeedLaneAsync("frontend", "web/src", 1, 2);

        var roster = await service.UpsertLaneAsync(
            _projectId, new LaneUpsert("frontend", null, null, null), LaneWriteMode.Update);

        var lane = roster.ShouldHaveSingleItem();
        lane.Contour.ShouldBeNull();
        lane.SerialOrder.ShouldBeNull();
        lane.Priority.ShouldBeNull();
    }

    [Fact]
    public async Task UpsertLane_Update_CaseOnlyName_EditsInPlaceWithoutRenaming()
    {
        var service = CreateService();
        await SeedLaneAsync("frontend");

        var roster = await service.UpsertLaneAsync(
            _projectId, new LaneUpsert("Frontend", "web/src", null, null), LaneWriteMode.Update);

        roster.ShouldHaveSingleItem().Name.ShouldBe("frontend");
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(1);
    }

    [Fact]
    public async Task UpsertLane_Update_UnknownLane_ThrowsMissingLane()
    {
        var service = CreateService();
        await SeedLaneAsync("frontend");

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            service.UpsertLaneAsync(_projectId, new LaneUpsert("ghost", null, null), LaneWriteMode.Update));

        ex.Code.ShouldBe(PlanningFailureCode.MissingLane);
        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
    }

    [Fact]
    public async Task UpsertLane_Create_UnknownProject_ThrowsNotFound()
    {
        var missingProject = Guid.NewGuid();

        var ex = await Should.ThrowAsync<PlanningException>(() => CreateService()
            .UpsertLaneAsync(missingProject, new LaneUpsert("frontend", null, null), LaneWriteMode.Create));

        ex.Code.ShouldBe(PlanningFailureCode.MissingProject);
        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
        ex.Message.ShouldContain($"Project '{missingProject}' was not found.");
    }

    [Fact]
    public async Task DeleteLane_LaneWithOpenFeatures_IsRefused()
    {
        var service = CreateService();
        await SeedLaneAsync("frontend", "web/src", 1);
        await CreateFeatureAsync("011-gone-a", "frontend");
        await CreateFeatureAsync("012-gone-b", "frontend");

        var ex = await Should.ThrowAsync<PlanningException>(() => service.DeleteLaneAsync(_projectId, "Frontend"));

        ex.Code.ShouldBe(PlanningFailureCode.LaneStillOccupied);
        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Message.ShouldContain("011-gone-a");
        ex.Message.ShouldContain("012-gone-b");
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(1);
        (await _context.PlanningFeatures.SingleAsync(f => f.Slug == "011-gone-a")).Lane.ShouldBe("frontend");
    }

    [Fact]
    public async Task DeleteLane_EmptyLane_IsRemoved()
    {
        var service = CreateService();
        await SeedLaneAsync("frontend");
        await CreateFeatureAsync("011-gone", "frontend");
        await CreateFeatureService().DeleteAsync(_projectId, "011-gone");

        var roster = await service.DeleteLaneAsync(_projectId, "frontend");

        roster.ShouldBeEmpty();
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(0);
    }

    [Fact]
    public async Task DeleteLane_UnknownLane_IsAnIdempotentNoOp()
    {
        var service = CreateService();
        await SeedLaneAsync("frontend");

        var roster = await service.DeleteLaneAsync(_projectId, "ghost");

        roster.Single().Name.ShouldBe("frontend");
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteLane_UnknownProject_ThrowsNotFound()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() =>
            CreateService().DeleteLaneAsync(Guid.NewGuid(), "frontend"));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
    }

    [Fact]
    public async Task UpdateFeature_UnknownLane_BirthsTheLaneRow()
    {
        var features = CreateFeatureService();
        await features.CreateAsync(_projectId, "011-laned", "Laned", "body");

        var updated = await features.UpdateAsync(
            _projectId, "011-laned", null, null, null, "lane assigned", PlanningLogSource.Agent, lane: "brand-new");

        updated.Lane.ShouldBe("brand-new");
        var roster = await CreateService().ListLanesAsync(_projectId);
        var lane = roster.ShouldHaveSingleItem();
        lane.Name.ShouldBe("brand-new");
        lane.Contour.ShouldBeNull();
        lane.Priority.ShouldBeNull();
        lane.OpenFeatureCount.ShouldBe(1);
        lane.Features.Single().Slug.ShouldBe("011-laned");
    }

    [Fact]
    public async Task UpdateFeature_UnknownLane_OverTheCap_Throws()
    {
        foreach (var index in Enumerable.Range(0, LaneService.MaxLanesPerProject))
        {
            await SeedLaneAsync($"lane-{index}");
        }

        var features = CreateFeatureService();
        await features.CreateAsync(_projectId, "011-laned", "Laned", "body");

        var ex = await Should.ThrowAsync<PlanningException>(() => features.UpdateAsync(
            _projectId, "011-laned", null, null, null, "lane assigned", PlanningLogSource.Agent, lane: "overflow"));

        ex.Code.ShouldBe(PlanningFailureCode.LaneRosterTooLarge);
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId))
            .ShouldBe(LaneService.MaxLanesPerProject);
    }

    [Fact]
    public async Task UpdateFeature_UnknownLane_UnaddressableName_ThrowsInvalidLaneName()
    {
        var features = CreateFeatureService();
        await features.CreateAsync(_projectId, "011-laned", "Laned", "body");

        var ex = await Should.ThrowAsync<PlanningException>(() => features.UpdateAsync(
            _projectId, "011-laned", null, null, null, "lane assigned", PlanningLogSource.Agent, lane: "web/src"));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidLaneName);
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(0);
    }

    [Fact]
    public async Task SetStatus_LastOpenFeatureDone_RetiresTheLaneAndLogsTheAutoLine()
    {
        await SeedLaneAsync("frontend", "web/src");
        await SeedDoneReadyFeatureAsync("011-closing", "frontend");

        await CreateFeatureService().SetStatusAsync(
            _projectId, "011-closing", PlanningFeatureStatus.Done, "closing");

        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(0);
        var retired = await _context.PlanningFeatures.SingleAsync(f => f.Slug == "011-closing");
        var line = PlanningLog.Parse(retired.Body).Last();
        line.Source.ShouldBe(PlanningLogSource.Auto);
        line.Text.ShouldBe("lane 'frontend' retired: no open features remain");
    }

    [Fact]
    public async Task SetStatus_DoneFlip_KeepsTheLaneWhenAReviewFeatureRemains()
    {
        await SeedLaneAsync("frontend");
        await SeedDoneReadyFeatureAsync("011-closing", "frontend");
        await CreateFeatureAsync("012-review", "frontend");
        var features = CreateFeatureService();
        await features.SetStatusAsync(_projectId, "012-review", PlanningFeatureStatus.InProgress, "wip");
        await features.SetStatusAsync(_projectId, "012-review", PlanningFeatureStatus.Review, "review");

        await features.SetStatusAsync(_projectId, "011-closing", PlanningFeatureStatus.Done, "closing");

        var roster = await CreateService().ListLanesAsync(_projectId);
        var lane = roster.ShouldHaveSingleItem();
        lane.Name.ShouldBe("frontend");
        lane.OpenFeatureCount.ShouldBe(1);
        lane.Features.Select(f => f.Slug).ShouldBe(["011-closing", "012-review"]);
    }

    [Fact]
    public async Task UpdateFeature_ClearingTheLane_RetiresTheEmptiedLane()
    {
        await SeedLaneAsync("frontend");
        await SeedLaneAsync("backend");
        await CreateFeatureAsync("011-mover", "frontend");
        await CreateFeatureAsync("012-stays", "backend");
        var features = CreateFeatureService();

        await features.UpdateAsync(
            _projectId, "011-mover", null, null, null, "cleared", PlanningLogSource.Agent, lane: "");

        (await _context.PlanningFeatures.SingleAsync(f => f.Slug == "011-mover")).Lane.ShouldBeNull();
        var roster = await CreateService().ListLanesAsync(_projectId);
        roster.Select(l => l.Name).ShouldBe(["backend"]);
        roster.Single().Features.Single().Slug.ShouldBe("012-stays");
    }

    [Fact]
    public async Task UpdateFeature_MovingToANewLane_BirthsItAndRetiresTheEmptiedOne()
    {
        await SeedLaneAsync("frontend");
        await SeedLaneAsync("backend");
        await CreateFeatureAsync("011-mover", "frontend");
        await CreateFeatureAsync("012-stays", "backend");
        var features = CreateFeatureService();

        await features.UpdateAsync(
            _projectId, "011-mover", null, null, null, "moved", PlanningLogSource.Agent, lane: "brand-new");

        var roster = await CreateService().ListLanesAsync(_projectId);
        roster.Select(l => l.Name).ShouldBe(["backend", "brand-new"]);
        roster.Single(l => l.Name == "backend").Features.Select(f => f.Slug).ShouldBe(["012-stays"]);
        roster.Single(l => l.Name == "brand-new").Features.Select(f => f.Slug).ShouldBe(["011-mover"]);
        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(2);
    }

    [Fact]
    public async Task Import_StatusRestoredToDone_RetiresTheLane()
    {
        await SeedLaneAsync("frontend");
        await CreateFeatureAsync("011-imported", "frontend");
        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == "011-imported");
        feature.SetStatus(PlanningFeatureStatus.InProgress);
        await _context.SaveChangesAsync();

        await CreateFeatureService().ImportAsync(
            _projectId,
            new BacklogFeatureImport(
                "011-imported",
                "Imported",
                "<goal>imported</goal>",
                PlanningFeatureStatus.Done,
                FeaturePriority.P2,
                1,
                DateTime.UtcNow,
                [],
                [],
                []),
            CancellationToken.None);

        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(0);
        var body = (await _context.PlanningFeatures.SingleAsync(f => f.Slug == "011-imported")).Body;
        var line = PlanningLog.Parse(body).Last();
        line.Source.ShouldBe(PlanningLogSource.Auto);
        line.Text.ShouldContain("lane 'frontend' retired");
    }

    [Fact]
    public async Task Import_StatusUnchanged_KeepsTheLane()
    {
        await SeedLaneAsync("frontend");
        await CreateFeatureAsync("011-imported", "frontend");

        await CreateFeatureService().ImportAsync(
            _projectId,
            new BacklogFeatureImport(
                "011-imported",
                "Imported",
                "<goal>imported</goal>",
                PlanningFeatureStatus.Draft,
                FeaturePriority.P2,
                1,
                DateTime.UtcNow,
                [],
                [],
                []),
            CancellationToken.None);

        (await _context.PlanningLanes.CountAsync(l => l.ProjectId == _projectId)).ShouldBe(1);
        var body = (await _context.PlanningFeatures.SingleAsync(f => f.Slug == "011-imported")).Body;
        PlanningLog.Parse(body).ShouldNotContain(l => l.Source == PlanningLogSource.Auto);
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
