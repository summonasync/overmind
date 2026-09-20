using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningServiceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;

    public PlanningServiceTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], []));
        _materializer.ClearAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], []));

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
            NullLogger<PlanningFeatureService>.Instance);

    private PlanningArtifactService CreateArtifactService() =>
        new(_context, _materializer, NullLogger<PlanningArtifactService>.Instance);

    [Fact]
    public async Task CreateFeature_ValidSlug_Succeeds()
    {
        var service = CreateFeatureService();
        var feature = await service.CreateAsync(_projectId, "my-feature", "Title", "body");

        feature.Slug.ShouldBe("my-feature");
        feature.Title.ShouldBe("Title");
        feature.Version.ShouldBe(1);
        feature.Status.ShouldBe(PlanningFeatureStatus.Draft);
    }

    [Fact]
    public async Task CreateFeature_DuplicateSlug_ThrowsDuplicateSlug()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "dup", "Title", "body");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.CreateAsync(_projectId, "dup", "Title2", "body2"));
        ex.Kind.ShouldBe(PlanningFailureKind.Duplicate);
    }

    [Fact]
    public async Task CreateFeature_InvalidSlug_ThrowsInvalidSlug()
    {
        var service = CreateFeatureService();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.CreateAsync(_projectId, "INVALID", "Title", "body"));
        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
    }

    [Fact]
    public async Task CreateFeature_MissingProject_ThrowsMissingProject()
    {
        var service = CreateFeatureService();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.CreateAsync(Guid.NewGuid(), "slug", "Title", "body"));
        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
    }

    [Fact]
    public async Task UpdateFeature_NullFields_Untouched()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Original Title", "original body");

        var updated = await service.UpdateAsync(_projectId, "feat", null, null, null, default);

        updated.Title.ShouldBe("Original Title");
        updated.Body.ShouldBe("original body");
    }

    [Fact]
    public async Task UpdateFeature_TitleOnly_KeepsBody()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Old", "body content");

        var updated = await service.UpdateAsync(_projectId, "feat", "New", null, null, default);

        updated.Title.ShouldBe("New");
        updated.Body.ShouldBe("body content");
    }

    [Fact]
    public async Task UpdateFeature_StatusChange_BumpsVersion()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Title", "body");

        await service.UpdateAsync(_projectId, "feat", null, null, PlanningFeatureStatus.Ready, default);
        var updated = await service.UpdateAsync(_projectId, "feat", "New Title", null, null, default);

        updated.Status.ShouldBe(PlanningFeatureStatus.Ready);
        updated.Version.ShouldBe(2);
    }

    [Fact]
    public async Task DeleteFeature_ClearsMaterializationBeforeRowsRemoved()
    {
        var service = CreateFeatureService();
        var feature = await service.CreateAsync(_projectId, "to-delete", "Title", "body");
        var featureId = feature.Id;

        await service.DeleteAsync(_projectId, "to-delete", default);

        await _materializer.Received(1)
            .ClearAsync(_projectId, featureId, Arg.Any<CancellationToken>());

        var exists = await _context.PlanningFeatures.AnyAsync(f => f.Id == featureId);
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteFeature_MissingFeature_Throws()
    {
        var service = CreateFeatureService();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.DeleteAsync(_projectId, "nonexistent", default));
        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
    }

    [Fact]
    public async Task SetCurrent_Pin_SetsFlag()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat-a", "Title A", "body");

        var pinned = await service.SetCurrentAsync(_projectId, "feat-a", true, default);

        pinned.IsCurrent.ShouldBeTrue();
        pinned.Status.ShouldBe(PlanningFeatureStatus.Draft);

        var persisted = await _context.PlanningFeatures
            .FirstAsync(f => f.ProjectId == _projectId && f.Slug == "feat-a");
        persisted.IsCurrent.ShouldBeTrue();
    }

    [Fact]
    public async Task SetCurrent_PinSecondFeature_UnpinsFirst()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat-a", "Title A", "body");
        await service.CreateAsync(_projectId, "feat-b", "Title B", "body");
        await service.SetCurrentAsync(_projectId, "feat-a", true, default);

        var pinned = await service.SetCurrentAsync(_projectId, "feat-b", true, default);

        pinned.IsCurrent.ShouldBeTrue();

        var states = await _context.PlanningFeatures
            .Where(f => f.ProjectId == _projectId)
            .ToDictionaryAsync(f => f.Slug, f => f.IsCurrent);
        states["feat-a"].ShouldBeFalse();
        states["feat-b"].ShouldBeTrue();
    }

    [Fact]
    public async Task SetCurrent_Unpin_ClearsFlag()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat-a", "Title A", "body");
        await service.SetCurrentAsync(_projectId, "feat-a", true, default);

        var unpinned = await service.SetCurrentAsync(_projectId, "feat-a", false, default);

        unpinned.IsCurrent.ShouldBeFalse();

        var persisted = await _context.PlanningFeatures
            .FirstAsync(f => f.ProjectId == _projectId && f.Slug == "feat-a");
        persisted.IsCurrent.ShouldBeFalse();
    }

    [Fact]
    public async Task SetCurrent_TwoProjects_KeepIndependentCurrents()
    {
        var otherProject = Project.Create($"test-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(otherProject);
        await _context.SaveChangesAsync();

        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat-a", "Title A", "body");
        await service.CreateAsync(otherProject.Id, "feat-b", "Title B", "body");

        await service.SetCurrentAsync(_projectId, "feat-a", true, default);
        await service.SetCurrentAsync(otherProject.Id, "feat-b", true, default);

        var states = await _context.PlanningFeatures
            .Where(f => f.Slug == "feat-a" || f.Slug == "feat-b")
            .ToDictionaryAsync(f => f.Slug, f => f.IsCurrent);
        states["feat-a"].ShouldBeTrue();
        states["feat-b"].ShouldBeTrue();
    }

    [Fact]
    public async Task SetCurrent_MissingFeature_ThrowsNotFound()
    {
        var service = CreateFeatureService();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.SetCurrentAsync(_projectId, "nonexistent", true, default));
        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
    }

    [Fact]
    public async Task UpsertArtifact_Create_Succeeds()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec", null, null, null, default);

        artifact.Kind.ShouldBe(PlanningArtifactKind.Spec);
        artifact.Version.ShouldBe(1);
        artifact.Order.ShouldBe(0);
    }

    [Fact]
    public async Task UpsertArtifact_ExistingSpec_VersionsBody()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "v1", null, null, null, default);
        var updated = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "v2", null, null, null, default);

        updated.Version.ShouldBe(2);
        updated.Body.ShouldBe("v2");

        var versions = await _context.PlanningArtifactVersions
            .Where(v => v.ArtifactId == updated.Id)
            .OrderBy(v => v.Version)
            .ToListAsync();
        versions.Count.ShouldBe(1);
        versions[0].Version.ShouldBe(1);
        versions[0].Body.ShouldBe("v1");
    }

    [Fact]
    public async Task UpsertArtifact_ExistingWithReview_UpdatesReviewWithoutLeavingATrace()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "v1", 0, PlanningReviewLevel.Light, null, default);

        var updated = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "v2", 0, PlanningReviewLevel.Hard, "body edit", default);

        updated.Review.ShouldBe(PlanningReviewLevel.Hard);
        updated.Version.ShouldBe(2);

        var versions = await _context.PlanningArtifactVersions
            .Where(v => v.ArtifactId == updated.Id)
            .ToListAsync();
        versions.Count.ShouldBe(1);
        versions[0].Body.ShouldBe("v1");

        var log = PlanningLog.Parse(updated.Body);
        log.Count.ShouldBe(1);
        log[0].Text.ShouldBe("body edit");
    }

    [Fact]
    public async Task UpsertArtifact_TaskOrderAutoIncrement()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        var task2 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);

        task1.Order.ShouldBe(0);
        task2.Order.ShouldBe(1);
    }

    [Fact]
    public async Task UpsertArtifact_FrozenWithoutNote_ThrowsChangeNoteRequired()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "v1", null, null, null, default);
        await artifactService.ValidateAsync(artifact.Id, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.UpsertAsync(
                feature.Id, PlanningArtifactKind.Spec, "v2", null, null, null, default));
        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
    }

    [Fact]
    public async Task ValidateAsync_Task_ThrowsCannotValidateTask()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var task = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.ValidateAsync(task.Id, default));
        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
    }

    [Fact]
    public async Task SetStatusAsync_InvalidStatus_ThrowsInvalidStatusForKind()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.SetStatusAsync(spec.Id, PlanningArtifactStatus.Done, "note", default));
        ex.Kind.ShouldBe(PlanningFailureKind.InvalidStatus);
    }

    [Fact]
    public async Task SetStatusAsync_EmptyNote_ThrowsChangeNoteRequired()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.SetStatusAsync(spec.Id, PlanningArtifactStatus.Draft, "  ", default));
        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
    }

    [Fact]
    public async Task UpsertArtifact_CreateSpec_OnDraftFeature_PromotesToInProgress()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "# Spec", null, null, null, default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        persisted.Version.ShouldBe(1);
    }

    [Fact]
    public async Task UpsertArtifact_CreatePlan_OnReadyFeature_PromotesToInProgress()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");
        await featureService.UpdateAsync(_projectId, "feat", null, null, PlanningFeatureStatus.Ready, default);

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "# Plan", null, null, null, default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        persisted.Version.ShouldBe(1);
    }

    [Fact]
    public async Task UpsertArtifact_UpdateExisting_OnDraftFeature_DoesNotPromote()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "v1", null, null, null, default);
        await featureService.UpdateAsync(_projectId, "feat", null, null, PlanningFeatureStatus.Draft, default);

        var updated = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "v2", null, null, null, default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.Draft);
        updated.Version.ShouldBe(2);
    }

    [Fact]
    public async Task UpsertArtifact_CreateTask_OnReviewFeature_DemotesToInProgress()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, "task 1 done", default);

        var inReview = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        inReview.Status.ShouldBe(PlanningFeatureStatus.Review);

        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        persisted.Version.ShouldBe(1);
    }

    [Fact]
    public async Task UpsertArtifact_CreateResult_OnReviewFeature_StaysReview()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, "task 1 done", default);

        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Result, "# Result", null, null, null, default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.Review);
    }

    [Fact]
    public async Task UpsertArtifact_CreateTask_OnDoneFeature_StaysDone()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, "task 1 done", default);
        var closed = await featureService.UpdateAsync(
            _projectId, "feat", null, null, PlanningFeatureStatus.Done, default);

        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.Done);
        persisted.Version.ShouldBe(closed.Version);
    }

    [Fact]
    public async Task SetStatusAsync_LastTaskDone_WithSpecAndPlan_PromotesToReview()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        var task2 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);

        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, "task 1 done", default);

        var midFlight = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        midFlight.Status.ShouldBe(PlanningFeatureStatus.InProgress);

        await artifactService.SetStatusAsync(task2.Id, PlanningArtifactStatus.Done, "task 2 done", default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.Review);
        persisted.Version.ShouldBe(1);
    }

    [Fact]
    public async Task SetStatusAsync_LastTaskDone_MissingPlan_StaysInProgress()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        var task2 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);

        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, "task 1 done", default);
        await artifactService.SetStatusAsync(task2.Id, PlanningArtifactStatus.Done, "task 2 done", default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.InProgress);
    }

    [Fact]
    public async Task SetStatusAsync_LastTaskDone_OnDoneFeature_StaysDone()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        var task2 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);

        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, "task 1 done", default);
        await featureService.UpdateAsync(_projectId, "feat", null, null, PlanningFeatureStatus.Review, default);
        var closed = await featureService.UpdateAsync(
            _projectId, "feat", null, null, PlanningFeatureStatus.Done, default);

        await artifactService.SetStatusAsync(task2.Id, PlanningArtifactStatus.Done, "late finish", default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.Done);
        persisted.Version.ShouldBe(closed.Version);
    }

    [Fact]
    public async Task SyncAsync_Throwing_MutationStillSucceeds()
    {
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(MaterializationException.IoFailure("/tmp/test", new System.IO.IOException("disk full")));

        var service = CreateFeatureService();
        var feature = await service.CreateAsync(_projectId, "sync-fail", "Title", "body");

        feature.Id.ShouldNotBe(Guid.Empty);

        var persisted = await _context.PlanningFeatures.FirstOrDefaultAsync(f => f.Slug == "sync-fail");
        persisted.ShouldNotBeNull();
    }

    [Fact]
    public async Task ClearAsync_Throwing_FeatureDeleteStillSucceeds()
    {
        _materializer.ClearAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(MaterializationException.IoFailure("/tmp/test", new System.IO.IOException("locked file")));

        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "clear-fail", "Title", "body");

        await service.DeleteAsync(_projectId, "clear-fail", default);

        var persisted = await _context.PlanningFeatures.FirstOrDefaultAsync(f => f.Slug == "clear-fail");
        persisted.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteArtifact_RemovesFromDb()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);

        await artifactService.DeleteAsync(artifact.Id, default);

        var exists = await _context.PlanningArtifacts.AnyAsync(a => a.Id == artifact.Id);
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task AppendLogAsync_UpdatesBodyButNotVersion()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec body", null, null, null, default);
        var versionBefore = artifact.Version;

        await artifactService.AppendLogAsync(artifact.Id, "a note", default);

        await _context.Entry(artifact).ReloadAsync();
        artifact.Version.ShouldBe(versionBefore);
        artifact.Body.ShouldContain("a note");
    }
}
