using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

public sealed class PlanningServiceTests : IDisposable, IAsyncLifetime
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

    private PlanningFeatureService CreateFeatureService() =>
        new(
            _context,
            _materializer,
            new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
            new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)),
            TestPlanningDispatcher.Create(_context, _materializer),
            NullLogger<PlanningFeatureService>.Instance);

    private PlanningArtifactService CreateArtifactService() =>
        new(
            _context,
            TestPlanningDispatcher.Create(_context, _materializer),
            new TaskDependencyService(
                _context, TestPlanningDispatcher.Create(_context, _materializer)));

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
    public async Task CreateFeature_CollidingNumber_ThrowsNumberCollisionNamingTheHolder()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "075-sprint", "Sprint", "body");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.CreateAsync(_projectId, "75-other", "Other", "body"));
        ex.Kind.ShouldBe(PlanningFailureKind.Duplicate);
        ex.Code.ShouldBe(PlanningFailureCode.NumberCollision);
        ex.Message.ShouldContain("075-sprint");
        ex.Message.ShouldContain("status 'draft'");
        ex.Message.ShouldContain("076");
    }

    [Fact]
    public async Task CreateFeature_PaddedRequestAgainstUnpaddedExisting_ThrowsNumberCollision()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "7-pinned", "Pinned", "body");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.CreateAsync(_projectId, "007-retry", "Retry", "body"));
        ex.Code.ShouldBe(PlanningFailureCode.NumberCollision);
        ex.Message.ShouldContain("7-pinned");
    }

    [Fact]
    public async Task CreateFeature_DuplicateSlug_PrecedesTheNumberCheck()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "007-x", "X", "body");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.CreateAsync(_projectId, "007-x", "X again", "body"));
        ex.Code.ShouldBe(PlanningFailureCode.DuplicateSlug);
    }

    [Fact]
    public async Task CreateFeature_NumberlessSlug_AmongNumberedOnes_Succeeds()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "007-x", "X", "body");

        var created = await service.CreateAsync(_projectId, "notes", "Notes", "body");
        created.Slug.ShouldBe("notes");
    }

    [Fact]
    public async Task CreateFeature_NextFreeNumber_Succeeds()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "075-a", "A", "body");

        var created = await service.CreateAsync(_projectId, "76-b", "B", "body");
        created.Slug.ShouldBe("76-b");
    }

    [Fact]
    public async Task ImportFeature_CollidingNumber_ImportsVerbatimAndJoinsTheGuard()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "10-alpha", "Alpha", "body");

        var result = await service.ImportAsync(
            _projectId,
            new BacklogFeatureImport(
                "010-beta",
                "Beta",
                "<goal>beta</goal>",
                PlanningFeatureStatus.Draft,
                FeaturePriority.P2,
                1,
                DateTime.UtcNow,
                [],
                [],
                []),
            CancellationToken.None);

        result.Created.ShouldBeTrue();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.CreateAsync(_projectId, "10-gamma", "Gamma", "body"));
        ex.Code.ShouldBe(PlanningFailureCode.NumberCollision);
        ex.Message.ShouldContain("010-beta");
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

        var updated = await service.UpdateAsync(
            _projectId, "feat", null, null, status: null, priority: null, ct: default);

        updated.Priority.ShouldBe(FeaturePriority.P3);
    }

    [Fact]
    public async Task CreateFeature_LandsAtPriorityBandTail()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "second", "Title", "body", FeaturePriority.P2);
        await service.CreateAsync(_projectId, "first", "Title", "body", FeaturePriority.P2);
        await service.CreateAsync(_projectId, "urgent", "Title", "body", FeaturePriority.P0);

        var positions = await _context.PlanningFeatures
            .Where(f => f.ProjectId == _projectId)
            .ToDictionaryAsync(f => f.Slug, f => f.QueuePosition);

        positions["second"].ShouldBe(1);
        positions["first"].ShouldBe(2);
        positions["urgent"].ShouldBe(1);
    }

    [Fact]
    public async Task UpdateFeature_PriorityChange_RetailsIntoNewBand()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "one", "Title", "body", FeaturePriority.P0);
        await service.CreateAsync(_projectId, "two", "Title", "body", FeaturePriority.P0);
        await service.CreateAsync(_projectId, "three", "Title", "body", FeaturePriority.P2);

        await service.UpdateAsync(_projectId, "two", null, null, null, FeaturePriority.P2, default);

        var positions = await _context.PlanningFeatures
            .Where(f => f.ProjectId == _projectId)
            .ToDictionaryAsync(f => f.Slug, f => f.QueuePosition);
        positions["one"].ShouldBe(1);
        positions["two"].ShouldBe(2);
        positions["three"].ShouldBe(1);
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
        versions[0].Body.ShouldBe(PlanningTaskSections.WithSkeleton("v1"));

        var log = PlanningLog.Parse(updated.Body);
        log.Count.ShouldBe(1);
        log[0].Text.ShouldBe("body edit");
    }

    [Fact]
    public async Task UpsertArtifact_StaleBodyDroppingStoredLogLine_ThrowsLogHistoryNotAppendable()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec,
            "# Spec\n\n<goal>\noriginal\n</goal>\n\n<log>\n- 2026-09-01 — seeded\n</log>\n",
            null, null, null, default);
        await artifactService.AppendLogAsync(spec.Id, "progress that must survive", default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.UpsertAsync(
                feature.Id, PlanningArtifactKind.Spec,
                "# Spec\n\n<goal>\noriginal\n</goal>\n\n<log>\n- 2026-09-01 — seeded\n</log>\n",
                null, null, null, default));

        ex.Code.ShouldBe(PlanningFailureCode.LogHistoryNotAppendable);
        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Message.ShouldContain("planning_append_log");

        await _context.Entry(spec).ReloadAsync();
        PlanningLog.Parse(spec.Body).Count(l => l.Text == "progress that must survive").ShouldBe(1);
    }

    [Fact]
    public async Task UpsertArtifact_FullResendWithStoredLogAndTailLine_Succeeds()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec,
            "# Spec\n\n<log>\n- 2026-09-01 — seeded\n</log>\n", null, null, null, default);
        await artifactService.AppendLogAsync(spec.Id, "progress that must survive", default);
        await _context.Entry(spec).ReloadAsync();

        var resent = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec,
            PlanningLog.Append(spec.Body, "tail line added by the resend"),
            null, null, null, default);

        PlanningLog.Parse(resent.Body).Select(l => l.Text).ToList()
            .ShouldBe(["seeded", "progress that must survive", "tail line added by the resend"]);
        resent.Version.ShouldBe(3);
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
    public async Task UpsertArtifact_TaskCreate_MaterializesTheDeclaredSkeleton()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var task = await CreateArtifactService().UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "# Task\n\n<goal>\nWire it.\n</goal>\n", null, null, null, default);

        PlanningSections.List(task.Body).ShouldBe(PlanningTaskSections.Declared);
        PlanningSections.Find("goal", task.Body)!.Content.ShouldBe("Wire it.");
        foreach (var section in PlanningTaskSections.Declared.Where(s => s != "goal"))
        {
            PlanningSections.Find(section, task.Body)!.Content.ShouldBe(PlanningSections.EmptyMarker);
        }
    }

    [Fact]
    public async Task UpsertArtifact_SpecCreate_LeavesTheBodyVerbatim()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var spec = await CreateArtifactService().UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nold\n</goal>\n", null, null, null, default);

        spec.Body.ShouldBe("# Spec\n\n<goal>\nold\n</goal>\n");
    }

    [Fact]
    public async Task UpsertArtifact_ConcatenatedTaskSections_RegisterAndPatch()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var task = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task,
            "<goal>Fix it.</goal><scope>Canonicalizer.</scope>"
            + "<acceptance-criteria>- [x] sections parse</acceptance-criteria><log></log>",
            0, null, null, default);

        PlanningSections.List(task.Body).ShouldBe(new[]
        {
            "goal", "scope", "acceptance-criteria", "log", "anti-scope", "constraints", "refs", "obstacles"
        });

        var patched = await artifactService.PatchAsync(
            task.Id,
            new PlanningArtifactPatch(Section: "scope", Content: "Expanded.", LogNote: "section patch"),
            default);

        PlanningSections.Find("scope", patched.Artifact.Body)!.Content.ShouldBe("Expanded.");
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
    public async Task DeclineAsync_WithReason_AppendsTheUiMarkedLine()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var versionBeforeDecline = spec.Version;

        var declined = await artifactService.DeclineAsync(spec.Id, "  shaky ground  ", default);

        declined.Status.ShouldBe(PlanningArtifactStatus.Draft);
        declined.Version.ShouldBe(versionBeforeDecline + 1);
        declined.Body.ShouldContain("[ui] — declined by human: shaky ground");
    }

    [Fact]
    public async Task DeclineAsync_BlankReason_AppendsTheBareLine()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);

        var declined = await artifactService.DeclineAsync(spec.Id, "   ", default);

        declined.Body.ShouldContain("[ui] — declined by human\n");
        declined.Body.ShouldNotContain("declined by human:");
    }

    [Fact]
    public async Task DeclineAsync_Task_ThrowsCannotDeclineTask()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var task = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.DeclineAsync(task.Id, "nope", default));
        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Code.ShouldBe(PlanningFailureCode.CannotDeclineTask);
    }

    [Fact]
    public async Task DeclineAsync_FrozenSpec_LogsWithoutDemotion()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);

        var declined = await artifactService.DeclineAsync(spec.Id, "wave veto", default);

        declined.Status.ShouldBe(PlanningArtifactStatus.Validated);
        declined.FrozenAt.ShouldNotBeNull();
        declined.Body.ShouldContain("[ui] — declined by human: wave veto");
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
    public async Task UpsertArtifact_CreateSpec_OnDraftFeature_MakesFeatureReady()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "# Spec", null, null, null, default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.Ready);
        persisted.Version.ShouldBe(1);
    }

    [Fact]
    public async Task UpsertArtifact_CreatePlan_OnReadyFeature_StaysReady()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");
        await featureService.UpdateAsync(_projectId, "feat", null, null, PlanningFeatureStatus.Ready, default);

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "# Plan", null, null, null, default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.Ready);
        PlanningLog.Parse(persisted.Body)
            .ShouldNotContain(l => l.Text.StartsWith("artifact created"));
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
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var plan = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
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
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var plan = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
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
            .Count(l => l.Text == "all tasks done + spec + plan validated → review")
            .ShouldBe(1);

        var fifth = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 5", null, null, null, default);

        var reopened = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        reopened.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        var reopenLines = PlanningLog.Parse(reopened.Body);
        reopenLines.Count.ShouldBe(4);
        var reopenLine = reopenLines
            .Single(l => l.Text == "task created while in review — reopened for the new work");
        reopenLine.Source.ShouldBe(PlanningLogSource.Auto);
        reopenLines.Count(l => l.Text == "all tasks done + spec + plan validated → review").ShouldBe(1);
        reopenLines.Count(l => l.Text == "task 00 taken → inProgress").ShouldBe(1);

        await artifactService.SetStatusAsync(
            fifth.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 5 closed", default);

        var backInReview = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        backInReview.Status.ShouldBe(PlanningFeatureStatus.Review);
        var transitionTexts = PlanningLog.Parse(backInReview.Body).Select(l => l.Text).ToList();
        transitionTexts.Count.ShouldBe(5);
        transitionTexts[0].ShouldBe("artifact created → ready");
        transitionTexts[1].ShouldBe("task 00 taken → inProgress");
        transitionTexts[2].ShouldBe("all tasks done + spec + plan validated → review");
        transitionTexts[3].ShouldBe("task created while in review — reopened for the new work");
        transitionTexts[4].ShouldBe("all tasks done + spec + plan validated → review");
    }

    [Fact]
    public async Task UpsertArtifact_CreateResult_OnReviewFeature_StaysReview()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var plan = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
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
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var plan = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Result, "close-out", null, null, null, default);
        var closed = await featureService.UpdateAsync(
            _projectId, "feat", null, null, PlanningFeatureStatus.Done, default);

        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.Done);
        persisted.Version.ShouldBe(closed.Version);
    }

    [Fact]
    public async Task UpsertTasks_FiveTasksInOneCall_OneTransitionLineAndOneMaterializationSync()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        _materializer.ClearReceivedCalls();

        var batch = await artifactService.UpsertTasksAsync(
            feature.Id,
            Enumerable.Range(1, 5).Select(i => new PlanningTaskUpsert($"task {i}")).ToArray(),
            default);

        batch.Created.ShouldBe(5);
        batch.Updated.ShouldBe(0);
        batch.Artifacts.Count.ShouldBe(5);
        batch.Artifacts.Select(a => a.Order).ToArray().ShouldBe([0, 1, 2, 3, 4]);
        batch.Artifacts.ShouldAllBe(a => a.Status == PlanningArtifactStatus.Todo);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.Ready);
        PlanningLog.Parse(persisted.Body)
            .Count(l => l.Text == "artifact created → ready")
            .ShouldBe(1);

        await _materializer.Received(1).SyncAsync(_projectId, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertTasks_CreateFromReview_SingleReopenLineNotOnePerTask()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var plan = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
        var first = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        await artifactService.SetStatusAsync(
            first.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);

        var inReview = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        inReview.Status.ShouldBe(PlanningFeatureStatus.Review);

        await artifactService.UpsertTasksAsync(
            feature.Id,
            [new PlanningTaskUpsert("task 2"), new PlanningTaskUpsert("task 3")],
            default);

        var reopened = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        reopened.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        PlanningLog.Parse(reopened.Body)
            .Count(l => l.Text == "task created while in review — reopened for the new work")
            .ShouldBe(1);
    }

    [Fact]
    public async Task UpsertTasks_MixedCreateAndUpdate_CountsAndUpdatesInPlace()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task 1 v1", 0, null, null, default);

        var batch = await artifactService.UpsertTasksAsync(
            feature.Id,
            [
                new PlanningTaskUpsert("task 1 v2", Order: 0, LogNote: "rewritten from the plan"),
                new PlanningTaskUpsert("task 2", Review: PlanningReviewLevel.Hard, Brief: "Wire the hook")
            ],
            default);

        batch.Created.ShouldBe(1);
        batch.Updated.ShouldBe(1);

        var updated = batch.Artifacts[0];
        updated.Version.ShouldBe(2);
        updated.Body.ShouldContain("task 1 v2");
        PlanningLog.Parse(updated.Body).Single().Text.ShouldBe("rewritten from the plan");

        var created = batch.Artifacts[1];
        created.Order.ShouldBe(1);
        created.Review.ShouldBe(PlanningReviewLevel.Hard);
        created.Brief.ShouldBe("Wire the hook");

        var persistedTasks = await _context.PlanningArtifacts
            .Where(a => a.FeatureId == feature.Id && a.Kind == PlanningArtifactKind.Task)
            .ToListAsync();
        persistedTasks.Count.ShouldBe(2);
    }

    [Fact]
    public async Task UpsertTasks_DuplicateExplicitOrderWithinBatch_MergesIntoOneArtifact()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();

        var batch = await artifactService.UpsertTasksAsync(
            feature.Id,
            [new PlanningTaskUpsert("first take", Order: 0), new PlanningTaskUpsert("second take", Order: 0, LogNote: "merged")],
            default);

        batch.Created.ShouldBe(1);
        batch.Updated.ShouldBe(1);
        var count = await _context.PlanningArtifacts
            .CountAsync(a => a.FeatureId == feature.Id && a.Kind == PlanningArtifactKind.Task);
        count.ShouldBe(1);
        batch.Artifacts[0].Body.ShouldContain("second take");
    }

    [Fact]
    public async Task UpsertTasks_MissingFeature_ThrowsNotFound()
    {
        var artifactService = CreateArtifactService();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.UpsertTasksAsync(Guid.NewGuid(), [new PlanningTaskUpsert("task")], default));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
    }

    [Fact]
    public async Task UpsertTasks_BlockedByOnItems_CreatesEdgesInOneSave()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");
        var artifactService = CreateArtifactService();

        var batch = await artifactService.UpsertTasksAsync(
            feature.Id,
            [
                new PlanningTaskUpsert("task zero"),
                new PlanningTaskUpsert("task one", BlockedBy: [0]),
                new PlanningTaskUpsert("task two", BlockedBy: [0, 1])
            ],
            default);

        batch.Created.ShouldBe(3);
        _context.ChangeTracker.Clear();

        var states = await CreateDependencyReader().BlockedOrdersAsync(feature.Id);
        states.Select(s => s.Blocked).ShouldBe([false, true, true]);
        states[2].BlockedBy.ShouldBe([0, 1]);
        (await _context.PlanningTaskDependencies.CountAsync()).ShouldBe(3);
    }

    [Fact]
    public async Task UpsertTasks_ForwardReferenceWithinBatch_ResolvesAgainstLaterItems()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");
        var artifactService = CreateArtifactService();

        await artifactService.UpsertTasksAsync(
            feature.Id,
            [
                new PlanningTaskUpsert("task zero", Order: 0, BlockedBy: [1]),
                new PlanningTaskUpsert("task one", Order: 1)
            ],
            default);

        _context.ChangeTracker.Clear();
        var states = await CreateDependencyReader().BlockedOrdersAsync(feature.Id);
        states[0].Blocked.ShouldBeTrue();
        states[0].BlockedBy.ShouldBe([1]);
    }

    [Fact]
    public async Task UpsertTasks_UnknownBlockerOrder_FailsTheItemAndPersistsNothing()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");
        var artifactService = CreateArtifactService();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.UpsertTasksAsync(
                feature.Id,
                [
                    new PlanningTaskUpsert("task zero", BlockedBy: [1]),
                    new PlanningTaskUpsert("task one", BlockedBy: [7])
                ],
                default));

        ex.Message.ShouldStartWith("tasks[1]:");
        ex.Message.ShouldContain("blockedBy order 7");
        _context.ChangeTracker.Clear();
        (await _context.PlanningArtifacts.CountAsync(a => a.FeatureId == feature.Id)).ShouldBe(0);
        _context.PlanningTaskDependencies.ShouldBeEmpty();
    }

    private TaskDependencyService CreateDependencyReader() =>
        new(_context, TestPlanningDispatcher.Create(_context, _materializer));

    [Fact]
    public async Task UpsertTasks_RewrittenStoredLogLine_ThrowsWrappedAsBatchItemAndRollsBack()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var task = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task,
            "# Task one\n\n<log>\n- 2026-09-01 — seeded\n</log>\n", 0, null, null, default);
        await artifactService.AppendLogAsync(task.Id, "progress that must survive", default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.UpsertTasksAsync(
                feature.Id,
                [
                    new PlanningTaskUpsert("# Task one\n\n<log>\n- 2026-09-01 — seeded\n</log>\n", Order: 0),
                    new PlanningTaskUpsert("# Task two\n", Order: 1)
                ],
                default));

        ex.Code.ShouldBe(PlanningFailureCode.LogHistoryNotAppendable);
        ex.Message.ShouldContain("tasks[0]:");
        ex.Message.ShouldContain("planning_append_log");

        var persisted = await _context.PlanningArtifacts
            .CountAsync(a => a.FeatureId == feature.Id && a.Kind == PlanningArtifactKind.Task);
        persisted.ShouldBe(1);
    }

    [Fact]
    public async Task UpsertTasks_ConcurrentWriterClaimsAnOrder_RollsBackWholeBatchWithRetryHint()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var armed = false;
        using var racing = _db.CreateContext();
        var interceptor = new CompetingWriterInterceptor(
            () => armed,
            () =>
            {
                racing.PlanningArtifacts.Add(PlanningArtifact.Create(
                    feature.Id, PlanningArtifactKind.Task, "claimed by the racing writer", 0,
                    PlanningReviewLevel.Light, null));
                racing.SaveChanges();
            });
        using var context = _db.CreateContext(interceptor);
        var artifactService = new PlanningArtifactService(
            context,
            TestPlanningDispatcher.Create(context, _materializer),
            new TaskDependencyService(context, TestPlanningDispatcher.Create(context, _materializer)));

        _materializer.ClearReceivedCalls();
        armed = true;
        var ex = await Should.ThrowAsync<PlanningException>(() => artifactService.UpsertTasksAsync(
            feature.Id,
            [new PlanningTaskUpsert("task A", Order: 0), new PlanningTaskUpsert("task B", Order: 1)],
            default));
        armed = false;

        ex.Kind.ShouldBe(PlanningFailureKind.Duplicate);
        ex.Message.ShouldContain("retry the call");

        using var verified = _db.CreateContext();
        var persisted = await verified.PlanningArtifacts
            .Where(a => a.FeatureId == feature.Id && a.Kind == PlanningArtifactKind.Task)
            .ToListAsync();
        persisted.Count.ShouldBe(1);
        persisted[0].Order.ShouldBe(0);
        persisted[0].Body.ShouldContain("claimed by the racing writer");

        var persistedFeature = await verified.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persistedFeature.Status.ShouldBe(PlanningFeatureStatus.Draft);
        PlanningLog.Parse(persistedFeature.Body)
            .ShouldNotContain(l => l.Text == "artifact created → ready");

        await _materializer.DidNotReceive().SyncAsync(
            Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    private sealed class CompetingWriterInterceptor(Func<bool> armed, Action compete) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (armed())
            {
                compete();
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task SetStatusAsync_LastTaskDone_WithValidatedSpecAndPlan_PromotesToReview()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var plan = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        var task2 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);

        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);

        var midFlight = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        midFlight.Status.ShouldBe(PlanningFeatureStatus.InProgress);

        await artifactService.SetStatusAsync(task2.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 2 done", default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.Review);
        persisted.Version.ShouldBe(1);
    }

    [Fact]
    public async Task SetStatusAsync_LastTaskDone_MissingPlan_NoReviewPromotion()
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
        persisted.Status.ShouldNotBe(PlanningFeatureStatus.Review);
    }

    [Fact]
    public async Task SetStatusAsync_LastTaskDone_DraftSpecAndPlan_NoPromotion()
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
        await artifactService.SetStatusAsync(task2.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 2 done", default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        PlanningLog.Parse(persisted.Body).ShouldNotContain(l => l.Source == PlanningLogSource.Auto && l.Text.Contains("review"));
    }

    [Fact]
    public async Task SetStatusAsync_ManualValidatedToDraft_OnSpecOfReviewFeature_DemotesFeatureToInProgress()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var plan = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        var task2 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);
        await artifactService.SetStatusAsync(task2.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 2 done", default);

        var inReview = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        inReview.Status.ShouldBe(PlanningFeatureStatus.Review);

        var result = await artifactService.SetStatusAsync(
            spec.Id, PlanningArtifactStatus.Draft, PlanningLogSource.Agent, "review outcome: spec rejected", default);

        await _context.Entry(inReview).ReloadAsync();
        inReview.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        PlanningLog.Parse(inReview.Body).Last().Text
            .ShouldBe("feature demoted: review → inProgress (spec no longer validated)");
        result.FeatureStatusChanged.ShouldBe(PlanningArtifact.FeatureDemotedStatusEcho);
        result.Warnings.ShouldBeNull();
    }

    [Fact]
    public async Task SetStatusAsync_AllAcTickedAndCheckboxInLogNote_NoWarnings()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var task = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task,
            "# Task\n\n<acceptance-criteria>\n- [x] endpoint wired\n- [x] docs written\n</acceptance-criteria>\n",
            null, null, null, default);

        var result = await artifactService.SetStatusAsync(
            task.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent,
            "closing with leftovers: - [ ] docs", default);

        result.Warnings.ShouldBeNull();
    }

    [Fact]
    public async Task SetStatusAsync_MarkerlessAcLine_ExcludedFromWarningDenominator()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var task = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task,
            "# Task\n\n<acceptance-criteria>\n- [ ] docs written\n- follow-up note\n</acceptance-criteria>\n",
            null, null, null, default);

        var result = await artifactService.SetStatusAsync(
            task.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task done", default);

        result.Warnings.ShouldBe(["1 of 1 acceptance items unticked"]);
    }

    [Fact]
    public async Task SetStatusAsync_ManualValidatedToDraft_OnSpecOfDoneFeature_FeatureStaysDone()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var plan = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        var task2 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);
        await artifactService.SetStatusAsync(task2.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 2 done", default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Result, "close-out", null, null, null, default);
        await featureService.SetStatusAsync(
            _projectId, "feat", PlanningFeatureStatus.Done, "closing the feature", ct: default);

        var result = await artifactService.SetStatusAsync(
            spec.Id, PlanningArtifactStatus.Draft, PlanningLogSource.Agent, "late reopen", default);

        var done = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        done.Status.ShouldBe(PlanningFeatureStatus.Done);
        result.FeatureStatusChanged.ShouldBeNull();
    }

    [Fact]
    public async Task SetStatusAsync_ManualValidatedToDraft_OnSpecOfReadyFeature_FeatureUntouched()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);

        var ready = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        var logLinesBefore = PlanningLog.Parse(ready.Body).Count;

        var result = await artifactService.SetStatusAsync(
            spec.Id, PlanningArtifactStatus.Draft, PlanningLogSource.Agent, "reopening the spec", default);

        await _context.Entry(ready).ReloadAsync();
        ready.Status.ShouldBe(PlanningFeatureStatus.Ready);
        PlanningLog.Parse(ready.Body).Count.ShouldBe(logLinesBefore);
        result.FeatureStatusChanged.ShouldBeNull();
    }

    [Fact]
    public async Task SetStatusAsync_ValidatedToValidated_NoFeatureConsequence()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var plan = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        var task2 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);
        await artifactService.SetStatusAsync(task2.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 2 done", default);

        var inReview = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        inReview.Status.ShouldBe(PlanningFeatureStatus.Review);

        var result = await artifactService.SetStatusAsync(
            spec.Id, PlanningArtifactStatus.Validated, PlanningLogSource.Agent, "re-confirming the spec", default);

        await _context.Entry(inReview).ReloadAsync();
        inReview.Status.ShouldBe(PlanningFeatureStatus.Review);
        result.FeatureStatusChanged.ShouldBeNull();
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
        await artifactService.SetStatusAsync(
            task2.Id, PlanningArtifactStatus.InProgress, PlanningLogSource.Agent, "starting task 2", default);
        await featureService.UpdateAsync(_projectId, "feat", null, null, PlanningFeatureStatus.Review, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Result, "close-out", null, null, null, default);
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
        line.Text.ShouldBe("artifact created → ready");
    }

    [Fact]
    public async Task SetStatusAsync_LastTaskDone_WritesAutoReviewLine()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var plan = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var task = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);

        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
        await artifactService.SetStatusAsync(
            task.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        var line = PlanningLog.Parse(persisted.Body)
            .Single(l => l.Text == "all tasks done + spec + plan validated → review");
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
    public async Task SetStatusAsync_TaskTaken_OnReadyFeature_PromotesFeatureToInProgress()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var task = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);

        var beforeTake = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        beforeTake.Status.ShouldBe(PlanningFeatureStatus.Ready);

        await artifactService.SetStatusAsync(
            task.Id, PlanningArtifactStatus.InProgress, PlanningLogSource.Agent, "starting work", default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        persisted.Version.ShouldBe(1);
        var line = PlanningLog.Parse(persisted.Body)
            .Single(l => l.Text == "task 00 taken → inProgress");
        line.Source.ShouldBe(PlanningLogSource.Auto);
    }

    [Fact]
    public async Task SetStatusAsync_TaskTaken_OnDraftFeature_PromotesFeatureToInProgress()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat-draft-take", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        var task = _context.PlanningArtifacts
            .Single(a => a.FeatureId == feature.Id && a.Kind == PlanningArtifactKind.Task);

        var imported = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        imported.SetStatus(
            PlanningFeatureStatus.Draft,
            "status restored from imported file",
            PlanningLogSource.Import);
        await _context.SaveChangesAsync(default);

        await artifactService.SetStatusAsync(
            task.Id, PlanningArtifactStatus.InProgress, PlanningLogSource.Agent, "starting work", default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        PlanningLog.Parse(persisted.Body)
            .ShouldContain(l => l.Text == "task 00 taken → inProgress" && l.Source == PlanningLogSource.Auto);
    }

    [Fact]
    public async Task SetStatusAsync_TaskTaken_OnInProgressFeature_NoSecondPromotionLine()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var first = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        var second = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);

        await artifactService.SetStatusAsync(
            first.Id, PlanningArtifactStatus.InProgress, PlanningLogSource.Agent, "starting work", default);
        await artifactService.SetStatusAsync(
            second.Id, PlanningArtifactStatus.InProgress, PlanningLogSource.Ui, "picking up task 2", default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        PlanningLog.Parse(persisted.Body)
            .Count(l => l.Text == "task 00 taken → inProgress" || l.Text == "task 01 taken → inProgress")
            .ShouldBe(1);
    }

    [Fact]
    public async Task SetStatusAsync_TaskTaken_OnReviewFeature_FeatureStaysInReview()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var plan = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);

        var inReview = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        inReview.Status.ShouldBe(PlanningFeatureStatus.Review);

        await artifactService.SetStatusAsync(
            task1.Id, PlanningArtifactStatus.InProgress, PlanningLogSource.Agent, "reopening task 1", default);

        var persisted = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.Review);
        PlanningLog.Parse(persisted.Body)
            .ShouldNotContain(l => l.Text.EndsWith("taken → inProgress"));
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
    public async Task DeleteArtifact_SoftDeletesAndKeepsRowAuditable()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec v1", null, null, null, default);
        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec v2", null, null, "rework", default);

        await artifactService.DeleteAsync(artifact.Id, byHuman: true, default);

        var visible = await _context.PlanningArtifacts.AnyAsync(a => a.Id == artifact.Id);
        visible.ShouldBeFalse();

        var row = await _context.PlanningArtifacts
            .IgnoreQueryFilters()
            .SingleAsync(a => a.Id == artifact.Id);
        row.DeletedAt.ShouldNotBeNull();
        row.DeletedBy.ShouldBe(PlanningArtifact.HumanDeletionMarker);
        row.Body.ShouldContain("spec v2");

        (await _context.PlanningArtifactVersions.CountAsync(v => v.ArtifactId == artifact.Id))
            .ShouldBe(1);
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
    public async Task AppendLogAsync_WithSource_StampedByTheDoor()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec body", null, null, null, default);

        await artifactService.AppendLogAsync(artifact.Id, "mid-task progress", source: PlanningLogSource.Agent);

        await _context.Entry(artifact).ReloadAsync();
        var line = PlanningLog.Parse(artifact.Body).ShouldHaveSingleItem();
        line.Text.ShouldBe("mid-task progress");
        line.Source.ShouldBe(PlanningLogSource.Agent);
    }

    [Fact]
    public async Task PatchAsync_LogNote_StampedByTheDoor()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nold\n</goal>\n", null, null, null, default);

        await artifactService.PatchAsync(
            artifact.Id,
            new PlanningArtifactPatch(Section: "goal", Content: "new", LogNote: "reopened for rework"),
            source: PlanningLogSource.Agent);

        var line = PlanningLog.Parse(artifact.Body).ShouldHaveSingleItem();
        line.Text.ShouldBe("reopened for rework");
        line.Source.ShouldBe(PlanningLogSource.Agent);
    }

    [Fact]
    public async Task UpsertAsync_LogNote_StampedByTheDoor()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "v1", null, null, null, default);

        var updated = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "v2", null, null, "unfreezing", default,
            source: PlanningLogSource.Ui);

        var line = PlanningLog.Parse(updated.Body).ShouldHaveSingleItem();
        line.Text.ShouldBe("unfreezing");
        line.Source.ShouldBe(PlanningLogSource.Ui);
    }

    [Fact]
    public async Task PatchAsync_DemotingValidatedSpec_OnReviewFeature_DemotesFeatureToInProgress()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\noriginal\n</goal>\n",
            null, null, null, default);
        var plan = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        var task2 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);
        await artifactService.SetStatusAsync(task2.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 2 done", default);

        var inReview = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        inReview.Status.ShouldBe(PlanningFeatureStatus.Review);

        var patched = await artifactService.PatchAsync(
            spec.Id,
            new PlanningArtifactPatch(Section: "goal", Content: "new", LogNote: "reopened for rework"),
            source: PlanningLogSource.Agent);

        await _context.Entry(inReview).ReloadAsync();
        inReview.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        var demoteLine = PlanningLog.Parse(inReview.Body).Last();
        demoteLine.Source.ShouldBe(PlanningLogSource.Auto);
        demoteLine.Text.ShouldBe("feature demoted: review → inProgress (spec no longer validated)");
        patched.Artifact.StatusChanged.ShouldBe(PlanningArtifact.DemotedStatusEcho);
        patched.Artifact.FeatureStatusChanged.ShouldBe(PlanningArtifact.FeatureDemotedStatusEcho);
    }

    [Fact]
    public async Task PatchAsync_DemotingValidatedPlan_OnReviewFeature_DemotesFeatureToInProgress()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var plan = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Plan, "# Plan\n\n<goal>\noriginal\n</goal>\n",
            null, null, null, default);
        var task1 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        var task2 = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 2", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
        await artifactService.SetStatusAsync(task1.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);
        await artifactService.SetStatusAsync(task2.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 2 done", default);

        var inReview = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        inReview.Status.ShouldBe(PlanningFeatureStatus.Review);

        var patched = await artifactService.PatchAsync(
            plan.Id,
            new PlanningArtifactPatch(Section: "goal", Content: "new", LogNote: "reopened for rework"),
            source: PlanningLogSource.Agent);

        await _context.Entry(inReview).ReloadAsync();
        inReview.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        var demoteLine = PlanningLog.Parse(inReview.Body).Last();
        demoteLine.Source.ShouldBe(PlanningLogSource.Auto);
        demoteLine.Text.ShouldBe("feature demoted: review → inProgress (plan no longer validated)");
        patched.Artifact.FeatureStatusChanged.ShouldBe(PlanningArtifact.FeatureDemotedStatusEcho);
    }

    [Fact]
    public async Task PatchAsync_DemotingValidatedSpec_OnDoneFeature_FeatureStaysDone()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\noriginal\n</goal>\n",
            null, null, null, default);
        var plan = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var task = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
        await artifactService.SetStatusAsync(task.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Result, "close-out", null, null, null, default);
        await featureService.SetStatusAsync(
            _projectId, "feat", PlanningFeatureStatus.Done, "closing the feature", ct: default);

        var done = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        var logLinesBefore = PlanningLog.Parse(done.Body).Count;

        await artifactService.PatchAsync(
            spec.Id,
            new PlanningArtifactPatch(Section: "goal", Content: "new", LogNote: "reopened for rework"),
            source: PlanningLogSource.Agent);

        await _context.Entry(done).ReloadAsync();
        done.Status.ShouldBe(PlanningFeatureStatus.Done);
        var featureLog = PlanningLog.Parse(done.Body);
        featureLog.Count.ShouldBe(logLinesBefore);
        featureLog.ShouldNotContain(l => l.Text.Contains("feature demoted"));
    }

    [Fact]
    public async Task PatchAsync_DemotingValidatedSpec_OnReadyFeature_FeatureUntouched()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\noriginal\n</goal>\n",
            null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);

        var ready = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        var logLinesBefore = PlanningLog.Parse(ready.Body).Count;

        await artifactService.PatchAsync(
            spec.Id,
            new PlanningArtifactPatch(Section: "goal", Content: "new", LogNote: "reopened for rework"),
            source: PlanningLogSource.Agent);

        await _context.Entry(ready).ReloadAsync();
        ready.Status.ShouldBe(PlanningFeatureStatus.Ready);
        PlanningLog.Parse(ready.Body).Count.ShouldBe(logLinesBefore);
    }

    [Fact]
    public async Task UpsertAsync_DemotingValidatedSpec_OnReviewFeature_DemotesFeatureToInProgress()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\noriginal\n</goal>\n",
            null, null, null, default);
        var plan = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        var task = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task 1", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
        await artifactService.SetStatusAsync(task.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);

        var inReview = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        inReview.Status.ShouldBe(PlanningFeatureStatus.Review);

        var updated = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nrewritten\n</goal>\n",
            null, null, "rewriting the spec", default,
            source: PlanningLogSource.Agent);

        await _context.Entry(inReview).ReloadAsync();
        inReview.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        PlanningLog.Parse(inReview.Body).Last().Text
            .ShouldBe("feature demoted: review → inProgress (spec no longer validated)");
        updated.FeatureStatusChanged.ShouldBe(PlanningArtifact.FeatureDemotedStatusEcho);
    }

    [Fact]
    public async Task PatchAsync_NonDemotingPatch_OnReviewFeature_FeatureStaysInReview()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);
        var plan = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Plan, "plan", null, null, null, default);
        await artifactService.ValidateAsync(spec.Id, default);
        await artifactService.ValidateAsync(plan.Id, default);
        var task = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task,
            "<acceptance-criteria>\n- [ ] wrap up\n</acceptance-criteria>", null, null, null, default);
        await artifactService.SetStatusAsync(task.Id, PlanningArtifactStatus.Done, PlanningLogSource.Agent, "task 1 done", default);

        var inReview = await _context.PlanningFeatures.FirstAsync(f => f.Id == feature.Id);
        inReview.Status.ShouldBe(PlanningFeatureStatus.Review);

        await artifactService.PatchAsync(
            task.Id,
            new PlanningArtifactPatch(TickIndex: 1),
            source: PlanningLogSource.Agent);

        await _context.Entry(inReview).ReloadAsync();
        inReview.Status.ShouldBe(PlanningFeatureStatus.Review);
    }

    [Fact]
    public async Task ValidateAsync_WithRepliedApproval_ValidationCarriesProofLine()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);

        var notification = AgentNotification.Create(
            NotificationKind.Approval, "please approve the spec", _projectId, "feat");
        notification.Reply("lgtm");
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();

        await artifactService.ValidateAsync(
            spec.Id, source: PlanningLogSource.Agent, requireHumanProof: true, ct: default);

        await _context.Entry(spec).ReloadAsync();
        var line = PlanningLog.Parse(spec.Body).ShouldHaveSingleItem();
        line.Source.ShouldBe(PlanningLogSource.Agent);
        line.Text.ShouldBe($"validated with human proof: notification {notification.Id} replied {notification.RepliedAt:yyyy-MM-dd HH:mm:ss} UTC");
    }

    [Fact]
    public async Task ValidateAsync_WithHiddenRepliedApproval_StillFindsProofLine()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);

        var notification = AgentNotification.Create(
            NotificationKind.Approval, "please approve the spec", _projectId, "feat");
        notification.Reply("lgtm");
        notification.Hide();
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();

        await artifactService.ValidateAsync(
            spec.Id, source: PlanningLogSource.Agent, requireHumanProof: true, ct: default);

        await _context.Entry(spec).ReloadAsync();
        var line = PlanningLog.Parse(spec.Body).ShouldHaveSingleItem();
        line.Source.ShouldBe(PlanningLogSource.Agent);
        line.Text.ShouldBe($"validated with human proof: notification {notification.Id} replied {notification.RepliedAt:yyyy-MM-dd HH:mm:ss} UTC");
    }

    [Fact]
    public async Task ValidateAsync_WithoutNotification_ThrowsValidationProofRequired()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.ValidateAsync(spec.Id, requireHumanProof: true, ct: default));

        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Code.ShouldBe(PlanningFailureCode.ValidationProofRequired);
        ex.Message.ShouldBe(
            "Human approval required: send overmind_notify(kind=approval, featureSlug=feat) " +
            "and wait for the human's reply in the notification center, or validate via the UI.");
    }

    [Fact]
    public async Task ValidateAsync_ArtifactEditedAfterReply_ThrowsValidationProofRequired()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec v1", null, null, null, default);

        var notification = AgentNotification.Create(
            NotificationKind.Approval, "please approve the spec", _projectId, "feat");
        notification.Reply("lgtm");
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();

        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec v2", null, null, "edited after the reply", default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.ValidateAsync(spec.Id, requireHumanProof: true, ct: default));

        ex.Code.ShouldBe(PlanningFailureCode.ValidationProofRequired);
    }

    [Fact]
    public async Task ValidateAsync_ApprovalReplyOnOtherFeature_ThrowsValidationProofRequired()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);

        var notification = AgentNotification.Create(
            NotificationKind.Approval, "please approve the other feature", _projectId, "other-feature");
        notification.Reply("lgtm");
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.ValidateAsync(spec.Id, requireHumanProof: true, ct: default));

        ex.Code.ShouldBe(PlanningFailureCode.ValidationProofRequired);
    }

    [Fact]
    public async Task ValidateAsync_ApprovalReplyInOtherProject_ThrowsValidationProofRequired()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var otherProject = Project.Create("other-project", "/tmp/other-project");
        _context.Projects.Add(otherProject);
        await _context.SaveChangesAsync();

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);

        var notification = AgentNotification.Create(
            NotificationKind.Approval, "please approve feat", otherProject.Id, "feat");
        notification.Reply("lgtm");
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.ValidateAsync(spec.Id, requireHumanProof: true, ct: default));

        ex.Code.ShouldBe(PlanningFailureCode.ValidationProofRequired);
    }

    [Fact]
    public async Task ValidateAsync_UnrepliedApproval_DoesNotCountAsProof()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);

        _context.AgentNotifications.Add(
            AgentNotification.Create(NotificationKind.Approval, "please approve the spec", _projectId, "feat"));
        await _context.SaveChangesAsync();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.ValidateAsync(spec.Id, requireHumanProof: true, ct: default));

        ex.Code.ShouldBe(PlanningFailureCode.ValidationProofRequired);
    }

    [Fact]
    public async Task ValidateAsync_QuestionKindReply_DoesNotCountAsProof()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);

        var notification = AgentNotification.Create(
            NotificationKind.Question, "which scope?", _projectId, "feat");
        notification.Reply("the narrow one");
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.ValidateAsync(spec.Id, requireHumanProof: true, ct: default));

        ex.Code.ShouldBe(PlanningFailureCode.ValidationProofRequired);
    }

    [Fact]
    public async Task ValidateAsync_ExplicitLogNote_BypassesProofSearch()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Spec, "spec", null, null, null, default);

        await artifactService.ValidateAsync(spec.Id, "validated as migrated — pre-approved on disk: x", PlanningLogSource.Agent, ct: default);

        await _context.Entry(spec).ReloadAsync();
        var line = PlanningLog.Parse(spec.Body).ShouldHaveSingleItem();
        line.Source.ShouldBe(PlanningLogSource.Agent);
        line.Text.ShouldBe("validated as migrated — pre-approved on disk: x");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetStatusAsync_DoneWithoutResult_ThrowsFeatureResultRequired(bool requireHumanTap)
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            featureService.SetStatusAsync(
                _projectId, "feat", PlanningFeatureStatus.Done, "closing the feature",
                requireHumanTap, default));

        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Code.ShouldBe(PlanningFailureCode.FeatureResultRequired);
        ex.Message.ShouldContain("result artifact is missing");
        ex.Message.ShouldContain("kind=result");
    }

    [Fact]
    public async Task SetStatusAsync_DoneWithResult_NoTap_ThrowsFeatureDoneTapRequired()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Result, "close-out", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            featureService.SetStatusAsync(
                _projectId, "feat", PlanningFeatureStatus.Done, "closing the feature",
                requireHumanTap: true, ct: default));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureDoneTapRequired);
        ex.Message.ShouldContain("overmind_notify(kind=approval, featureSlug=feat)");
        ex.Message.ShouldContain("newer than the result's last update");
    }

    [Fact]
    public async Task SetStatusAsync_DoneWithReplyOlderThanResultUpdate_ThrowsFeatureDoneTapRequired()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var notification = AgentNotification.Create(
            NotificationKind.Approval, "please approve the close", _projectId, "feat");
        notification.Reply("lgtm");
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();

        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Result, "close-out v2", null, null, "edited after the reply", default);

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            featureService.SetStatusAsync(
                _projectId, "feat", PlanningFeatureStatus.Done, "closing the feature",
                requireHumanTap: true, ct: default));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureDoneTapRequired);
    }

    [Fact]
    public async Task SetStatusAsync_DoneWithFreshTap_AppendsProvenanceLine()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Result, "close-out", null, null, null, default);
        var notification = AgentNotification.Create(
            NotificationKind.Approval, "please approve the close", _projectId, "feat");
        notification.Reply("lgtm");
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();

        var claimed = await _context.PlanningFeatures.SingleAsync(f => f.Slug == "feat");
        claimed.Claim("Dan", "qwen", Guid.NewGuid(), DateTime.UtcNow);
        await _context.SaveChangesAsync();

        await featureService.SetStatusAsync(
            _projectId, "feat", PlanningFeatureStatus.Done, "review cycle closed it",
            requireHumanTap: true, ct: default);

        var closed = await _context.PlanningFeatures.AsNoTracking().SingleAsync(f => f.Slug == "feat");
        var log = PlanningLog.Parse(closed.Body).ToList();
        log.ShouldContain(l => l.Source == PlanningLogSource.Agent &&
            l.Text == $"done via agent review by Dan (qwen), approved by human tap {notification.RepliedAt:yyyy-MM-dd}");
        log.ShouldContain(l => l.Text == "review cycle closed it");
    }

    [Fact]
    public async Task SetStatusAsync_DoneWithFreshTap_NoClaim_ProvenanceOmitsReviewer()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Result, "close-out", null, null, null, default);
        var notification = AgentNotification.Create(
            NotificationKind.Approval, "please approve the close", _projectId, "feat");
        notification.Reply("lgtm");
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();

        await featureService.SetStatusAsync(
            _projectId, "feat", PlanningFeatureStatus.Done, "review cycle closed it",
            requireHumanTap: true, ct: default);

        var closed = await _context.PlanningFeatures.AsNoTracking().SingleAsync(f => f.Slug == "feat");
        var line = PlanningLog.Parse(closed.Body)
            .Single(l => l.Text.StartsWith("done via agent review"));
        line.Text.ShouldBe($"done via agent review, approved by human tap {notification.RepliedAt:yyyy-MM-dd}");
    }

    [Fact]
    public async Task SetStatusAsync_Done_HiddenApprovalReplyStillCountsAsProof()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Result, "close-out", null, null, null, default);
        var notification = AgentNotification.Create(
            NotificationKind.Approval, "please approve the close", _projectId, "feat");
        notification.Reply("lgtm");
        notification.Hide();
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();

        await featureService.SetStatusAsync(
            _projectId, "feat", PlanningFeatureStatus.Done, "review cycle closed it",
            requireHumanTap: true, ct: default);

        var closed = await _context.PlanningFeatures.AsNoTracking().SingleAsync(f => f.Slug == "feat");
        closed.Status.ShouldBe(PlanningFeatureStatus.Done);
        PlanningLog.Parse(closed.Body).ShouldContain(l => l.Text.StartsWith("done via agent review"));
    }

    [Fact]
    public async Task SetStatusAsync_NonDoneTarget_NeedsNoResultOrTap()
    {
        var featureService = CreateFeatureService();
        await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var moved = await featureService.SetStatusAsync(
            _projectId, "feat", PlanningFeatureStatus.InProgress, "picking up the feature", default);

        moved.Status.ShouldBe(PlanningFeatureStatus.InProgress);
    }

    [Fact]
    public async Task UpdateAsync_DoneWithoutResult_ThrowsFeatureResultRequired()
    {
        var featureService = CreateFeatureService();
        await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            featureService.UpdateAsync(_projectId, "feat", null, null, PlanningFeatureStatus.Done, default));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureResultRequired);

        var untouched = await _context.PlanningFeatures.AsNoTracking().SingleAsync(f => f.Slug == "feat");
        untouched.Status.ShouldBe(PlanningFeatureStatus.Draft);
    }

    [Fact]
    public async Task UpdateAsync_DoneWithResult_NeedsNoTap()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Result, "close-out", null, null, null, default);

        var closed = await featureService.UpdateAsync(
            _projectId, "feat", null, null, PlanningFeatureStatus.Done, default);

        closed.Status.ShouldBe(PlanningFeatureStatus.Done);
        var log = PlanningLog.Parse(closed.Body).ToList();
        log.ShouldNotContain(l => l.Text.Contains("done via agent review"));
        log.Last().Source.ShouldBe(PlanningLogSource.Ui);
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

        patched.Artifact.Version.ShouldBe(2);
        patched.Artifact.Body.ShouldContain("patched goal");
        patched.Artifact.Body.ShouldNotContain("old goal");

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
            artifact.Id, new PlanningArtifactPatch(TickIndex: 1), default);

        patched.TickedIndex.ShouldBe(1);
        patched.TickedText.ShouldBe("write the parser");
        patched.Artifact.Version.ShouldBe(2);
        patched.Artifact.Body.ShouldContain("[x] write the parser");

        var persisted = await _context.PlanningArtifactVersions.SingleAsync(v => v.ArtifactId == artifact.Id);
        persisted.Body.ShouldContain("[ ] write the parser");
    }

    [Fact]
    public async Task PatchAsync_UnknownSection_OnTask_ThrowsWithVocabulary()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "# Task\n\n<goal>\nold\n</goal>\n", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.PatchAsync(
                artifact.Id, new PlanningArtifactPatch(Section: "banana", Content: "x"), default));

        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Code.ShouldBe(PlanningFailureCode.PatchSectionNotFound);
        ex.Message.ShouldContain("goal");
        ex.Message.ShouldContain("Accepted sections for tasks");
        ex.Message.ShouldContain("obstacles");
    }

    [Fact]
    public async Task PatchAsync_UnknownSection_OnSpec_InsertsTheTag()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nold\n</goal>\n", null, null, null, default);

        var patched = await artifactService.PatchAsync(
            artifact.Id, new PlanningArtifactPatch(Section: "refs", Content: "- the spec"), default);

        PlanningSections.Find("refs", patched.Artifact.Body)!.Content.ShouldBe("- the spec");
    }

    [Fact]
    public async Task UpsertArtifact_InlineSections_CanonicalizeOnStore()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifact = await CreateArtifactService().UpsertAsync(
            feature.Id,
            PlanningArtifactKind.Spec,
            "# Spec\n\n<goal>\nShip.\n</goal>\n\n<anti-scope>No parser changes.</anti-scope>\n",
            null, null, null, default);

        artifact.Body.ShouldBe(
            "# Spec\n\n<goal>\nShip.\n</goal>\n\n<anti-scope>\nNo parser changes.\n</anti-scope>\n");
    }

    [Fact]
    public async Task UpsertArtifact_FrictionPayload_InlineSectionsPatchableWithoutResend()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifact = await CreateArtifactService().UpsertAsync(
            feature.Id,
            PlanningArtifactKind.Task,
            """
            # Task 01

            <goal>
            Every section in a body becomes patchable.
            </goal>

            <scope>
            - canonicalize at the entrances
            </scope>

            <anti-scope>Parser stays anchored.</anti-scope>

            <constraints>No new packages.</constraints>

            <acceptance-criteria>
            - [ ] friction payload pinned
            </acceptance-criteria>
            """,
            0, null, null, default);

        var antiScope = await CreateArtifactService().PatchAsync(
            artifact.Id,
            new PlanningArtifactPatch(Section: "anti-scope", Content: "Parser stays anchored, entries canonicalize."),
            default);
        antiScope.Artifact.Body
            .ShouldContain("<anti-scope>\nParser stays anchored, entries canonicalize.\n</anti-scope>");

        var constraints = await CreateArtifactService().PatchAsync(
            artifact.Id,
            new PlanningArtifactPatch(Section: "constraints", Content: "- english only"),
            default);
        constraints.Artifact.Body.ShouldContain("<constraints>\n- english only\n</constraints>");
    }

    [Fact]
    public async Task UpsertTasks_InlineSections_CanonicalizeOnStore()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var batch = await CreateArtifactService().UpsertTasksAsync(
            feature.Id, [new PlanningTaskUpsert("# T\n\n<goal>Ship.</goal>")], default);

        var body = batch.Artifacts.ShouldHaveSingleItem().Body;
        body.ShouldStartWith("# T\n\n<goal>\nShip.\n</goal>");
        PlanningSections.Find("goal", body)!.Content.ShouldBe("Ship.");
    }

    [Fact]
    public async Task UpsertTasks_ItemWithoutObstacles_YieldsTaskWithDeclaredEmptyObstacles()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var batch = await CreateArtifactService().UpsertTasksAsync(
            feature.Id, [new PlanningTaskUpsert("# T\n\n<goal>\nShip.\n</goal>\n")], default);

        var task = batch.Artifacts.ShouldHaveSingleItem();
        PlanningSections.Find("obstacles", task.Body)!.Content.ShouldBe(PlanningSections.EmptyMarker);
        PlanningSections.List(task.Body).ShouldBe(PlanningTaskSections.Declared);

        var patched = await CreateArtifactService().PatchAsync(
            task.Id,
            new PlanningArtifactPatch(Section: "obstacles", Content: "- the index is flaky", LogNote: "recording obstacles"),
            default);

        PlanningSections.Find("obstacles", patched.Artifact.Body)!.Content.ShouldBe("- the index is flaky");
    }

    [Fact]
    public async Task UpsertTasks_ResendingTheSameBatch_LeavesVersionsUntouched()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");
        var artifactService = CreateArtifactService();

        const string body = "# T\n\n<goal>\nShip.\n</goal>\n";
        await artifactService.UpsertTasksAsync(feature.Id, [new PlanningTaskUpsert(body, 0)], default);
        var resent = await artifactService.UpsertTasksAsync(feature.Id, [new PlanningTaskUpsert(body, 0)], default);

        var task = resent.Artifacts.ShouldHaveSingleItem();
        task.Version.ShouldBe(1);
        _context.PlanningArtifactVersions.Count(v => v.ArtifactId == task.Id).ShouldBe(0);
    }

    [Fact]
    public async Task Patch_StoredInlineSection_ReturnsCanonicalizationHint()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");
        var stored = PlanningArtifact.Create(
            feature.Id,
            PlanningArtifactKind.Spec,
            "# Spec\n\n<goal>\nOld.\n</goal>\n\n<anti-scope>None.</anti-scope>\n");
        _context.PlanningArtifacts.Add(stored);
        await _context.SaveChangesAsync();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => CreateArtifactService().PatchAsync(
                stored.Id, new PlanningArtifactPatch(Section: "anti-scope", Content: "x"), default));

        ex.Code.ShouldBe(PlanningFailureCode.PatchSectionNotFound);
        ex.Message.ShouldContain("A one-line '<anti-scope>…</anti-scope>' section exists");
        ex.Message.ShouldContain("re-send the body once");
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

        patched.Artifact.Status.ShouldBe(PlanningArtifactStatus.Draft);
        patched.Artifact.Version.ShouldBe(2);

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
            artifact.Id, new PlanningArtifactPatch(TickIndex: 1), default);

        patched.NoChange.ShouldBeTrue();
        patched.TickedIndex.ShouldBe(1);
        patched.TickedText.ShouldBe("already done");
        patched.Artifact.Version.ShouldBe(1);

        var snapshots = await _context.PlanningArtifactVersions.CountAsync(v => v.ArtifactId == artifact.Id);
        snapshots.ShouldBe(0);
    }

    [Fact]
    public async Task PatchAsync_UntickPatch_ClearsItem_AndEchoesResolvedItem()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id,
            PlanningArtifactKind.Task,
            "<acceptance-criteria>\n- [x] mistaken tick\n- [ ] keep me\n</acceptance-criteria>\n",
            null, null, null, default);

        var patched = await artifactService.PatchAsync(
            artifact.Id, new PlanningArtifactPatch(TickText: "mistaken tick", Untick: true), default);

        patched.NoChange.ShouldBeFalse();
        patched.TickedIndex.ShouldBe(1);
        patched.TickedText.ShouldBe("mistaken tick");
        patched.Artifact.Version.ShouldBe(2);
        patched.Artifact.Body.ShouldContain("[ ] mistaken tick");
        patched.Artifact.Body.ShouldContain("[ ] keep me");

        var persisted = await _context.PlanningArtifactVersions.SingleAsync(v => v.ArtifactId == artifact.Id);
        persisted.Body.ShouldContain("[x] mistaken tick");
    }

    [Fact]
    public async Task PatchAsync_UntickAlreadyUnticked_IsNoOp_EchoingResolvedItem()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id,
            PlanningArtifactKind.Task,
            "<acceptance-criteria>\n- [ ] untouched item\n</acceptance-criteria>\n",
            null, null, null, default);

        var patched = await artifactService.PatchAsync(
            artifact.Id, new PlanningArtifactPatch(TickIndex: 1, Untick: true), default);

        patched.NoChange.ShouldBeTrue();
        patched.TickedIndex.ShouldBe(1);
        patched.TickedText.ShouldBe("untouched item");
        patched.Artifact.Version.ShouldBe(1);

        var snapshots = await _context.PlanningArtifactVersions.CountAsync(v => v.ArtifactId == artifact.Id);
        snapshots.ShouldBe(0);
    }

    [Fact]
    public async Task PatchAsync_UntickMarkerlessItem_ThrowsTickItemNotCheckable()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id,
            PlanningArtifactKind.Task,
            "<acceptance-criteria>\n- plain bullet\n</acceptance-criteria>\n",
            null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.PatchAsync(
                artifact.Id, new PlanningArtifactPatch(TickIndex: 1, Untick: true), default));

        ex.Code.ShouldBe(PlanningFailureCode.TickItemNotCheckable);
    }

    [Fact]
    public async Task PatchAsync_UntickWithoutSelector_ThrowsInvalidPatchRequest()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nold\n</goal>\n", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.PatchAsync(artifact.Id, new PlanningArtifactPatch(Untick: true), default));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidPatchRequest);
    }

    [Fact]
    public async Task ArtifactMutation_MovesFeatureUpdatedAt_WithoutTouchingItsLog()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var spec = await artifactService.UpsertAsync(
            feature.Id,
            PlanningArtifactKind.Spec,
            "# Spec\n\n<goal>\nold goal\n</goal>\n",
            null, null, null, default);

        await _context.Entry(feature).ReloadAsync();
        var updatedAtAfterCreate = feature.UpdatedAt;
        var featureLogBefore = PlanningLog.Parse(feature.Body).Count;
        updatedAtAfterCreate.ShouldBeGreaterThan(feature.CreatedAt);

        await artifactService.AppendLogAsync(spec.Id, "progress line", default);
        await artifactService.PatchAsync(
            spec.Id, new PlanningArtifactPatch(Section: "goal", Content: "new goal"), default);
        await artifactService.SetStatusAsync(
            spec.Id, PlanningArtifactStatus.Validated, PlanningLogSource.Agent, "freezing", default);
        await artifactService.ValidateAsync(spec.Id, default);

        await _context.Entry(feature).ReloadAsync();
        feature.UpdatedAt.ShouldBeGreaterThan(updatedAtAfterCreate);
        PlanningLog.Parse(feature.Body).Count.ShouldBe(featureLogBefore);
    }

    [Fact]
    public async Task AppendLogAsync_RealTick_AndRetick_MoveFeatureUpdatedAtOnlyOnRealChange()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var task = await artifactService.UpsertAsync(
            feature.Id,
            PlanningArtifactKind.Task,
            "<acceptance-criteria>\n- [ ] ship it\n</acceptance-criteria>\n",
            null, null, null, default);

        await _context.Entry(feature).ReloadAsync();
        var updatedAtAfterCreate = feature.UpdatedAt;

        await artifactService.AppendLogAsync(task.Id, "halfway there", default);
        await _context.Entry(feature).ReloadAsync();
        var updatedAtAfterLog = feature.UpdatedAt;
        updatedAtAfterLog.ShouldBeGreaterThan(updatedAtAfterCreate);

        var flip = await artifactService.PatchAsync(
            task.Id, new PlanningArtifactPatch(TickIndex: 1), default);
        flip.NoChange.ShouldBeFalse();
        await _context.Entry(feature).ReloadAsync();
        var updatedAtAfterTick = feature.UpdatedAt;
        updatedAtAfterTick.ShouldBeGreaterThan(updatedAtAfterLog);

        await artifactService.PatchAsync(
            task.Id, new PlanningArtifactPatch(TickIndex: 1), default);
        await _context.Entry(feature).ReloadAsync();
        feature.UpdatedAt.ShouldBe(updatedAtAfterTick);
    }

    [Fact]
    public async Task UpsertAsync_IdenticalBodyWithoutNote_DoesNotMoveFeatureUpdatedAt()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec body", null, null, null, default);

        await _context.Entry(feature).ReloadAsync();
        var updatedAtAfterCreate = feature.UpdatedAt;

        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "spec body", null, null, null, default);

        await _context.Entry(feature).ReloadAsync();
        feature.UpdatedAt.ShouldBe(updatedAtAfterCreate);
    }

    [Fact]
    public async Task UpsertTasksAsync_MovesFeatureUpdatedAt_WhenATaskChanges()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        await artifactService.UpsertTasksAsync(
            feature.Id,
            [new PlanningTaskUpsert("task 1", 0, null, null, null)],
            default);

        await _context.Entry(feature).ReloadAsync();
        var updatedAtAfterCreate = feature.UpdatedAt;

        await artifactService.UpsertTasksAsync(
            feature.Id,
            [new PlanningTaskUpsert("task 1 rewritten", 0, null, "rework", null)],
            default);

        await _context.Entry(feature).ReloadAsync();
        feature.UpdatedAt.ShouldBeGreaterThan(updatedAtAfterCreate);
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
                new PlanningArtifactPatch(Section: "goal", Content: "new", TickIndex: 1),
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
    public async Task PatchAsync_SectionWithoutContent_OnMissingSection_InsertsDeclaredEmpty()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nold\n</goal>\n", null, null, null, default);

        var patched = await artifactService.PatchAsync(
            artifact.Id, new PlanningArtifactPatch(Section: "scope"), default);

        PlanningSections.Find("scope", patched.Artifact.Body)!.Content.ShouldBe(PlanningSections.EmptyMarker);
        patched.Artifact.Version.ShouldBe(2);
    }

    [Fact]
    public async Task PatchAsync_SectionWithoutContent_OnTaskMissingSection_InsertsDeclaredEmpty()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");
        var stored = PlanningArtifact.Create(
            feature.Id, PlanningArtifactKind.Task, "# Task\n\n<goal>\nold\n</goal>\n");
        _context.PlanningArtifacts.Add(stored);
        await _context.SaveChangesAsync();

        var patched = await CreateArtifactService().PatchAsync(
            stored.Id, new PlanningArtifactPatch(Section: "obstacles"), default);

        PlanningSections.Find("obstacles", patched.Artifact.Body)!.Content.ShouldBe(PlanningSections.EmptyMarker);
        patched.Artifact.Version.ShouldBe(2);
    }

    [Fact]
    public async Task PatchAsync_SectionWithoutContent_OnExistingSection_ThrowsContentRequired()
    {
        var featureService = CreateFeatureService();
        var feature = await featureService.CreateAsync(_projectId, "feat", "Title", "body");

        var artifactService = CreateArtifactService();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nold\n</goal>\n", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.PatchAsync(
                artifact.Id, new PlanningArtifactPatch(Section: "goal"), default));

        ex.Code.ShouldBe(PlanningFailureCode.SectionContentRequiredForReplace);
        ex.Message.ShouldContain("goal");
    }

    [Fact]
    public async Task UpsertArtifact_TaskBlockedBy_ResolvesOrdersToEdges()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureService().CreateAsync(_projectId, "feat", "Title", "body");
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task one", 1, null, null, default);

        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default,
            blockedBy: [1]);

        var blocked = await _context.PlanningArtifacts
            .Include(a => a.BlockedBy)
            .SingleAsync(a => a.FeatureId == feature.Id && a.Order == 0);
        var blocker = await _context.PlanningArtifacts
            .SingleAsync(a => a.FeatureId == feature.Id && a.Order == 1);

        blocked.BlockedBy.Single().BlockingId.ShouldBe(blocker.Id);
    }

    [Fact]
    public async Task UpsertArtifact_CreateTaskWithBlockedBy_CreatesEdges()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureService().CreateAsync(_projectId, "feat", "Title", "body");
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task one", 1, null, null, default);

        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task two", 2, null, null, default,
            blockedBy: [0, 1]);

        var created = await _context.PlanningArtifacts
            .Include(a => a.BlockedBy)
            .SingleAsync(a => a.FeatureId == feature.Id && a.Order == 2);

        created.BlockedBy.Count.ShouldBe(2);
    }

    [Fact]
    public async Task UpsertArtifact_BlockedByRearrayed_ReplacesEdgeSet()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureService().CreateAsync(_projectId, "feat", "Title", "body");
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task one", 1, null, null, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task two", 2, null, null, default);
        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default, blockedBy: [1]);

        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default, blockedBy: [2]);

        var blocked = await _context.PlanningArtifacts
            .Include(a => a.BlockedBy)
            .SingleAsync(a => a.FeatureId == feature.Id && a.Order == 0);

        blocked.BlockedBy.Single().BlockingId.ShouldBe(
            (await _context.PlanningArtifacts.SingleAsync(a => a.Order == 2 && a.FeatureId == feature.Id)).Id);
    }

    [Fact]
    public async Task UpsertArtifact_BlockedByEmptyArray_ClearsEdgeSet()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureService().CreateAsync(_projectId, "feat", "Title", "body");
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task one", 1, null, null, default);
        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default, blockedBy: [1]);

        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default, blockedBy: []);

        _context.PlanningTaskDependencies.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpsertArtifact_WithoutBlockedBy_KeepsExistingEdges()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureService().CreateAsync(_projectId, "feat", "Title", "body");
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task one", 1, null, null, default);
        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default, blockedBy: [1]);

        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task zero edited", 0, null, null, default);

        _context.PlanningTaskDependencies.Count().ShouldBe(1);
    }

    [Fact]
    public async Task UpsertArtifact_TaskBlockedByUnknownOrder_RefusesWithoutCreating()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureService().CreateAsync(_projectId, "feat", "Title", "body");

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            artifactService.UpsertAsync(
                feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default,
                blockedBy: [3]));

        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Code.ShouldBe(PlanningFailureCode.BlockedByOrderUnknown);
        (await _context.PlanningArtifacts.CountAsync(a => a.FeatureId == feature.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task UpsertArtifact_TaskBlockedBySelfReference_Throws()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureService().CreateAsync(_projectId, "feat", "Title", "body");
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            artifactService.UpsertAsync(
                feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default,
                blockedBy: [0]));

        ex.Code.ShouldBe(PlanningFailureCode.BlockedBySelfReference);
    }

    [Fact]
    public async Task UpsertArtifact_TaskBlockedByDuplicateOrder_Throws()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureService().CreateAsync(_projectId, "feat", "Title", "body");
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default);
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task one", 1, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            artifactService.UpsertAsync(
                feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default,
                blockedBy: [1, 1]));

        ex.Code.ShouldBe(PlanningFailureCode.BlockedByDuplicateOrder);
    }

    [Fact]
    public async Task UpsertArtifact_TaskBlockedByCycle_Rejected()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureService().CreateAsync(_projectId, "feat", "Title", "body");
        await artifactService.UpsertAsync(feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default);
        await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Task, "task one", 1, null, null, default, blockedBy: [0]);

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            artifactService.UpsertAsync(
                feature.Id, PlanningArtifactKind.Task, "task zero", 0, null, null, default,
                blockedBy: [1]));

        ex.Code.ShouldBe(PlanningFailureCode.TaskDependencyCycle);
        _context.PlanningTaskDependencies.Count().ShouldBe(1);
    }

    [Fact]
    public async Task UpsertArtifact_BlockedByOnSpec_Throws()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureService().CreateAsync(_projectId, "feat", "Title", "body");

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            artifactService.UpsertAsync(
                feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nx\n</goal>\n", null, null, null, default,
                blockedBy: []));

        ex.Code.ShouldBe(PlanningFailureCode.BlockedByNotAllowedForKind);
    }

    [Fact]
    public async Task SetCodePin_SetsFieldsAndLogLine_AndSurvivesReload()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "pinned", "Title", "body");

        var pinned = await service.SetCodePinAsync(
            _projectId, "pinned", "feat/pin-branch", "/repo/wt", PlanningLogSource.Agent);

        pinned.PinnedBranch.ShouldBe("feat/pin-branch");
        pinned.PinnedWorktreePath.ShouldBe("/repo/wt");
        pinned.LastLogLine.ShouldContain("code pin set: branch 'feat/pin-branch', worktree '/repo/wt'");

        var lines = PlanningLog.Parse(pinned.Body);
        lines.Last().Source.ShouldBe(PlanningLogSource.Agent);

        var reloaded = await _context.PlanningFeatures.SingleAsync(f => f.Slug == "pinned");
        reloaded.PinnedBranch.ShouldBe("feat/pin-branch");
        reloaded.PinnedWorktreePath.ShouldBe("/repo/wt");
    }

    [Fact]
    public async Task SetCodePin_Replace_AppendsOneLinePerChange()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "repinned", "Title", "body");

        await service.SetCodePinAsync(_projectId, "repinned", "old", null, PlanningLogSource.Agent);
        var replaced = await service.SetCodePinAsync(
            _projectId, "repinned", "new", "/repo/wt", PlanningLogSource.Agent);

        replaced.PinnedBranch.ShouldBe("new");
        var lines = PlanningLog.Parse(replaced.Body);
        lines.Count.ShouldBe(2);
        lines[1].Text.ShouldBe("code pin set: branch 'new', worktree '/repo/wt'");
    }

    [Fact]
    public async Task SetCodePin_ClearOnEmptyPin_IsNoOpWithoutLogLine()
    {
        var service = CreateFeatureService();
        var feature = await service.CreateAsync(_projectId, "unpinned", "Title", "body");

        var cleared = await service.SetCodePinAsync(_projectId, "unpinned", null, null, PlanningLogSource.Ui);

        cleared.PinnedBranch.ShouldBeNull();
        cleared.PinnedWorktreePath.ShouldBeNull();
        PlanningLog.Parse(cleared.Body).ShouldBeEmpty();
        cleared.LastLogLine.ShouldBe(feature.LastLogLine);
    }

    [Fact]
    public async Task SetCodePin_ClearPinnedPin_ClearsFields()
    {
        var service = CreateFeatureService();
        await service.CreateAsync(_projectId, "toclear", "Title", "body");
        await service.SetCodePinAsync(_projectId, "toclear", "feat/x", "/repo/wt", PlanningLogSource.Agent);

        var cleared = await service.SetCodePinAsync(_projectId, "toclear", null, null, PlanningLogSource.Agent);

        cleared.PinnedBranch.ShouldBeNull();
        cleared.PinnedWorktreePath.ShouldBeNull();
        PlanningLog.Parse(cleared.Body).Last().Text.ShouldBe("code pin cleared");
    }

    [Fact]
    public async Task SetCodePin_MissingFeature_ThrowsNotFound()
    {
        var service = CreateFeatureService();

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            service.SetCodePinAsync(_projectId, "ghost", "feat/x", null, PlanningLogSource.Agent));

        ex.Code.ShouldBe(PlanningFailureCode.MissingFeature);
    }

    [Fact]
    public async Task SetCodePin_MissingProject_ThrowsNotFound()
    {
        var service = CreateFeatureService();

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            service.SetCodePinAsync(Guid.NewGuid(), "feat", "x", null, PlanningLogSource.Agent));

        ex.Code.ShouldBe(PlanningFailureCode.MissingProject);
    }
}
