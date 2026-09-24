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
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], [], []));
        _materializer.ClearAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], [], []));

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
        new(
            new PlanningFeatureService(
                _context,
                _materializer,
                new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
                NullLogger<PlanningFeatureService>.Instance),
            new FrontmatterKeyRegistry(_context, NullLogger<FrontmatterKeyRegistry>.Instance));

    private Task<BacklogImportReport> ImportAsync(string path) =>
        CreateImporter().ImportAsync(new BacklogImportRequest(path, _projectId), CancellationToken.None);

    private Task<BacklogFeatureImportReport> ImportFeatureAsync(string path) =>
        CreateImporter().ImportFeatureAsync(_projectId, _repo.Root, path, CancellationToken.None);

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
    public async Task ImportFeature_SingleFolder_RestoresFeatureAndArtifactsInOneCall()
    {
        WriteFullFeature();

        var report = await ImportFeatureAsync(Path.Combine(_repo.Root, "BACKLOG", "010-demo"));

        report.Slug.ShouldBe("010-demo");
        report.Title.ShouldBe("Demo feature title");
        report.Status.ShouldBe(PlanningFeatureStatus.Ready);
        report.Created.ShouldBeTrue();
        report.ArtifactsCreated.ShouldBe(4);
        report.ArtifactsUpdated.ShouldBe(0);
        report.TrackedFiles.ShouldBe(5);
        report.Materialization.ShouldBe(PlanningMaterialization.On);
        report.NextStep.ShouldBe("5 files tracked, materialize is a no-op");

        var feature = await LoadFeatureAsync("010-demo");
        feature.ShouldNotBeNull();
        feature.Version.ShouldBe(3);
        feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Spec).Status
            .ShouldBe(PlanningArtifactStatus.Validated);
        var tasks = feature.Artifacts.Where(a => a.Kind == PlanningArtifactKind.Task).OrderBy(a => a.Order).ToList();
        tasks[0].Status.ShouldBe(PlanningArtifactStatus.Done);
        tasks[1].Status.ShouldBe(PlanningArtifactStatus.InProgress);
    }

    [Fact]
    public async Task ImportFeature_ReimportAfterStatusDrift_RestoresStatusWithImportMarker()
    {
        WriteFullFeature();
        await ImportFeatureAsync(Path.Combine(_repo.Root, "BACKLOG", "010-demo"));

        var feature = await LoadFeatureAsync("010-demo");
        var task = feature!.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task && a.Order == 2);
        task.SetStatus(PlanningArtifactStatus.Blocked, "hit a wall mid-task", PlanningLogSource.Agent);
        await _context.SaveChangesAsync();

        var report = await ImportFeatureAsync(Path.Combine(_repo.Root, "BACKLOG", "010-demo"));

        report.Created.ShouldBeFalse();
        report.ArtifactsCreated.ShouldBe(0);

        feature = await LoadFeatureAsync("010-demo");
        task = feature!.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task && a.Order == 2);
        task.Status.ShouldBe(PlanningArtifactStatus.InProgress);
        task.Body.ShouldContain("[import] — status restored from imported file");
    }

    [Fact]
    public async Task ImportFeature_PathOutsideProjectRoot_Rejected()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"overmind-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(outside, "BACKLOG", "058-elsewhere"));
        try
        {
            var folder = Path.Combine(outside, "BACKLOG", "058-elsewhere");

            var ex = await Should.ThrowAsync<PlanningException>(() => ImportFeatureAsync(folder));

            ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
            ex.Message.ShouldContain("resolves outside the project directory");
            (await LoadFeatureAsync("058-elsewhere")).ShouldBeNull();
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task ImportFeature_RelativePath_Rejected()
    {
        WriteFullFeature();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => ImportFeatureAsync(Path.Combine("BACKLOG", "010-demo")));

        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Message.ShouldContain("is not absolute");
        (await LoadFeatureAsync("010-demo")).ShouldBeNull();
    }

    [Fact]
    public async Task ImportFeature_FolderWithoutFeatureMd_Rejected()
    {
        _repo.Write("099-bare/notes.md", "not a feature folder");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => ImportFeatureAsync(Path.Combine(_repo.Root, "BACKLOG", "099-bare")));

        ex.Message.ShouldContain("holds no feature.md");
        (await LoadFeatureAsync("099-bare")).ShouldBeNull();
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
    public async Task Import_ReviewFeatureStatus_MappedWithoutSkip()
    {
        _repo.Write("061-review/feature.md",
            """
            ---
            feature: 061
            slug: review
            status: review
            version: 1
            created: 2026-09-01
            ---

            # Feature 061 — Review

            Body under review.
            """);

        var report = await ImportAsync(_repo.Root);

        report.Skipped.ShouldBeEmpty();
        var feature = await LoadFeatureAsync("061-review");
        feature.ShouldNotBeNull();
        feature.Status.ShouldBe(PlanningFeatureStatus.Review);
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
    public async Task Import_LetterSuffixedTask_ReImportAfterMaterializerWrotePlainNames_CreatesNoDuplicate()
    {
        _repo.Write("017-suffix-cycle/feature.md",
            """
            ---
            feature: 017
            slug: suffix-cycle
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 017 — Suffix cycle
            """);
        _repo.Write("017-suffix-cycle/tasks/01-first.md",
            """
            ---
            feature: 017
            task: 01
            slug: suffix-cycle
            status: todo
            review: light
            created: 2026-09-01
            ---

            # First task
            """);
        _repo.Write("017-suffix-cycle/tasks/01b-extra.md",
            """
            ---
            feature: 017
            task: 01b
            slug: suffix-cycle
            status: todo
            review: light
            created: 2026-09-01
            ---

            # Extra task
            """);

        var first = await ImportAsync(_repo.Root);

        first.ArtifactsCreated.ShouldBe(2);
        first.Skipped.ShouldContain(
            s => s.Contains("reassigned:") && s.Contains("01b-extra.md") && s.Contains("appended as order 2"));

        _repo.Write("017-suffix-cycle/tasks/02-extra.md",
            """
            ---
            feature: 017
            task: 02
            slug: suffix-cycle
            status: todo
            review: light
            created: 2026-09-01
            ---

            # Extra task
            """);

        var second = await ImportAsync(_repo.Root);

        second.FeaturesCreated.ShouldBe(0);
        second.ArtifactsCreated.ShouldBe(0);
        second.ArtifactsUpdated.ShouldBe(0);
        second.Skipped.ShouldContain(s => s.Contains("matched:") && s.Contains("01b-extra.md"));
        second.Skipped.ShouldNotContain(s => s.Contains("reassigned:"));

        var feature = await LoadFeatureAsync("017-suffix-cycle");
        var tasks = feature!.Artifacts.Where(a => a.Kind == PlanningArtifactKind.Task).OrderBy(a => a.Order).ToList();
        tasks.Count.ShouldBe(2);
        tasks[0].Order.ShouldBe(1);
        tasks[1].Order.ShouldBe(2);
        tasks[1].Body.ShouldContain("Extra task");

        var third = await ImportAsync(_repo.Root);

        third.ArtifactsCreated.ShouldBe(0);
        third.ArtifactsUpdated.ShouldBe(0);

        feature = await LoadFeatureAsync("017-suffix-cycle");
        feature!.Artifacts.Count(a => a.Kind == PlanningArtifactKind.Task).ShouldBe(2);
    }

    [Fact]
    public async Task Import_LetterSuffixedTask_ReImportAfterCeremonyStatusMove_CreatesNoDuplicate()
    {
        _repo.Write("018-suffix-ceremony/feature.md",
            """
            ---
            feature: 018
            slug: suffix-ceremony
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 018 — Suffix ceremony
            """);
        _repo.Write("018-suffix-ceremony/tasks/01-first.md",
            """
            ---
            feature: 018
            task: 01
            slug: suffix-ceremony
            status: todo
            review: light
            created: 2026-09-01
            ---

            # First task
            """);
        _repo.Write("018-suffix-ceremony/tasks/01b-extra.md",
            """
            ---
            feature: 018
            task: 01b
            slug: suffix-ceremony
            status: todo
            review: light
            created: 2026-09-01
            ---

            # Extra task
            """);

        var first = await ImportAsync(_repo.Root);

        first.ArtifactsCreated.ShouldBe(2);

        var feature = await LoadFeatureAsync("018-suffix-ceremony");
        var appended = feature!.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task && a.Order == 2);
        appended.SetStatus(PlanningArtifactStatus.InProgress, "started during ceremony");
        await _context.SaveChangesAsync();

        var bodyAfterCeremony = appended.Body;
        _repo.Write("018-suffix-ceremony/tasks/02-extra.md",
            "---\n" +
            "feature: 018\n" +
            "task: 02\n" +
            "slug: suffix-ceremony\n" +
            "status: inprogress\n" +
            "review: light\n" +
            "created: 2026-09-01\n" +
            "---\n\n" +
            bodyAfterCeremony);

        var second = await ImportAsync(_repo.Root);

        second.ArtifactsCreated.ShouldBe(0);
        second.ArtifactsUpdated.ShouldBe(0);
        second.Skipped.ShouldContain(s => s.Contains("matched:") && s.Contains("01b-extra.md"));
        second.Skipped.ShouldNotContain(s => s.Contains("reassigned:"));

        feature = await LoadFeatureAsync("018-suffix-ceremony");
        var tasks = feature!.Artifacts.Where(a => a.Kind == PlanningArtifactKind.Task).OrderBy(a => a.Order).ToList();
        tasks.Count.ShouldBe(2);
        tasks[1].Order.ShouldBe(2);
        tasks[1].Status.ShouldBe(PlanningArtifactStatus.InProgress);
        tasks[1].Body.ShouldBe(bodyAfterCeremony);
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
    public async Task Import_FeatureStatusChangedOnDisk_WritesImportLine()
    {
        WriteFullFeature();
        await ImportAsync(_repo.Root);

        _repo.Write("010-demo/feature.md",
            """
            ---
            feature: 010
            slug: demo
            status: in_progress
            version: 3
            created: 2026-09-01
            ---

            # Feature 010 — Demo feature title

            <goal>
            Build the demo.
            </goal>
            """);
        await ImportAsync(_repo.Root);

        var feature = await LoadFeatureAsync("010-demo");
        feature!.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        var line = PlanningLog.Parse(feature.Body).Single();
        line.Source.ShouldBe(PlanningLogSource.Import);
        line.Text.ShouldBe("status restored from imported file");
    }

    [Fact]
    public async Task Import_ArtifactStatusChangedOnDisk_WritesImportLine()
    {
        WriteFullFeature();
        await ImportAsync(_repo.Root);

        _repo.Write("010-demo/tasks/01-first.md",
            """
            ---
            feature: 010
            task: 01
            slug: demo
            status: blocked
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
        await ImportAsync(_repo.Root);

        var feature = await LoadFeatureAsync("010-demo");
        var task = feature!.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task && a.Order == 1);
        task.Status.ShouldBe(PlanningArtifactStatus.Blocked);
        var line = PlanningLog.Parse(task.Body).Single();
        line.Source.ShouldBe(PlanningLogSource.Import);
        line.Text.ShouldBe("status restored from imported file");
    }

    [Fact]
    public async Task Import_MixedLegacyAndSourcedLogLines_SurviveRoundTripWithoutCorruptionOrDuplication()
    {
        _repo.Write("028-markers/feature.md",
            """
            ---
            feature: 028
            slug: markers
            status: done
            version: 2
            created: 2026-09-01
            ---

            # Feature 028 — Marker round-trip

            <goal>
            Survive the importer.
            </goal>

            <log>
            - 2026-09-01 — created from the plan.
            - 2026-09-02 [agent] — spec written and user-approved.
            - 2026-09-03 [auto] — all tasks done + spec + plan → review.
            - 2026-09-04 [ui] — status moved to 'done' via ui.
            </log>
            """);
        _repo.Write("028-markers/tasks/01-marked.md",
            """
            ---
            feature: 028
            task: 01
            slug: markers
            status: done
            review: light
            created: 2026-09-01
            ---

            # Marked task

            <goal>
            Do the work.
            </goal>

            <log>
            - 2026-09-01 — created from the plan.
            - 2026-09-02 [agent] — implemented and green.
            - 2026-09-03 [import] — status restored from imported file.
            </log>
            """);

        var first = await ImportAsync(_repo.Root);

        first.FeaturesCreated.ShouldBe(1);
        first.ArtifactsCreated.ShouldBe(1);
        first.Skipped.ShouldBeEmpty();

        var feature = await LoadFeatureAsync("028-markers");
        feature.ShouldNotBeNull();
        feature!.Status.ShouldBe(PlanningFeatureStatus.Done);

        var featureLines = PlanningLog.Parse(feature.Body);
        featureLines.Count.ShouldBe(4);
        featureLines[0].Date.ShouldBe(new DateOnly(2026, 9, 1));
        featureLines[0].Source.ShouldBeNull();
        featureLines[0].Text.ShouldBe("created from the plan.");
        featureLines[1].Source.ShouldBe(PlanningLogSource.Agent);
        featureLines[1].Text.ShouldBe("spec written and user-approved.");
        featureLines[2].Source.ShouldBe(PlanningLogSource.Auto);
        featureLines[2].Text.ShouldBe("all tasks done + spec + plan → review.");
        featureLines[3].Source.ShouldBe(PlanningLogSource.Ui);
        featureLines[3].Text.ShouldBe("status moved to 'done' via ui.");

        var task = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task);
        task.Status.ShouldBe(PlanningArtifactStatus.Done);
        var taskLines = PlanningLog.Parse(task.Body);
        taskLines.Count.ShouldBe(3);
        taskLines[0].Source.ShouldBeNull();
        taskLines[1].Source.ShouldBe(PlanningLogSource.Agent);
        taskLines[2].Source.ShouldBe(PlanningLogSource.Import);

        var featureBodyAfterFirst = feature.Body;
        var taskBodyAfterFirst = task.Body;

        var second = await ImportAsync(_repo.Root);

        second.FeaturesCreated.ShouldBe(0);
        second.FeaturesUpdated.ShouldBe(1);
        second.ArtifactsCreated.ShouldBe(0);
        second.ArtifactsUpdated.ShouldBe(0);

        feature = await LoadFeatureAsync("028-markers");
        feature.ShouldNotBeNull();
        feature!.Status.ShouldBe(PlanningFeatureStatus.Done);
        feature.Version.ShouldBe(2);
        feature.Body.ShouldBe(featureBodyAfterFirst);
        feature.Body.ShouldNotContain("re-imported from ");

        var reloadedTask = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task);
        reloadedTask.Version.ShouldBe(1);
        reloadedTask.Body.ShouldBe(taskBodyAfterFirst);
        reloadedTask.Body.ShouldNotContain("re-imported from ");
    }

    [Fact]
    public async Task Import_ArtifactOnlyInDatabase_KeptAndReported()
    {
        var featureService = new PlanningFeatureService(
            _context,
            _materializer,
            new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
            NullLogger<PlanningFeatureService>.Instance);
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

    [Fact]
    public async Task Import_FeatureHeadingColonSeparator_TitleStripped()
    {
        _repo.Write("020-colon/feature.md",
            """
            ---
            feature: 020
            slug: colon
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 020: Colon title
            """);

        var report = await ImportAsync(_repo.Root);

        report.FeaturesCreated.ShouldBe(1);

        var feature = await LoadFeatureAsync("020-colon");
        feature.ShouldNotBeNull();
        feature.Title.ShouldBe("Colon title");
    }

    [Fact]
    public async Task Import_FeatureHeadingNestedPrefixes_TitleFullyStripped()
    {
        _repo.Write("022-nested/feature.md",
            """
            ---
            feature: 022
            slug: nested
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 022 — Feature 022: Nested title
            """);

        await ImportAsync(_repo.Root);

        var feature = await LoadFeatureAsync("022-nested");
        feature.ShouldNotBeNull();
        feature.Title.ShouldBe("Nested title");
    }

    [Fact]
    public async Task Import_DuplicateTaskOrder_FirstAlphabeticalImportsLoserSkippedAndReported()
    {
        _repo.Write("019-dup/feature.md",
            """
            ---
            feature: 019
            slug: dup
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 019 — Dup
            """);
        _repo.Write("019-dup/tasks/01-a.md",
            """
            ---
            feature: 019
            task: 01
            slug: dup
            status: todo
            review: light
            created: 2026-09-01
            ---

            # Alpha task
            """);
        _repo.Write("019-dup/tasks/01-b.md",
            """
            ---
            feature: 019
            task: 01
            slug: dup
            status: todo
            review: light
            created: 2026-09-01
            ---

            # Beta task
            """);

        var report = await ImportAsync(_repo.Root);

        report.ArtifactsCreated.ShouldBe(1);
        report.Skipped.ShouldContain(
            s => s.Contains("01-b.md") && s.Contains("duplicate order 01") && s.Contains("01-a.md"));

        var feature = await LoadFeatureAsync("019-dup");
        feature.ShouldNotBeNull();
        var tasks = feature.Artifacts.Where(a => a.Kind == PlanningArtifactKind.Task).ToList();
        tasks.Count.ShouldBe(1);
        tasks.Single().Body.ShouldContain("Alpha task");
    }

    [Fact]
    public async Task Import_MissingCreatedToken_FallsBackToFileMtimeAndReports()
    {
        _repo.Write("023-mtime/feature.md",
            """
            ---
            feature: 023
            slug: mtime
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 023 — Mtime
            """);
        _repo.Write("023-mtime/tasks/01-dated.md",
            """
            ---
            feature: 023
            task: 01
            slug: mtime
            status: todo
            review: light
            ---

            # Dated task
            """);

        var modifiedAt = new DateTime(2026, 8, 15, 12, 30, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path.Combine(_repo.BacklogDir, "023-mtime/tasks/01-dated.md"), modifiedAt);

        var report = await ImportAsync(_repo.Root);

        var feature = await LoadFeatureAsync("023-mtime");
        feature.ShouldNotBeNull();
        var task = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task);
        task.CreatedAt.ShouldBe(modifiedAt);

        var relative = Path.Combine("BACKLOG", "023-mtime", "tasks", "01-dated.md");
        report.CreatedFallbacks.ShouldContain(
            entry => entry.Contains(relative) && entry.Contains("file mtime"));
        report.CreatedFallbacks.ShouldNotContain(entry => entry.Contains("feature.md"));
    }

    [Fact]
    public async Task Import_ExplicitCreatedToken_DoesNotReportCreatedFallback()
    {
        WriteFullFeature();

        var report = await ImportAsync(_repo.Root);

        report.CreatedFallbacks.ShouldBeEmpty();
    }

    [Fact]
    public async Task Import_UnknownFrontmatterKeys_PreservedOnArtifactAndListedPerFile()
    {
        _repo.Write("024-keys/feature.md",
            """
            ---
            feature: 024
            slug: keys
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 024 — Keys
            """);
        _repo.Write("024-keys/tasks/01-key.md",
            """
            ---
            feature: 024
            task: 01
            slug: keys
            status: todo
            review: light
            created: 2026-09-01
            depends_on: 02-other
            risk_level: high
            ---

            # Key task
            """);

        var report = await ImportAsync(_repo.Root);

        var feature = await LoadFeatureAsync("024-keys");
        feature.ShouldNotBeNull();
        var task = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task);
        task.PreservedFrontmatter.ShouldBe("{\"depends_on\":\"02-other\",\"risk_level\":\"high\"}");
        var preserved = PreservedFrontmatterJson.Deserialize(task.PreservedFrontmatter)!;
        preserved["depends_on"].ShouldBe("02-other");
        preserved["risk_level"].ShouldBe("high");

        var relative = Path.Combine("BACKLOG", "024-keys", "tasks", "01-key.md");
        report.Preserved.ShouldContain(
            entry => entry.Contains(relative)
                && entry.Contains("depends_on")
                && entry.Contains("risk_level"));
        report.Skipped.ShouldNotContain(entry => entry.Contains("depends_on") || entry.Contains("risk_level"));
    }

    [Fact]
    public async Task Import_UnknownFrontmatterKeys_RegisteredWithColorAndNewKeysInReport()
    {
        _repo.Write("025-reg/feature.md",
            """
            ---
            feature: 025
            slug: reg
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 025 — Reg
            """);
        _repo.Write("025-reg/tasks/01-reg.md",
            """
            ---
            feature: 025
            task: 01
            slug: reg
            status: todo
            review: light
            created: 2026-09-01
            depends_on: 02-other
            risk_level: high
            ---

            # Reg task
            """);

        var report = await ImportAsync(_repo.Root);

        var seeded = await _context.FrontmatterKeys.SingleAsync(k => k.Name == "depends_on");
        seeded.Color.ShouldBe("#fb923c");

        var registered = await _context.FrontmatterKeys.SingleAsync(k => k.Name == "risk_level");
        registered.Color.ShouldBe(FrontmatterKeyPalette.ColorFor("risk_level"));

        report.NewKeys.ShouldContain(
            key => key.Name == "risk_level" && key.Color == registered.Color);
        report.NewKeys.ShouldNotContain(key => key.Name == "depends_on");
    }

    [Fact]
    public async Task Import_StructuralKeysOnly_PreservedFrontmatterStaysNull()
    {
        WriteFullFeature();

        await ImportAsync(_repo.Root);

        var feature = await LoadFeatureAsync("010-demo");
        feature.ShouldNotBeNull();
        feature.Artifacts.ShouldAllBe(a => a.PreservedFrontmatter == null);
    }

    [Fact]
    public async Task Import_ReRunWithSameUnknownKeys_PreservedStableAndNoNewKeys()
    {
        WriteUnknownKeyFeature();
        await ImportAsync(_repo.Root);

        var second = await ImportAsync(_repo.Root);

        second.ArtifactsUpdated.ShouldBe(0);
        second.NewKeys.ShouldBeEmpty();

        var feature = await LoadFeatureAsync("026-stable");
        feature.ShouldNotBeNull();
        var task = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task);
        task.Version.ShouldBe(1);
        task.PreservedFrontmatter.ShouldBe("{\"risk_level\":\"low\"}");
    }

    [Fact]
    public async Task Import_ChangedUnknownKeyValue_UpdatesPreservedColumnWithoutBodyChange()
    {
        WriteUnknownKeyFeature();
        await ImportAsync(_repo.Root);

        _repo.Write("026-stable/tasks/01-stable.md",
            """
            ---
            feature: 026
            task: 01
            slug: stable
            status: todo
            review: light
            created: 2026-09-01
            risk_level: high
            ---

            # Stable task
            """);

        var second = await ImportAsync(_repo.Root);

        second.ArtifactsUpdated.ShouldBe(1);

        var feature = await LoadFeatureAsync("026-stable");
        feature.ShouldNotBeNull();
        var task = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task);
        task.Version.ShouldBe(1);
        task.PreservedFrontmatter.ShouldBe("{\"risk_level\":\"high\"}");
    }

    [Fact]
    public async Task Import_NoFrontmatterKeySilentlyDropped_ReportListsEveryUnknownKey()
    {
        _repo.Write("027-complete/feature.md",
            """
            ---
            feature: 027
            slug: complete
            status: draft
            version: 1
            created: 2026-09-01
            audience: internal
            ---

            # Feature 027 — Complete
            """);
        _repo.Write("027-complete/spec.md",
            """
            ---
            feature: 027
            slug: complete
            status: draft
            created: 2026-09-01
            phase: 2
            decision: Use SQLite
            ---

            # Spec 027 — Complete

            Spec body.
            """);

        var report = await ImportAsync(_repo.Root);

        var feature = await LoadFeatureAsync("027-complete");
        feature.ShouldNotBeNull();
        var spec = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Spec);
        var preserved = PreservedFrontmatterJson.Deserialize(spec.PreservedFrontmatter)!;
        preserved["phase"].ShouldBe("2");
        preserved["decision"].ShouldBe("Use SQLite");

        var unknownKeys = new[] { "audience", "phase", "decision" };
        foreach (var key in unknownKeys)
        {
            report.Preserved.ShouldContain(entry => entry.Contains(key));
        }

        var registered = await _context.FrontmatterKeys
            .Where(k => unknownKeys.Contains(k.Name))
            .Select(k => k.Name)
            .ToListAsync();
        registered.ShouldBe(unknownKeys, ignoreOrder: true);
    }

    private void WriteUnknownKeyFeature()
    {
        _repo.Write("026-stable/feature.md",
            """
            ---
            feature: 026
            slug: stable
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 026 — Stable
            """);
        _repo.Write("026-stable/tasks/01-stable.md",
            """
            ---
            feature: 026
            task: 01
            slug: stable
            status: todo
            review: light
            created: 2026-09-01
            risk_level: low
            ---

            # Stable task
            """);
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
