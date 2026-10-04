using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class FeatureDependencyServiceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;

    public FeatureDependencyServiceTests()
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

    private PlanningFeatureService CreateFeatureService() =>
        new(
            _context,
            _materializer,
            new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
            new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)),
            TestPlanningDispatcher.Create(_context, _materializer),
            NullLogger<PlanningFeatureService>.Instance);

    private FeatureDependencyService CreateDependencyService() =>
        new(_context, TestPlanningDispatcher.Create(_context, _materializer));

    private async Task<PlanningFeature> CreateFeatureAsync(string slug, PlanningFeatureStatus? status = null)
    {
        var service = CreateFeatureService();
        var feature = await service.CreateAsync(_projectId, slug, slug, string.Empty);
        if (status.HasValue && status.Value != PlanningFeatureStatus.Draft)
        {
            foreach (var step in PathTo(status.Value))
            {
                await service.SetStatusAsync(_projectId, slug, step, "test move");
            }
        }

        return feature;
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

    [Fact]
    public async Task AddDependency_SelfDependency_RejectedAsCycle()
    {
        var service = CreateDependencyService();
        await CreateFeatureAsync("alpha");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.AddDependencyAsync(_projectId, "alpha", "alpha"));
        ex.Code.ShouldBe(PlanningFailureCode.DependencyCycle);
        ex.Message.ShouldContain("alpha -> alpha");
    }

    [Fact]
    public async Task AddDependency_DirectCycle_RejectedWithChain()
    {
        var service = CreateDependencyService();
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await service.AddDependencyAsync(_projectId, "beta", "alpha");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.AddDependencyAsync(_projectId, "alpha", "beta"));
        ex.Code.ShouldBe(PlanningFailureCode.DependencyCycle);
        ex.Message.ShouldContain("alpha -> beta -> alpha");
    }

    [Fact]
    public async Task AddDependency_TransitiveCycle_RejectedWithFullChain()
    {
        var service = CreateDependencyService();
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma");
        await CreateFeatureAsync("delta");
        await service.AddDependencyAsync(_projectId, "beta", "alpha");
        await service.AddDependencyAsync(_projectId, "gamma", "beta");
        await service.AddDependencyAsync(_projectId, "delta", "gamma");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.AddDependencyAsync(_projectId, "alpha", "delta"));
        ex.Code.ShouldBe(PlanningFailureCode.DependencyCycle);
        ex.Message.ShouldContain("alpha -> delta -> gamma -> beta -> alpha");
    }

    [Fact]
    public async Task AddDependency_NoPathBetweenFeatures_Succeeds()
    {
        var service = CreateDependencyService();
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma");

        var view = await service.AddDependencyAsync(_projectId, "beta", "alpha");
        view.BlockedBy.Select(e => e.Slug).ShouldBe(["alpha"]);

        var unrelated = await service.AddDependencyAsync(_projectId, "gamma", "beta");
        unrelated.BlockedBy.Select(e => e.Slug).ShouldBe(["beta"]);
    }

    [Fact]
    public async Task AddDependency_MissingBlocker_ThrowsMissingFeature()
    {
        var service = CreateDependencyService();
        await CreateFeatureAsync("alpha");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.AddDependencyAsync(_projectId, "alpha", "ghost"));
        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
    }

    [Fact]
    public async Task ListDependencies_ReturnsBothDirections()
    {
        var service = CreateDependencyService();
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("gamma");
        await service.AddDependencyAsync(_projectId, "beta", "alpha");
        await service.AddDependencyAsync(_projectId, "gamma", "alpha");

        var asBlocker = await service.ListDependenciesAsync(_projectId, "alpha");
        asBlocker.Slug.ShouldBe("alpha");
        asBlocker.BlockedBy.ShouldBeEmpty();
        asBlocker.Blocks.Select(e => e.Slug).ShouldBe(["beta", "gamma"]);

        var asDependent = await service.ListDependenciesAsync(_projectId, "beta");
        asDependent.BlockedBy.Select(e => e.Slug).ShouldBe(["alpha"]);
        asDependent.Blocks.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveDependency_LiftsLinkOnBothSides()
    {
        var service = CreateDependencyService();
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");
        await service.AddDependencyAsync(_projectId, "beta", "alpha");

        await service.RemoveDependencyAsync(_projectId, "beta", "alpha");

        var dependent = await service.ListDependenciesAsync(_projectId, "beta");
        dependent.BlockedBy.ShouldBeEmpty();
        var blocker = await service.ListDependenciesAsync(_projectId, "alpha");
        blocker.Blocks.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveDependency_AbsentLink_IsNoOp()
    {
        var service = CreateDependencyService();
        await CreateFeatureAsync("alpha");
        await CreateFeatureAsync("beta");

        var view = await service.RemoveDependencyAsync(_projectId, "beta", "alpha");
        view.BlockedBy.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(PlanningFeatureStatus.Draft)]
    [InlineData(PlanningFeatureStatus.Ready)]
    [InlineData(PlanningFeatureStatus.InProgress)]
    public async Task ListDependencies_BlockerNotDoneOrReview_KeepsBlock(PlanningFeatureStatus status)
    {
        var service = CreateDependencyService();
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("alpha", status);
        await service.AddDependencyAsync(_projectId, "beta", "alpha");

        var view = await service.ListDependenciesAsync(_projectId, "beta");

        var entry = view.BlockedBy.Single();
        entry.Slug.ShouldBe("alpha");
        entry.Status.ShouldBe(status);
        entry.Closed.ShouldBeFalse();
    }

    [Fact]
    public async Task ListDependencies_BlockerDone_LiftsBlock()
    {
        var service = CreateDependencyService();
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("alpha", PlanningFeatureStatus.Done);
        await service.AddDependencyAsync(_projectId, "beta", "alpha");

        var view = await service.ListDependenciesAsync(_projectId, "beta");
        view.BlockedBy.Single().Closed.ShouldBeTrue();
    }

    [Fact]
    public async Task ListDependencies_BlockerReview_LiftsBlock()
    {
        var service = CreateDependencyService();
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("alpha", PlanningFeatureStatus.Review);
        await service.AddDependencyAsync(_projectId, "beta", "alpha");

        var view = await service.ListDependenciesAsync(_projectId, "beta");
        view.BlockedBy.Single().Closed.ShouldBeTrue();
    }

    [Fact]
    public async Task ListDependencies_BlockerLeavesReview_ReEngagesBlock()
    {
        var service = CreateDependencyService();
        await CreateFeatureAsync("beta");
        await CreateFeatureAsync("alpha", PlanningFeatureStatus.Review);
        await service.AddDependencyAsync(_projectId, "beta", "alpha");

        await CreateFeatureService().SetStatusAsync(_projectId, "alpha", PlanningFeatureStatus.InProgress, "back to work");

        var view = await service.ListDependenciesAsync(_projectId, "beta");
        view.BlockedBy.Single().Closed.ShouldBeFalse();
    }
}
