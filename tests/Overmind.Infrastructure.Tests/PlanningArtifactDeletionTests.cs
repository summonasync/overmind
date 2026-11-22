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

public sealed class PlanningArtifactDeletionTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;
    private int _featureCounter;

    public PlanningArtifactDeletionTests()
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
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Theory]
    [InlineData(PlanningArtifactKind.Spec)]
    [InlineData(PlanningArtifactKind.Plan)]
    [InlineData(PlanningArtifactKind.Result)]
    public async Task DeleteAsync_AgentDoor_DraftDocument_SoftDeletesWithAgentMarker(PlanningArtifactKind kind)
    {
        var feature = await SeedFeatureAsync();
        var service = CreateService();
        var artifact = await service.UpsertAsync(feature.Id, kind, "seed body", null, null, null, default);

        await service.DeleteAsync(artifact.Id, byHuman: false);

        var row = await RawArtifactAsync(artifact.Id);
        row.DeletedAt.ShouldNotBeNull();
        row.DeletedBy.ShouldBe(PlanningArtifact.AgentDeletionMarker);

        var persisted = await FeatureWithArtifactsAsync(feature.Id);
        persisted.Artifacts.ShouldBeEmpty();
        var last = PlanningLog.Parse(persisted.Body).Last();
        last.Text.ShouldBe($"artifact {kind.ToString().ToLowerInvariant()} deleted by agent");
        last.Source.ShouldBe(PlanningLogSource.Agent);
    }

    [Fact]
    public async Task DeleteAsync_AgentDoor_DoneTask_ThrowsDeletionRefused()
    {
        var feature = await SeedFeatureAsync();
        var service = CreateService();
        var task = await service.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "body", 0, null, null, default);
        await service.SetStatusAsync(task.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "done", default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.DeleteAsync(task.Id, byHuman: false));

        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Code.ShouldBe(PlanningFailureCode.ArtifactDeletionRefused);
        ex.Message.ShouldContain("draft artifacts only");
        (await RawArtifactAsync(task.Id)).DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_AgentDoor_ValidatedSpec_ThrowsDeletionRefused()
    {
        var feature = await SeedFeatureAsync();
        var service = CreateService();
        var spec = await service.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        await service.ValidateAsync(spec.Id, "validated via ui", PlanningLogSource.Ui, ct: default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.DeleteAsync(spec.Id, byHuman: false));

        ex.Code.ShouldBe(PlanningFailureCode.ArtifactDeletionRefused);
    }

    [Fact]
    public async Task DeleteAsync_HumanDoor_ValidatedSpec_Succeeds()
    {
        var feature = await SeedFeatureAsync();
        var service = CreateService();
        var spec = await service.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        await service.ValidateAsync(spec.Id, "validated via ui", PlanningLogSource.Ui, ct: default);

        await service.DeleteAsync(spec.Id, byHuman: true);

        var row = await RawArtifactAsync(spec.Id);
        row.DeletedBy.ShouldBe(PlanningArtifact.HumanDeletionMarker);
        row.Body.ShouldContain("validated via ui");
    }

    [Fact]
    public async Task DeleteAsync_TaskWithDependents_ThrowsCleanUpDependenciesFirst()
    {
        var feature = await SeedFeatureAsync();
        var service = CreateService();
        var blocker = await service.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "blocker", 0, null, null, default);
        await service.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "dependent", 1, null, null, default, blockedBy: [0]);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.DeleteAsync(blocker.Id, byHuman: false));

        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Code.ShouldBe(PlanningFailureCode.DeletionBlockedByDependents);
        ex.Message.ShouldContain("Clean up dependencies first");
        ex.Message.ShouldContain("task 01");
        (await FeatureWithArtifactsAsync(feature.Id)).Artifacts.Count.ShouldBe(2);
        (await CountEdgesAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteAsync_DependentLeafDropsItsOwnEdges_BlockerThenDeletable()
    {
        var feature = await SeedFeatureAsync();
        var service = CreateService();
        var blocker = await service.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "blocker", 0, null, null, default);
        var dependent = await service.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "dependent", 1, null, null, default, blockedBy: [0]);

        await service.DeleteAsync(dependent.Id, byHuman: false);
        await service.DeleteAsync(blocker.Id, byHuman: false);

        (await CountEdgesAsync()).ShouldBe(0);
        (await FeatureWithArtifactsAsync(feature.Id)).Artifacts.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_ValidatedSpecOnReviewFeature_DemotesFeatureToInProgress()
    {
        var (service, featureId, spec) = await SeedReviewFeatureAsync();

        await service.DeleteAsync(spec.Id, byHuman: true);

        var feature = await FeatureWithArtifactsAsync(featureId);
        feature.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        var lines = PlanningLog.Parse(feature.Body);
        lines.ShouldContain(l => l.Source == PlanningLogSource.Auto
            && l.Text == "feature demoted: review → inProgress (spec deleted)");
        lines.ShouldContain(l => l.Source == PlanningLogSource.Ui
            && l.Text == "artifact spec deleted by human");
    }

    [Fact]
    public async Task DeleteAsync_DoneTaskOnReviewFeature_ReopensFeature()
    {
        var (service, featureId, _) = await SeedReviewFeatureAsync();
        var second = await _context.PlanningArtifacts
            .AsNoTracking()
            .SingleAsync(a => a.FeatureId == featureId && a.Kind == PlanningArtifactKind.Task && a.Order == 1);

        await service.DeleteAsync(second.Id, byHuman: true);

        var feature = await FeatureWithArtifactsAsync(featureId);
        feature.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        PlanningLog.Parse(feature.Body).ShouldContain(l => l.Source == PlanningLogSource.Auto
            && l.Text == "task deleted while in review — reopened");
        PlanningLog.Parse(feature.Body).ShouldContain(l =>
            l.Text == "artifact task 01 deleted by human");
    }

    [Fact]
    public async Task DeleteAsync_DeletedTaskVanishesFromRoster_AuditLineCarriesReason()
    {
        var feature = await SeedFeatureAsync();
        var service = CreateService();
        var duplicate = await service.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "dup", 0, null, null, default);
        await service.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "kept", 1, null, null, default);

        await service.DeleteAsync(duplicate.Id, byHuman: false, "duplicate from upsert without order");

        var persisted = await FeatureWithArtifactsAsync(feature.Id);
        persisted.Artifacts.Select(a => a.Order).ShouldBe([1]);
        var auditLine = PlanningLog.Parse(persisted.Body)
            .Last(l => l.Text.StartsWith("artifact task", StringComparison.Ordinal));
        auditLine.Text.ShouldBe("artifact task 00 deleted by agent, duplicate from upsert without order");
        auditLine.Source.ShouldBe(PlanningLogSource.Agent);
    }

    [Fact]
    public async Task DeleteAsync_AlreadyDeletedArtifact_ThrowsNotFound()
    {
        var feature = await SeedFeatureAsync();
        var service = CreateService();
        var artifact = await service.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);

        await service.DeleteAsync(artifact.Id, byHuman: true);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.DeleteAsync(artifact.Id, byHuman: true));
        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
    }

    [Fact]
    public async Task UpsertAsync_AfterDeletion_ReusesTheFreedOrder()
    {
        var feature = await SeedFeatureAsync();
        var service = CreateService();
        var task = await service.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "first", 0, null, null, default);

        await service.DeleteAsync(task.Id, byHuman: false);
        var recreated = await service.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "replacement", task.Order, null, null, default);

        recreated.Id.ShouldNotBe(task.Id);
        recreated.Order.ShouldBe(task.Order);
        (await FeatureWithArtifactsAsync(feature.Id)).Artifacts.Count.ShouldBe(1);
    }

    private PlanningArtifactService CreateService() =>
        new(
            _context,
            TestPlanningDispatcher.Create(_context, _materializer),
            new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)));

    private async Task<PlanningFeature> SeedFeatureAsync()
    {
        var feature = PlanningFeature.Create(
            _projectId, $"del-{++_featureCounter:D2}-{Guid.NewGuid():N}".ToLowerInvariant(), "Deletion", "body");
        _context.PlanningFeatures.Add(feature);
        await _context.SaveChangesAsync();
        return feature;
    }

    private async Task<(PlanningArtifactService Service, Guid FeatureId, PlanningArtifact Spec)> SeedReviewFeatureAsync()
    {
        var feature = await SeedFeatureAsync();
        var service = CreateService();
        var spec = await service.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        await service.ValidateAsync(spec.Id, "validated via ui", PlanningLogSource.Ui, ct: default);
        var plan = await service.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var first = await service.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task 1", 0, null, null, default);
        var second = await service.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task 2", 1, null, null, default);
        await service.ValidateAsync(plan.Id, "validated via ui", PlanningLogSource.Ui, ct: default);
        await service.SetStatusAsync(first.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "done 1", default);
        await service.SetStatusAsync(second.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "done 2", default);

        var persisted = await FeatureWithArtifactsAsync(feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.Review);
        return (service, feature.Id, spec);
    }

    private async Task<PlanningFeature> FeatureWithArtifactsAsync(Guid featureId)
    {
        using var verify = _db.CreateContext();
        return await verify.PlanningFeatures
            .Include(f => f.Artifacts)
            .AsNoTracking()
            .FirstAsync(f => f.Id == featureId);
    }

    private async Task<PlanningArtifact> RawArtifactAsync(Guid artifactId)
    {
        using var verify = _db.CreateContext();
        return await verify.PlanningArtifacts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(a => a.Id == artifactId);
    }

    private async Task<int> CountEdgesAsync()
    {
        using var verify = _db.CreateContext();
        return await verify.PlanningTaskDependencies.CountAsync();
    }
}
