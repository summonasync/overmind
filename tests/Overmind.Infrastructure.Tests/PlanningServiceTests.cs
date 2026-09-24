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
    public async Task CreateFeature_ExplicitPriority_Persisted()
    {
        var service = CreateFeatureService();
        var created = await service.CreateAsync(_projectId, "urgent", "Title", "body", FeaturePriority.P0);
        await service.CreateAsync(_projectId, "normal", "Title", "body");

        var features = await _context.PlanningFeatures
            .Where(f => f.ProjectId == _projectId)
            .ToDictionaryAsync(f => f.Slug, f => f.Priority);

        created.Priority.ShouldBe(FeaturePriority.P0);
        features["urgent"].ShouldBe(FeaturePriority.P0);
        features["normal"].ShouldBe(FeaturePriority.P2);
    }

    [Fact]
    public async Task UpdateFeature_PriorityChange_AppliedAndPersisted()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Title", "body");

        var updated = await service.UpdateAsync(_projectId, "feat", null, null, null, FeaturePriority.P1);
        var reloaded = await _context.PlanningFeatures
            .SingleAsync(f => f.ProjectId == _projectId && f.Slug == "feat");

        updated.Priority.ShouldBe(FeaturePriority.P1);
        reloaded.Priority.ShouldBe(FeaturePriority.P1);
    }

    [Fact]
    public async Task UpdateFeature_NullPriority_LeavesItUntouched()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Title", "body", FeaturePriority.P3);

        var updated = await service.UpdateAsync(_projectId, "feat", null, null, null, null, default);

        updated.Priority.ShouldBe(FeaturePriority.P3);
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
        ex.Message.ShouldContain("logNote");
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
            () => artifactService.SetStatusAsync(
                spec.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "note", default));
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
            () => artifactService.SetStatusAsync(
                spec.Id, PlanningArtifactStatus.Draft, PlanningLogSource.Agent, "  ", default));
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
        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);

        var inReview = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        inReview.Status.ShouldBe(PlanningFeatureStatus.Review);

        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        persisted.Version.ShouldBe(1);
    }

    [Fact]
    public async Task UpsertArtifact_CreateTask_OnReviewFeature_ReopenCollapsesToSingleLineWithoutRepromotion()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var closedTasks = new List<PlanningArtifact>();
        for (var i = 1; i <= 4; i++)
        {
            closedTasks.Add(await artifactService.UpsertAsync(
                feature.Id, PlanningArtifactKind.Task, $"task {i}", null, null, null, default));
        }

        foreach (var task in closedTasks)
        {
            await artifactService.SetStatusAsync(
                task.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task closed", default);
        }

        var inReview = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        inReview.Status.ShouldBe(PlanningFeatureStatus.Review);
        PlanningLog.Parse(inReview.Body)
            .Count(l => l.Text == "all tasks done + spec + plan → review")
            .ShouldBe(1);

        var fifth = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 5", null, null, null, default);

        var reopened = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        reopened.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        var reopenLines = PlanningLog.Parse(reopened.Body);
        reopenLines.Count.ShouldBe(3);
        var reopenLine = reopenLines
            .Single(l => l.Text == "task created while in review — reopened for the new work");
        reopenLine.Source.ShouldBe(PlanningLogSource.Auto);
        reopenLines.Count(l => l.Text == "all tasks done + spec + plan → review").ShouldBe(1);

        await artifactService.SetStatusAsync(
            fifth.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 5 closed", default);

        var backInReview = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        backInReview.Status.ShouldBe(PlanningFeatureStatus.Review);
        var transitionTexts = PlanningLog.Parse(backInReview.Body).Select(l => l.Text).ToList();
        transitionTexts.Count.ShouldBe(4);
        transitionTexts[0].ShouldBe("artifact created → inProgress");
        transitionTexts[1].ShouldBe("all tasks done + spec + plan → review");
        transitionTexts[2].ShouldBe("task created while in review — reopened for the new work");
        transitionTexts[3].ShouldBe("all tasks done + spec + plan → review");
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
        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);

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
        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);
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

        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);

        var midFlight = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        midFlight.Status.ShouldBe(PlanningFeatureStatus.InProgress);

        await artifactService.SetStatusAsync(task2.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 2 done", default);

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

        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);
        await artifactService.SetStatusAsync(task2.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 2 done", default);

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

        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);
        await featureService.UpdateAsync(_projectId, "feat", null, null, PlanningFeatureStatus.Review, default);
        var closed = await featureService.UpdateAsync(
            _projectId, "feat", null, null, PlanningFeatureStatus.Done, default);

        await artifactService.SetStatusAsync(
            task2.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "late finish", default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.Done);
        persisted.Version.ShouldBe(closed.Version);
    }

    [Fact]
    public async Task UpdateFeature_StatusChange_WritesUiServiceLine()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Title", "body");

        var updated = await service.UpdateAsync(_projectId, "feat", null, null, PlanningFeatureStatus.Ready, default);

        var line = PlanningLog.Parse(updated.Body).Single();
        line.Source.ShouldBe(PlanningLogSource.Ui);
        line.Text.ShouldBe("status moved to 'ready' via ui");
    }

    [Fact]
    public async Task SetFeatureStatus_AgentDoor_KeepsUserNoteWithAgentMarker()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "feat", "Title", "body");

        var updated = await service.SetStatusAsync(
            _projectId, "feat", PlanningFeatureStatus.InProgress, "by user request in-session", default);

        var line = PlanningLog.Parse(updated.Body).Single();
        line.Source.ShouldBe(PlanningLogSource.Agent);
        line.Text.ShouldBe("by user request in-session");
    }

    [Fact]
    public async Task UpsertArtifact_CreateSpec_WritesAutoTransitionLine()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "# Spec", null, null, null, default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        var line = PlanningLog.Parse(persisted.Body).Single();
        line.Source.ShouldBe(PlanningLogSource.Auto);
        line.Text.ShouldBe("artifact created → inProgress");
    }

    [Fact]
    public async Task SetStatusAsync_LastTaskDone_WritesAutoReviewLine()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var task = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);

        await artifactService.SetStatusAsync(
            task.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        var line = PlanningLog.Parse(persisted.Body)
            .Single(l => l.Text == "all tasks done + spec + plan → review");
        line.Source.ShouldBe(PlanningLogSource.Auto);
    }

    [Fact]
    public async Task SetArtifactStatus_AgentDoor_KeepsNoteWithAgentMarker()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var task = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);

        await artifactService.SetStatusAsync(
            task.Id, PlanningArtifactStatus.InProgress, PlanningLogSource.Agent, "starting work", default);

        var persisted = await _context.PlanningArtifacts.FirstAsync(a => a.Id == task.Id);
        var line = PlanningLog.Parse(persisted.Body).Single();
        line.Source.ShouldBe(PlanningLogSource.Agent);
        line.Text.ShouldBe("starting work");
    }

    [Fact]
    public async Task SetArtifactStatus_UiDoor_KeepsNoteWithUiMarker()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var task = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);

        await artifactService.SetStatusAsync(
            task.Id, PlanningArtifactStatus.Done, PlanningLogSource.Ui, "closed from the board", default);

        var persisted = await _context.PlanningArtifacts.FirstAsync(a => a.Id == task.Id);
        var line = PlanningLog.Parse(persisted.Body).Single();
        line.Source.ShouldBe(PlanningLogSource.Ui);
        line.Text.ShouldBe("closed from the board");
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
    public async Task AppendLogAsync_SnapshotsBody_AndBumpsVersion()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec body", null, null, null, default);
        var versionBefore = artifact.Version;

        await artifactService.AppendLogAsync(artifact.Id, "a note", default);

        await _context.Entry(artifact).ReloadAsync();
        artifact.Version.ShouldBe(versionBefore + 1);
        artifact.Body.ShouldContain("a note");

        var snapshot = await _context.PlanningArtifactVersions.SingleAsync(v => v.ArtifactId == artifact.Id);
        snapshot.Version.ShouldBe(versionBefore);
        snapshot.Body.ShouldBe("spec body");
    }

    [Fact]
    public async Task PatchAsync_SectionPatch_PersistsSnapshot_AndSyncsMaterialization()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nold goal\n</goal>\n", null, null, null, default);

        _materializer.ClearReceivedCalls();

        var patched = await artifactService.PatchAsync(
            artifact.Id, new PlanningArtifactPatch(Section: "goal", Content: "patched goal"), default);

        patched.Version.ShouldBe(2);
        patched.Body.ShouldContain("patched goal");
        patched.Body.ShouldNotContain("old goal");

        await _context.Entry(artifact).ReloadAsync();
        artifact.Body.ShouldContain("patched goal");

        var snapshot = await _context.PlanningArtifactVersions.SingleAsync(v => v.ArtifactId == artifact.Id);
        snapshot.Version.ShouldBe(1);
        snapshot.Body.ShouldContain("<goal>\nold goal\n</goal>");

        await _materializer.Received(1).SyncAsync(_projectId, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PatchAsync_TickPatch_FlipsItem_AndBumpsVersion()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id,
            PlanningArtifactKind.Task,
            "<acceptance-criteria>\n- [ ] write the parser\n</acceptance-criteria>\n",
            null, null, null, default);

        var patched = await artifactService.PatchAsync(
            artifact.Id, new PlanningArtifactPatch(TickIndex: 0), default);

        patched.Version.ShouldBe(2);
        patched.Body.ShouldContain("[x] write the parser");

        var persisted = await _context.PlanningArtifactVersions.SingleAsync(v => v.ArtifactId == artifact.Id);
        persisted.Body.ShouldContain("[ ] write the parser");
    }

    [Fact]
    public async Task PatchAsync_UnknownSection_ThrowsPatchSectionNotFound()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nold\n</goal>\n", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.PatchAsync(
                artifact.Id, new PlanningArtifactPatch(Section: "refs", Content: "x"), default));

        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Code.ShouldBe(PlanningFailureCode.PatchSectionNotFound);
        ex.Message.ShouldContain("goal");
    }

    [Fact]
    public async Task PatchAsync_FrozenArtifact_PatchWithoutNote_ThrowsChangeNoteRequired()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nold\n</goal>\n", null, null, null, default);
        await artifactService.ValidateAsync(artifact.Id, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.PatchAsync(
                artifact.Id, new PlanningArtifactPatch(Section: "goal", Content: "new"), default));

        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Code.ShouldBe(PlanningFailureCode.ChangeNoteRequired);
    }

    [Fact]
    public async Task PatchAsync_FrozenArtifact_PatchWithNote_DemotesToDraft_AndSnapshotsFrozenBody()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nold\n</goal>\n", null, null, null, default);
        await artifactService.ValidateAsync(artifact.Id, default);

        var patched = await artifactService.PatchAsync(
            artifact.Id,
            new PlanningArtifactPatch(Section: "goal", Content: "new", LogNote: "reopened for rework"),
            default);

        patched.Status.ShouldBe(PlanningArtifactStatus.Draft);
        patched.Version.ShouldBe(2);

        await _context.Entry(artifact).ReloadAsync();
        artifact.Status.ShouldBe(PlanningArtifactStatus.Draft);
        artifact.Body.ShouldContain("reopened for rework");

        var snapshot = await _context.PlanningArtifactVersions.SingleAsync(v => v.ArtifactId == artifact.Id);
        snapshot.Version.ShouldBe(1);
    }

    [Fact]
    public async Task PatchAsync_IdempotentRetick_DoesNotBumpVersionOrSnapshot()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id,
            PlanningArtifactKind.Task,
            "<acceptance-criteria>\n- [x] already done\n</acceptance-criteria>\n",
            null, null, null, default);

        var patched = await artifactService.PatchAsync(
            artifact.Id, new PlanningArtifactPatch(TickIndex: 0), default);

        patched.Version.ShouldBe(1);

        var snapshots = await _context.PlanningArtifactVersions.CountAsync(v => v.ArtifactId == artifact.Id);
        snapshots.ShouldBe(0);
    }

    [Fact]
    public async Task PatchAsync_MissingArtifact_ThrowsMissingArtifact()
    {
        var artifactService = CreateArtifactService();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.PatchAsync(
                Guid.NewGuid(), new PlanningArtifactPatch(Section: "goal", Content: "x"), default));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
        ex.Code.ShouldBe(PlanningFailureCode.MissingArtifact);
    }

    [Fact]
    public async Task PatchAsync_SectionAndTickTogether_ThrowsInvalidPatchRequest()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nold\n</goal>\n", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.PatchAsync(
                artifact.Id,
                new PlanningArtifactPatch(Section: "goal", Content: "new", TickIndex: 0),
                default));

        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Code.ShouldBe(PlanningFailureCode.InvalidPatchRequest);
    }

    [Fact]
    public async Task PatchAsync_EmptyPatch_ThrowsInvalidPatchRequest()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nold\n</goal>\n", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.PatchAsync(artifact.Id, new PlanningArtifactPatch(), default));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidPatchRequest);
    }

    [Fact]
    public async Task PatchAsync_SectionWithoutContent_ThrowsInvalidPatchRequest()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nold\n</goal>\n", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.PatchAsync(
                artifact.Id, new PlanningArtifactPatch(Section: "goal"), default));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidPatchRequest);
    }
}
