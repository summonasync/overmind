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

public sealed class WaveSnapshotServiceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;
    private readonly Guid _sessionId;

    public WaveSnapshotServiceTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));

        var project = Project.Create($"wave-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        var session = AgentSession.Create("marshal", "claude");
        _context.AgentSessions.Add(session);
        _context.SaveChanges();
        _projectId = project.Id;
        _sessionId = session.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Snapshot_AggregatesQueueClaimsBlockersAndEntries()
    {
        await CreateFeatureAsync("lane-a", PlanningFeatureStatus.InProgress);
        await CreateFeatureAsync("lane-b");
        await CreateFeatureAsync("lane-c");
        await Claims().ClaimAsync(_projectId, "lane-a", "marshal", "claude", _sessionId);
        await Dependencies().AddDependencyAsync(_projectId, "lane-b", "lane-c");
        var syncEntry = await CreateEntryAsync("wave standup", "lane-a is moving", "wave-sync");
        await CreateEntryAsync("unrelated", "no wave tags here", "release");
        await CreateEntryAsync("untagged", "no tags at all");

        var snapshot = await Service().SnapshotAsync(_projectId);

        snapshot.QueueFeatures.Select(f => f.Slug).ShouldBe(["lane-a", "lane-b", "lane-c"]);
        snapshot.InFlightFeatures.Select(f => f.Slug).ShouldBe(["lane-a"]);
        snapshot.CoordinationTags.ShouldBe(["wave-sync", "lane-marshalling"]);
        snapshot.CoordinationEntries.Select(e => e.Id).ShouldBe([syncEntry.Id]);

        var claimLiveness = snapshot.ClaimLiveness.ShouldBeAssignableTo<IReadOnlyDictionary<Guid, AgentSessionLiveness>>();
        claimLiveness.ShouldContainKey(snapshot.InFlightFeatures.Single().Id);
    }

    [Fact]
    public async Task Snapshot_InFlightCoversReviewWithoutClaim()
    {
        await CreateFeatureAsync("reviewing", PlanningFeatureStatus.Review);
        await CreateFeatureAsync("queued");

        var snapshot = await Service().SnapshotAsync(_projectId);

        snapshot.InFlightFeatures.Select(f => f.Slug).ShouldBe(["reviewing"]);
        snapshot.ClaimLiveness.ShouldBeEmpty();
    }

    [Fact]
    public async Task Snapshot_DoneFeaturesDropOutOfTheQueue()
    {
        await CreateFeatureAsync("shipped", PlanningFeatureStatus.Done);
        await CreateFeatureAsync("alive");

        var snapshot = await Service().SnapshotAsync(_projectId);

        snapshot.QueueFeatures.Select(f => f.Slug).ShouldBe(["alive"]);
        snapshot.InFlightFeatures.ShouldBeEmpty();
    }

    [Fact]
    public async Task Snapshot_CustomTagList_ReplacesDefaultsAndEchoesIt()
    {
        await CreateEntryAsync("default tagged", "default", "wave-sync");
        var custom = await CreateEntryAsync("custom tagged", "custom", "custom-lane");

        var snapshot = await Service().SnapshotAsync(_projectId, ["custom-lane"]);

        snapshot.CoordinationTags.ShouldBe(["custom-lane"]);
        snapshot.CoordinationEntries.Select(e => e.Id).ShouldBe([custom.Id]);
    }

    [Fact]
    public async Task Snapshot_BlankTags_YieldEmptyEntriesSection()
    {
        await CreateEntryAsync("tagged", "body", "wave-sync");

        var snapshot = await Service().SnapshotAsync(_projectId, ["   "]);

        snapshot.CoordinationTags.ShouldBeEmpty();
        snapshot.CoordinationEntries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Snapshot_EntryLimit_CapsTheSection()
    {
        for (var i = 0; i < 3; i++)
        {
            await CreateEntryAsync($"sync {i}", "body", "wave-sync");
        }

        var snapshot = await Service().SnapshotAsync(_projectId, entryLimit: 2);

        snapshot.CoordinationEntries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Snapshot_EntryLimit_AboveTheCapIsClamped()
    {
        for (var i = 0; i <= WaveSnapshotService.MaxEntryLimit; i++)
        {
            await CreateEntryAsync($"sync {i}", "body", "wave-sync");
        }

        var snapshot = await Service().SnapshotAsync(_projectId, entryLimit: 5000);

        snapshot.CoordinationEntries.Count.ShouldBe(WaveSnapshotService.MaxEntryLimit);
    }

    [Fact]
    public async Task Snapshot_UnknownProject_ThrowsMissingProject()
    {
        var ex = await Should.ThrowAsync<PlanningException>(
            () => Service().SnapshotAsync(Guid.NewGuid()));

        ex.Code.ShouldBe(PlanningFailureCode.MissingProject);
    }

    [Fact]
    public async Task Snapshot_LaneBoundAliases_CarryOnlyBoundLanes()
    {
        _context.PlanningLanes.Add(PlanningLane.Create(_projectId, "frontend", null, null, null, "johan"));
        _context.PlanningLanes.Add(PlanningLane.Create(_projectId, "backend", null, null));
        await CreateFeatureAsync("lane-a");
        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == "lane-a");
        feature.SetLane("frontend");
        await _context.SaveChangesAsync();

        var snapshot = await Service().SnapshotAsync(_projectId);

        snapshot.LaneBoundAliases.Keys.ShouldBe(["frontend"]);
        snapshot.LaneBoundAliases["frontend"].ShouldBe("johan");
    }

    private WaveSnapshotService Service() =>
        new(_context, new FeatureClaimService(_context, Dispatcher()), new DirectiveService(_context));

    private FeatureClaimService Claims() => new(_context, Dispatcher());

    private FeatureDependencyService Dependencies() => new(_context, Dispatcher());

    private PlanningMaterializationDispatcher Dispatcher() =>
        TestPlanningDispatcher.Create(_context, _materializer);

    private PlanningFeatureService CreateFeatureService() =>
        new(
            _context,
            _materializer,
            new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
            new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)),
            TestPlanningDispatcher.Create(_context, _materializer),
            NullLogger<PlanningFeatureService>.Instance);

    private async Task CreateFeatureAsync(string slug, PlanningFeatureStatus? status = null)
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, slug, slug, string.Empty);
        if (status == PlanningFeatureStatus.Done)
        {
            var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == slug);
            _context.PlanningArtifacts.Add(
                PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Result, "close-out"));
            await _context.SaveChangesAsync();
        }

        if (status.HasValue && status.Value != PlanningFeatureStatus.Draft)
        {
            List<PlanningFeatureStatus> path = status.Value switch
            {
                PlanningFeatureStatus.InProgress => [PlanningFeatureStatus.InProgress],
                PlanningFeatureStatus.Review => [PlanningFeatureStatus.InProgress, PlanningFeatureStatus.Review],
                PlanningFeatureStatus.Done => [PlanningFeatureStatus.InProgress, PlanningFeatureStatus.Review, PlanningFeatureStatus.Done],
                _ => [],
            };
            foreach (var step in path)
            {
                await service.SetStatusAsync(_projectId, slug, step, "test move");
            }
        }
    }

    private async Task<ContextEntry> CreateEntryAsync(string title, string content, params string[] tagNames)
    {
        var entry = ContextEntry.Create(_projectId, EntryType.Note, title, content);
        foreach (var name in tagNames)
        {
            var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == name);
            if (tag is null)
            {
                tag = Tag.Create(name);
                _context.Tags.Add(tag);
            }

            entry.AddTag(tag);
        }

        _context.ContextEntries.Add(entry);
        await _context.SaveChangesAsync();
        return entry;
    }
}
