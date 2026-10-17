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

public sealed class PlanningFeatureCodeSurfacesTests : IDisposable, IAsyncLifetime
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;

    public PlanningFeatureCodeSurfacesTests()
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

    [Fact]
    public async Task Update_NullCodeSurfaces_LeavesDeclaredSurfacesUntouched()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Title", "body");
        await service.UpdateAsync(_projectId, "feat", null, null, null,
            lane: null, codeSurfaces: ["src/web/**"], ct: default);

        await service.UpdateAsync(_projectId, "feat", "Renamed", null, null, default);

        var stored = await StoredFeatureAsync("feat");
        CodeSurfacesJson.Deserialize(stored.CodeSurfaces).ShouldBe(["src/web/**"]);
    }

    [Fact]
    public async Task Update_EmptyArray_ClearsDeclaredSurfaces()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Title", "body");
        await service.UpdateAsync(_projectId, "feat", null, null, null,
            lane: null, codeSurfaces: ["src/web/**"], ct: default);

        await service.UpdateAsync(_projectId, "feat", null, null, null,
            lane: null, codeSurfaces: [], ct: default);

        var stored = await StoredFeatureAsync("feat");
        stored.CodeSurfaces.ShouldBeNull();
    }

    [Fact]
    public async Task Update_Array_FullyReplacesPreviousSurfaces()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Title", "body");
        await service.UpdateAsync(_projectId, "feat", null, null, null,
            lane: null, codeSurfaces: ["src/web/**", "docs/**"], ct: default);

        await service.UpdateAsync(_projectId, "feat", null, null, null,
            lane: null, codeSurfaces: ["src/api/**/*.cs"], ct: default);

        var stored = await StoredFeatureAsync("feat");
        CodeSurfacesJson.Deserialize(stored.CodeSurfaces).ShouldBe(["src/api/**/*.cs"]);
    }

    [Fact]
    public async Task Update_HumanDoorEdit_AppendsLogLineAndTouchesUpdatedAt()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Title", "body");
        var before = (await StoredFeatureAsync("feat")).UpdatedAt;

        var updated = await service.UpdateAsync(_projectId, "feat", null, null, null,
            lane: null, codeSurfaces: ["src/web/**"], ct: default);
        var stored = await StoredFeatureAsync("feat");

        stored.UpdatedAt.ShouldBeGreaterThan(before);
        updated.LastLogLine.ShouldContain("code surfaces set");
        stored.Body.ShouldContain("code surfaces set");
    }

    [Fact]
    public async Task Update_HumanDoorClear_AppendsClearedLine()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Title", "body");
        await service.UpdateAsync(_projectId, "feat", null, null, null,
            lane: null, codeSurfaces: ["src/web/**"], ct: default);

        await service.UpdateAsync(_projectId, "feat", null, null, null,
            lane: null, codeSurfaces: [], ct: default);

        var stored = await StoredFeatureAsync("feat");
        stored.Body.ShouldContain("code surfaces cleared");
    }

    [Fact]
    public async Task Update_HumanDoorSameValue_NoSecondLogLine()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Title", "body");
        await service.UpdateAsync(_projectId, "feat", null, null, null,
            lane: null, codeSurfaces: ["src/web/**"], ct: default);
        var afterFirstSet = (await StoredFeatureAsync("feat")).Body;

        await service.UpdateAsync(_projectId, "feat", null, null, null,
            lane: null, codeSurfaces: ["src/web/**"], ct: default);

        var stored = await StoredFeatureAsync("feat");
        stored.Body.ShouldBe(afterFirstSet);
    }

    [Fact]
    public async Task Update_AgentDoorOverload_CarriesSurfaces()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Title", "body");

        var updated = await service.UpdateAsync(
            _projectId, "feat", null, null, null, "declaring surfaces",
            PlanningLogSource.Agent, lane: null, ["src/web/**"], ct: default);

        var stored = await StoredFeatureAsync("feat");
        stored.CodeSurfaces.ShouldBe(updated.CodeSurfaces);
        CodeSurfacesJson.Deserialize(stored.CodeSurfaces).ShouldBe(["src/web/**"]);
        updated.LastLogLine.ShouldBe("declaring surfaces");
        stored.Body.ShouldNotContain("code surfaces set");
    }

    [Fact]
    public async Task Update_SurfacesSurviveReloadRoundTrip()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Title", "body");

        await service.UpdateAsync(_projectId, "feat", null, null, null,
            lane: null, codeSurfaces: ["src/web/**", "src/shared/*.cs"], ct: default);

        var fresh = await StoredFeatureAsync("feat");
        CodeSurfacesJson.Deserialize(fresh.CodeSurfaces).ShouldBe(["src/web/**", "src/shared/*.cs"]);
    }

    private async Task<PlanningFeature> StoredFeatureAsync(string slug) =>
        await _context.PlanningFeatures.AsNoTracking()
            .SingleAsync(f => f.ProjectId == _projectId && f.Slug == slug);

    private PlanningFeatureService CreateFeatureService() =>
        new(
            _context,
            _materializer,
            new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
            new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)),
            TestPlanningDispatcher.Create(_context, _materializer),
            NullLogger<PlanningFeatureService>.Instance);
}
