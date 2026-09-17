using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class BacklogImporterTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer = Substitute.For<IPlanningMaterializer>();
    private readonly Guid _projectId;
    private readonly TempRepo _repo = new();

    public BacklogImporterTests()
    {
        _context = _db.CreateContext();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], []));
        _materializer.ClearAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], []));

        var project = Project.Create($"import-{Guid.NewGuid():N}", _repo.Root, string.Empty);
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
        _repo.Dispose();
    }

    private BacklogImporter CreateImporter() =>
        new(new PlanningFeatureService(_context, _materializer, NullLogger<PlanningFeatureService>.Instance));

    private Task<BacklogImportReport> ImportAsync(string path) =>
        CreateImporter().ImportAsync(new BacklogImportRequest(path, _projectId), CancellationToken.None);

    private async Task<PlanningFeature?> LoadFeatureAsync(string slug) =>
        await _context.PlanningFeatures
            .Include(f => f.Artifacts)
            .FirstOrDefaultAsync(f => f.ProjectId == _projectId && f.Slug == slug);

    [Fact]
    public async Task Import_FullFolder_MapsFeatureSpecPlanAndTasks()
    {
        WriteFullFeature();

        var report = await ImportAsync(_repo.Root);

        report.FeaturesCreated.ShouldBe(1);
        report.FeaturesUpdated.ShouldBe(0);
        report.ArtifactsCreated.ShouldBe(4);
        report.Skipped.ShouldContain(s => s.EndsWith("requirements.md"));

        var feature = await LoadFeatureAsync("010-demo");
        feature.ShouldNotBeNull();
        feature.Title.ShouldBe("Demo feature title");
        feature.Status.ShouldBe(PlanningFeatureStatus.Ready);
        feature.Version.ShouldBe(3);
        feature.CreatedAt.ShouldBe(new DateTime(2026, 9, 1));
        feature.Body.ShouldContain("<goal>");
        feature.Body.ShouldContain("Build the demo.");

        var spec = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Spec);
        spec.Status.ShouldBe(PlanningArtifactStatus.Validated);
        spec.FrozenAt.ShouldBe(new DateTime(2026, 9, 2));
        spec.Order.ShouldBe(0);
        spec.Version.ShouldBe(1);

        var plan = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Plan);
        plan.Status.ShouldBe(PlanningArtifactStatus.Draft);
        plan.FrozenAt.ShouldBeNull();

        var tasks = feature.Artifacts.Where(a => a.Kind == PlanningArtifactKind.Task).OrderBy(a => a.Order).ToList();
        tasks.Count.ShouldBe(2);
        tasks[0].Order.ShouldBe(1);
        tasks[0].Status.ShouldBe(PlanningArtifactStatus.Done);
        tasks[0].Review.ShouldBe(PlanningReviewLevel.Hard);
        tasks[1].Order.ShouldBe(2);
        tasks[1].Status.ShouldBe(PlanningArtifactStatus.InProgress);
        tasks[1].Review.ShouldBe(PlanningReviewLevel.Light);
    }

    [Fact]
    public async Task Import_TaskLogSection_PreservedVerbatim()
    {
        WriteFullFeature();

        await ImportAsync(_repo.Root);

        var feature = await LoadFeatureAsync("010-demo");
        var task = feature!.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task && a.Order == 1);
        task.Body.ShouldContain("<log>");
        task.Body.ShouldContain("2026-09-01 — created from the plan.");
        task.Body.ShouldContain("2026-09-02 — implemented and green.");
    }

    [Fact]
    public async Task Import_FeatureOnlyFolder_ToleratesMissingSpecPlanAndTasks()
    {
        _repo.Write("050-solo/feature.md",
            """
            ---
            feature: 050
            slug: solo
            status: draft
            version: 1
            created: 2026-09-03
            ---

            # Feature 050 — Solo

            Body only.
            """);

        var report = await ImportAsync(_repo.Root);

        report.FeaturesCreated.ShouldBe(1);
        report.ArtifactsCreated.ShouldBe(0);

        var feature = await LoadFeatureAsync("050-solo");
        feature.ShouldNotBeNull();
        feature.Artifacts.ShouldBeEmpty();
        feature.Title.ShouldBe("Solo");
    }

    [Fact]
    public async Task Import_UnknownStatuses_DefaultAndReportSkipped()
    {
        _repo.Write("020-weird/feature.md",
            """
            ---
            feature: 020
            slug: weird
            status: bogus
            version: 1
            created: 2026-09-01
            ---

            # Feature 020 — Weird
            """);
        _repo.Write("020-weird/tasks/01-odd.md",
            """
            ---
            feature: 020
            task: 01
            slug: weird
            status: maybe
            review: medium
            created: 2026-09-01
            ---

            # Odd task
            """);

        var report = await ImportAsync(_repo.Root);

        report.Skipped.ShouldContain(s => s.Contains("unknown status 'bogus'"));
        report.Skipped.ShouldContain(s => s.Contains("unknown status 'maybe'"));
        report.Skipped.ShouldContain(s => s.Contains("unknown review level 'medium'"));

        var feature = await LoadFeatureAsync("020-weird");
        feature.ShouldNotBeNull();
        feature.Status.ShouldBe(PlanningFeatureStatus.Draft);
        feature.Artifacts.Single().Status.ShouldBe(PlanningArtifactStatus.Todo);
        feature.Artifacts.Single().Review.ShouldBe(PlanningReviewLevel.Light);
    }

    [Fact]
    public async Task Import_MaterializerStatusSpellings_Mapped()
    {
        _repo.Write("060-mat/feature.md",
            """
            ---
            feature: 060
            slug: mat
            status: inProgress
            version: 1
            created: 2026-09-01
            ---

            # Feature 060 — Mat
            """);
        _repo.Write("060-mat/tasks/01-on-going.md",
            """
            ---
            feature: 060
            task: 01
            slug: mat
            status: inprogress
            review: light
            created: 2026-09-01
            ---

            # On-going task
            """);

        var report = await ImportAsync(_repo.Root);

        report.Skipped.ShouldBeEmpty();

        var feature = await LoadFeatureAsync("060-mat");
        feature.ShouldNotBeNull();
        feature.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        feature.Artifacts.Single().Status.ShouldBe(PlanningArtifactStatus.InProgress);
    }

    [Fact]
    public async Task Import_SpecPlanInlineComments_AreStrippedBeforeMapping()
    {
        _repo.Write("070-comments/feature.md",
            """
            ---
            feature: 070
            slug: comments
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 070 — Comments
            """);
        _repo.Write("070-comments/spec.md",
            """
            ---
            feature: 070
            slug: comments
            status: approved        # draft | approved (approved = FROZEN)
            created: 2026-09-01
            ---

            # Spec 070 — Comments

            Spec body.
            """);
        _repo.Write("070-comments/plan.md",
            """
            ---
            feature: 070
            slug: comments
            status: draft        # draft | approved (approved = FROZEN)
            created: 2026-09-01
            ---

            # Plan 070 — Comments

            Plan body.
            """);

        var report = await ImportAsync(_repo.Root);

        report.Skipped.ShouldBeEmpty();

        var feature = await LoadFeatureAsync("070-comments");
        feature.ShouldNotBeNull();
        var spec = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Spec);
        spec.Status.ShouldBe(PlanningArtifactStatus.Validated);
        spec.FrozenAt.ShouldBe(new DateTime(2026, 9, 1));
        var plan = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Plan);
        plan.Status.ShouldBe(PlanningArtifactStatus.Draft);
        plan.FrozenAt.ShouldBeNull();
    }

    [Fact]
    public async Task Import_SpecPlanWithoutStatus_DefaultToDraft()
    {
        _repo.Write("080-nostatus/feature.md",
            """
            ---
            feature: 080
            slug: nostatus
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 080 — No status
            """);
        _repo.Write("080-nostatus/spec.md",
            """
            ---
            feature: 080
            slug: nostatus
            created: 2026-09-01
            ---

            # Spec 080 — No status

            Spec body.
            """);

        var report = await ImportAsync(_repo.Root);

        report.Skipped.ShouldBeEmpty();

        var feature = await LoadFeatureAsync("080-nostatus");
        feature.ShouldNotBeNull();
        var spec = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Spec);
        spec.Status.ShouldBe(PlanningArtifactStatus.Draft);
        spec.FrozenAt.ShouldBeNull();
    }

    [Fact]
    public async Task Import_StatusValueStartingWithHash_IsNotAComment()
    {
        _repo.Write("090-hash/feature.md",
            """
            ---
            feature: 090
            slug: hash
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 090 — Hash
            """);
        _repo.Write("090-hash/spec.md",
            """
            ---
            feature: 090
            slug: hash
            status: # approved
            created: 2026-09-01
            ---

            # Spec 090 — Hash

            Spec body.
            """);

        var report = await ImportAsync(_repo.Root);

        report.Skipped.ShouldContain(s => s.Contains("unknown status '#'"));

        var feature = await LoadFeatureAsync("090-hash");
        feature.ShouldNotBeNull();
        feature.Artifacts.Single().Status.ShouldBe(PlanningArtifactStatus.Draft);
    }

    [Fact]
    public async Task Import_ResultMd_MapsAsResultArtifactOrderZero()
    {
        WriteResultFeature();

        var report = await ImportAsync(_repo.Root);

        report.FeaturesCreated.ShouldBe(1);
        report.ArtifactsCreated.ShouldBe(1);
        report.Skipped.ShouldNotContain(s => s.Contains("result.md"));

        var feature = await LoadFeatureAsync("011-result");
        feature.ShouldNotBeNull();
        var result = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Result);
        result.Order.ShouldBe(0);
        result.Status.ShouldBe(PlanningArtifactStatus.Validated);
        result.FrozenAt.ShouldBe(new DateTime(2026, 9, 4));
        result.Review.ShouldBe(PlanningReviewLevel.Light);
        result.Body.ShouldContain("Ship summary.");
    }

    [Fact]
    public async Task Import_ResultMd_ReRun_CreatesNoDuplicate()
    {
        WriteResultFeature();
        await ImportAsync(_repo.Root);

        var second = await ImportAsync(_repo.Root);

        second.FeaturesCreated.ShouldBe(0);
        second.FeaturesUpdated.ShouldBe(1);
        second.ArtifactsCreated.ShouldBe(0);
        second.ArtifactsUpdated.ShouldBe(0);
        second.Skipped.ShouldNotContain(s => s.Contains("result.md"));

        var feature = await LoadFeatureAsync("011-result");
        var results = feature!.Artifacts.Where(a => a.Kind == PlanningArtifactKind.Result).ToList();
        results.Count.ShouldBe(1);
        results.Single().Version.ShouldBe(1);
    }

    [Fact]
    public async Task Import_ResultMd_FolderWithOnlyFeatureAndResult_NothingIgnored()
    {
        _repo.Write("012-clean/feature.md",
            """
            ---
            feature: 012
            slug: clean
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 012 — Clean
            """);
        _repo.Write("012-clean/result.md",
            """
            ---
            feature: 012
            slug: clean
            status: draft
            created: 2026-09-01
            ---

            # Result 012 — Clean

            Draft outcome.
            """);

        var report = await ImportAsync(_repo.Root);

        report.Skipped.ShouldBeEmpty();

        var feature = await LoadFeatureAsync("012-clean");
        var result = feature!.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Result);
        result.Order.ShouldBe(0);
        result.Status.ShouldBe(PlanningArtifactStatus.Draft);
        result.FrozenAt.ShouldBeNull();
    }

    [Fact]
    public async Task Import_ResultMd_UnknownStatus_DefaultsToDraftAndReports()
    {
        _repo.Write("013-weird-result/feature.md",
            """
            ---
            feature: 013
            slug: weird-result
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 013 — Weird result
            """);
        _repo.Write("013-weird-result/result.md",
            """
            ---
            feature: 013
            slug: weird-result
            status: done
            created: 2026-09-01
            ---

            # Result 013 — Weird result

            Outcome body.
            """);

        var report = await ImportAsync(_repo.Root);

        report.Skipped.ShouldContain(s => s.Contains("unknown status 'done'"));

        var feature = await LoadFeatureAsync("013-weird-result");
        var result = feature!.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Result);
        result.Status.ShouldBe(PlanningArtifactStatus.Draft);
        result.FrozenAt.ShouldBeNull();
    }

    [Fact]
    public async Task Import_LetterSuffixedTask_NoCollision_KeepsBaseOrder()
    {
        _repo.Write("014-suffix/feature.md",
            """
            ---
            feature: 014
            slug: suffix
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 014 — Suffix
            """);
        _repo.Write("014-suffix/tasks/01b-extra.md",
            """
            ---
            feature: 014
            task: 01b
            slug: suffix
            status: todo
            review: light
            created: 2026-09-01
            ---

            # Extra task
            """);

        var report = await ImportAsync(_repo.Root);

        report.Skipped.ShouldBeEmpty();

        var feature = await LoadFeatureAsync("014-suffix");
        var task = feature!.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task);
        task.Order.ShouldBe(1);
        task.Status.ShouldBe(PlanningArtifactStatus.Todo);
    }

    [Fact]
    public async Task Import_LetterSuffixedTask_Collision_AppendsAtMaxPlusOneAndReports()
    {
        _repo.Write("015-suffix-collision/feature.md",
            """
            ---
            feature: 015
            slug: suffix-collision
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 015 — Suffix collision
            """);
        _repo.Write("015-suffix-collision/tasks/01-first.md",
            """
            ---
            feature: 015
            task: 01
            slug: suffix-collision
            status: todo
            review: light
            created: 2026-09-01
            ---

            # First task
            """);
        _repo.Write("015-suffix-collision/tasks/02-second.md",
            """
            ---
            feature: 015
            task: 02
            slug: suffix-collision
            status: todo
            review: light
            created: 2026-09-01
            ---

            # Second task
            """);
        _repo.Write("015-suffix-collision/tasks/01b-extra.md",
            """
            ---
            feature: 015
            task: 01b
            slug: suffix-collision
            status: todo
            review: light
            created: 2026-09-01
            ---

            # Extra task
            """);

        var report = await ImportAsync(_repo.Root);

        report.Skipped.ShouldContain(
            s => s.Contains("reassigned:") && s.Contains("01b-extra.md") && s.Contains("appended as order 3"));

        var feature = await LoadFeatureAsync("015-suffix-collision");
        var tasks = feature!.Artifacts.Where(a => a.Kind == PlanningArtifactKind.Task).OrderBy(a => a.Order).ToList();
        tasks.Count.ShouldBe(3);
        tasks[0].Order.ShouldBe(1);
        tasks[1].Order.ShouldBe(2);
        tasks[2].Order.ShouldBe(3);
        tasks[2].Body.ShouldContain("Extra task");
    }

    [Fact]
    public async Task Import_LetterSuffixedTasks_MultipleLetters_AscendByLetter()
    {
        _repo.Write("016-suffix-letters/feature.md",
            """
            ---
            feature: 016
            slug: suffix-letters
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 016 — Suffix letters
            """);
        _repo.Write("016-suffix-letters/tasks/01-first.md",
            """
            ---
            feature: 016
            task: 01
            slug: suffix-letters
            status: todo
            review: light
            created: 2026-09-01
            ---

            # First task
            """);
        _repo.Write("016-suffix-letters/tasks/01b-extra.md",
            """
            ---
            feature: 016
            task: 01b
            slug: suffix-letters
            status: todo
            review: light
            created: 2026-09-01
            ---

            # Extra task
            """);
        _repo.Write("016-suffix-letters/tasks/01c-more.md",
            """
            ---
            feature: 016
            task: 01c
            slug: suffix-letters
            status: todo
            review: light
            created: 2026-09-01
            ---

            # More task
            """);

        var report = await ImportAsync(_repo.Root);

        var reassignments = report.Skipped.Where(s => s.Contains("reassigned:")).ToList();
        reassignments.Count.ShouldBe(2);
        reassignments.ShouldContain(s => s.Contains("01b-extra.md") && s.Contains("appended as order 2"));
        reassignments.ShouldContain(s => s.Contains("01c-more.md") && s.Contains("appended as order 3"));

        var feature = await LoadFeatureAsync("016-suffix-letters");
        var tasks = feature!.Artifacts.Where(a => a.Kind == PlanningArtifactKind.Task).OrderBy(a => a.Order).ToList();
        tasks.Count.ShouldBe(3);
        tasks[0].Order.ShouldBe(1);
        tasks[1].Order.ShouldBe(2);
        tasks[2].Order.ShouldBe(3);
        tasks[1].Body.ShouldContain("Extra task");
        tasks[2].Body.ShouldContain("More task");
    }

    [Fact]
    public async Task Import_SkipsTemplateAndFeaturelessFolders()
    {
        _repo.Write("_TEMPLATE/feature.md",
            """
            ---
            feature: 000
            slug: template
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 000 — Template
            """);
        _repo.Write("030-notes/readme.txt", "not a feature folder");
        _repo.Write("README.md", "stray top-level file");
        _repo.Write("040-valid/feature.md",
            """
            ---
            feature: 040
            slug: valid
            status: ready
            version: 1
            created: 2026-09-01
            ---

            # Feature 040 — Valid
            """);
        _repo.Write("040-valid/tasks/readme.md", "no order prefix");
        _repo.Write("040-valid/tasks/01-real.md",
            """
            ---
            feature: 040
            task: 01
            slug: valid
            status: todo
            review: light
            created: 2026-09-01
            ---

            # Real task
            """);

        var report = await ImportAsync(_repo.Root);

        report.FeaturesCreated.ShouldBe(1);
        report.ArtifactsCreated.ShouldBe(1);
        report.Skipped.ShouldContain(s => s.Contains("_TEMPLATE"));
        report.Skipped.ShouldContain(s => s.Contains("030-notes") && s.Contains("no feature.md"));
        report.Skipped.ShouldContain(s => s.EndsWith("README.md"));
        report.Skipped.ShouldContain(s => s.Contains("readme.md") && s.Contains("NN-"));

        var features = await _context.PlanningFeatures.Where(f => f.ProjectId == _projectId).ToListAsync();
        features.Count.ShouldBe(1);
        features.Single().Slug.ShouldBe("040-valid");
    }

    [Fact]
    public async Task Import_ReRunOnSameTree_ChangesNothing()
    {
        WriteFullFeature();
        await ImportAsync(_repo.Root);

        var second = await ImportAsync(_repo.Root);

        second.FeaturesCreated.ShouldBe(0);
        second.FeaturesUpdated.ShouldBe(1);
        second.ArtifactsCreated.ShouldBe(0);
        second.ArtifactsUpdated.ShouldBe(0);

        var feature = await LoadFeatureAsync("010-demo");
        feature!.Artifacts.Count.ShouldBe(4);
        feature.Artifacts.ShouldAllBe(a => a.Version == 1);
        feature.Version.ShouldBe(3);
    }

    [Fact]
    public async Task Import_ReRunAfterEdit_UpdatesWithNoteAndStaysStable()
    {
        WriteFullFeature();
        await ImportAsync(_repo.Root);

        _repo.Write("010-demo/tasks/01-first.md",
            """
            ---
            feature: 010
            task: 01
            slug: demo
            status: done
            review: hard
            created: 2026-09-01
            ---

            # Task one

            <goal>
            First task, edited on disk.
            </goal>

            <log>
            2026-09-01 — created from the plan.
            2026-09-02 — implemented and green.
            </log>
            """);

        var second = await ImportAsync(_repo.Root);

        second.ArtifactsUpdated.ShouldBe(1);
        second.ArtifactsCreated.ShouldBe(0);

        var feature = await LoadFeatureAsync("010-demo");
        feature!.Artifacts.Count.ShouldBe(4);
        var task = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task && a.Order == 1);
        task.Version.ShouldBe(2);
        task.Body.ShouldContain("edited on disk");
        task.Body.ShouldContain("re-imported from ");
        task.Body.Split("re-imported from ").Length.ShouldBe(2);

        var third = await ImportAsync(_repo.Root);
        third.ArtifactsUpdated.ShouldBe(0);

        feature = await LoadFeatureAsync("010-demo");
        task = feature!.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task && a.Order == 1);
        task.Version.ShouldBe(2);
        task.Body.Split("re-imported from ").Length.ShouldBe(2);
    }

    [Fact]
    public async Task Import_ArtifactOnlyInDatabase_KeptAndReported()
    {
        var featureService = new PlanningFeatureService(_context, _materializer, NullLogger<PlanningFeatureService>.Instance);
        var artifactService = new PlanningArtifactService(_context, _materializer, NullLogger<PlanningArtifactService>.Instance);
        var created = await featureService.CreateAsync(_projectId, "010-demo", "Old title", "old body");
        await artifactService.UpsertAsync(
            created.Id, PlanningArtifactKind.Task, "ui-created task", 5, null, null, CancellationToken.None);

        WriteFullFeature();

        var report = await ImportAsync(_repo.Root);

        report.Skipped.ShouldContain("kept: task 5 not present on disk");

        var feature = await LoadFeatureAsync("010-demo");
        feature.ShouldNotBeNull();
        feature.Title.ShouldBe("Demo feature title");
        feature!.Artifacts.Single(a => a.Order == 5).Body.ShouldBe("ui-created task");
    }

    [Fact]
    public async Task Import_RootResolution_AcceptsRepoRootAndBacklogFolder_RejectsOthers()
    {
        WriteFullFeature();

        var fromRoot = await ImportAsync(_repo.Root);
        fromRoot.FeaturesCreated.ShouldBe(1);

        var fromBacklogFolder = await ImportAsync(_repo.BacklogDir);
        fromBacklogFolder.FeaturesCreated.ShouldBe(0);
        fromBacklogFolder.FeaturesUpdated.ShouldBe(1);

        var emptyRoot = Path.Combine(Path.GetTempPath(), $"overmind-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyRoot);
        try
        {
            await Should.ThrowAsync<ArgumentException>(() => ImportAsync(emptyRoot));
            await Should.ThrowAsync<ArgumentException>(() => ImportAsync(Path.Combine(emptyRoot, "missing")));
        }
        finally
        {
            Directory.Delete(emptyRoot);
        }
    }

    private void WriteFullFeature()
    {
        _repo.Write("010-demo/feature.md",
            """
            ---
            feature: 010
            slug: demo
            status: ready
            version: 3
            created: 2026-09-01
            ---

            # Feature 010 — Demo feature title

            <goal>
            Build the demo.
            </goal>
            """);
        _repo.Write("010-demo/spec.md",
            """
            ---
            feature: 010
            slug: demo
            status: validated
            created: 2026-09-01
            validated: 2026-09-02 — user approved in chat
            ---

            # Spec 010 — Demo

            Spec body.
            """);
        _repo.Write("010-demo/plan.md",
            """
            ---
            feature: 010
            slug: demo
            status: draft
            created: 2026-09-01
            ---

            # Plan 010 — Demo

            Plan body.
            """);
        _repo.Write("010-demo/tasks/01-first.md",
            """
            ---
            feature: 010
            task: 01
            slug: demo
            status: done
            review: hard
            created: 2026-09-01
            ---

            # Task one

            <goal>
            First task.
            </goal>

            <log>
            2026-09-01 — created from the plan.
            2026-09-02 — implemented and green.
            </log>
            """);
        _repo.Write("010-demo/tasks/02-second.md",
            """
            ---
            feature: 010
            task: 02
            slug: demo
            status: in_progress
            review: light
            created: 2026-09-01
            ---

            # Task two

            <goal>
            Second task.
            </goal>
            """);
        _repo.Write("010-demo/requirements.md", "# Requirements\n\nIgnored by the importer.\n");
    }

    private void WriteResultFeature()
    {
        _repo.Write("011-result/feature.md",
            """
            ---
            feature: 011
            slug: result
            status: ready
            version: 1
            created: 2026-09-01
            ---

            # Feature 011 — Result round-trip
            """);
        _repo.Write("011-result/result.md",
            """
            ---
            feature: 011
            slug: result
            status: validated
            created: 2026-09-01
            validated: 2026-09-04 — reviewed against acceptance criteria
            ---

            # Result 011 — Result round-trip

            Ship summary.
            """);
    }

    private sealed class TempRepo : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"overmind-import-{Guid.NewGuid():N}");

        public string BacklogDir => Path.Combine(Root, "BACKLOG");

        public TempRepo()
        {
            Directory.CreateDirectory(BacklogDir);
        }

        public void Write(string relativePath, string content)
        {
            var path = Path.Combine(BacklogDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
