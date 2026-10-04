using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Overmind.Infrastructure.Search;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningFtsSyncTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly Guid _projectId;

    public PlanningFtsSyncTests()
    {
        using var ctx = _db.CreateContext();
        var project = Project.Create($"planning-fts-{Guid.NewGuid():N}", "/tmp/planning-fts");
        ctx.Projects.Add(project);
        _projectId = project.Id;
        ctx.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task FeatureInsert_BecomesSearchableInTitleAndBody()
    {
        await SeedFeatureAsync("narwhal feature", "planning body mentions capybara");

        (await FeatureHitsAsync("narwhal")).ShouldBe(1);
        (await FeatureHitsAsync("capybara")).ShouldBe(1);
    }

    [Fact]
    public async Task FeatureUpdate_OldTitleAndBodyGone_NewTermsFound()
    {
        var featureId = await SeedFeatureAsync("obsolete title axolotl", "obsolete body bandicoot");

        using (var ctx = _db.CreateContext())
        {
            var feature = await ctx.PlanningFeatures.SingleAsync(f => f.Id == featureId);
            feature.Update("renewed title chipmunk", "renewed body dormouse");
            await ctx.SaveChangesAsync();
        }

        (await FeatureHitsAsync("axolotl")).ShouldBe(0);
        (await FeatureHitsAsync("bandicoot")).ShouldBe(0);
        (await FeatureHitsAsync("chipmunk")).ShouldBe(1);
        (await FeatureHitsAsync("dormouse")).ShouldBe(1);
    }

    [Fact]
    public async Task FeatureDelete_NoLongerFound()
    {
        var featureId = await SeedFeatureAsync("doomed feature", "ermined body pangolin");

        using (var ctx = _db.CreateContext())
        {
            var feature = await ctx.PlanningFeatures.SingleAsync(f => f.Id == featureId);
            ctx.PlanningFeatures.Remove(feature);
            await ctx.SaveChangesAsync();
        }

        (await FeatureHitsAsync("pangolin")).ShouldBe(0);
    }

    [Fact]
    public async Task ArtifactInsert_BecomesSearchable()
    {
        var featureId = await SeedFeatureAsync("artifact host alpha", "host body");

        await SeedArtifactAsync(featureId, PlanningArtifactKind.Spec, "artifact body mentions meerkat");

        (await ArtifactHitsAsync("meerkat")).ShouldBe(1);
    }

    [Fact]
    public async Task ArtifactUpdate_OldBodyGone_NewBodyFound()
    {
        var featureId = await SeedFeatureAsync("artifact host beta", "host body");
        var artifactId = await SeedArtifactAsync(featureId, PlanningArtifactKind.Plan, "old body okapi");

        using (var ctx = _db.CreateContext())
        {
            var artifact = await ctx.PlanningArtifacts.SingleAsync(a => a.Id == artifactId);
            artifact.UpdateBody("new body porcupine");
            await ctx.SaveChangesAsync();
        }

        (await ArtifactHitsAsync("okapi")).ShouldBe(0);
        (await ArtifactHitsAsync("porcupine")).ShouldBe(1);
    }

    [Fact]
    public async Task ArtifactDelete_NoLongerFound()
    {
        var featureId = await SeedFeatureAsync("artifact host gamma", "host body");
        var artifactId = await SeedArtifactAsync(featureId, PlanningArtifactKind.Spec, "doomed body quoll");

        using (var ctx = _db.CreateContext())
        {
            var artifact = await ctx.PlanningArtifacts.SingleAsync(a => a.Id == artifactId);
            ctx.PlanningArtifacts.Remove(artifact);
            await ctx.SaveChangesAsync();
        }

        (await ArtifactHitsAsync("quoll")).ShouldBe(0);
    }

    [Fact]
    public async Task ArtifactSoftDelete_ExcludedFromSearchHits()
    {
        var featureId = await SeedFeatureAsync("artifact host delta", "host body");
        var artifactId = await SeedArtifactAsync(featureId, PlanningArtifactKind.Spec, "soft doomed body ibex");
        var materializer = Substitute.For<IPlanningMaterializer>();
        materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], [], []));

        (await ArtifactUnionHitsAsync("ibex")).ShouldBe(1);

        using (var ctx = _db.CreateContext())
        {
            var artifacts = new PlanningArtifactService(
                ctx,
                TestPlanningDispatcher.Create(ctx, materializer),
                new TaskDependencyService(ctx, TestPlanningDispatcher.Create(ctx, materializer)));
            await artifacts.DeleteAsync(artifactId, byHuman: true);
        }

        (await ArtifactUnionHitsAsync("ibex")).ShouldBe(0);
    }

    [Fact]
    public async Task ImportThroughFeatureService_InsertsBecomeSearchable()
    {
        using var ctx = _db.CreateContext();
        var materializer = Substitute.For<IPlanningMaterializer>();
        materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], [], []));
        materializer.ClearAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(Guid.Empty, PlanningMaterialization.Off, [], [], []));

        var features = new PlanningFeatureService(
            ctx,
            materializer,
            new BacklogImportSeeder(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx))),
            new TaskDependencyService(ctx, TestPlanningDispatcher.Create(ctx, materializer)),
            TestPlanningDispatcher.Create(ctx, materializer),
            NullLogger<PlanningFeatureService>.Instance);

        var now = DateTime.UtcNow;
        var result = await features.ImportAsync(
            _projectId,
            new BacklogFeatureImport(
                "070-import-fts",
                "Import fts probe",
                "<goal>import body tapir</goal>",
                PlanningFeatureStatus.Ready,
                FeaturePriority.P2,
                1,
                now,
                [],
                [new BacklogArtifactImport(
                    PlanningArtifactKind.Spec,
                    0,
                    PlanningArtifactStatus.Draft,
                    PlanningReviewLevel.Light,
                    "<goal>spec body wallaby</goal>",
                    null,
                    now,
                    "BACKLOG/070-import-fts/spec.md",
                    "sha256-placeholder")],
                []),
            CancellationToken.None);

        result.Created.ShouldBeTrue();
        result.ArtifactsCreated.ShouldBe(1);
        (await FeatureHitsAsync("tapir")).ShouldBe(1);
        (await ArtifactHitsAsync("wallaby")).ShouldBe(1);
    }

    private async Task<Guid> SeedFeatureAsync(string title, string body)
    {
        using var ctx = _db.CreateContext();
        var feature = PlanningFeature.Create(_projectId, $"fts-{Guid.NewGuid():N}", title, body);
        ctx.PlanningFeatures.Add(feature);
        await ctx.SaveChangesAsync();
        return feature.Id;
    }

    private async Task<Guid> SeedArtifactAsync(Guid featureId, PlanningArtifactKind kind, string body)
    {
        using var ctx = _db.CreateContext();
        var artifact = PlanningArtifact.Create(featureId, kind, body);
        ctx.PlanningArtifacts.Add(artifact);
        await ctx.SaveChangesAsync();
        return artifact.Id;
    }

    private async Task<long> FeatureHitsAsync(string term)
    {
        using var ctx = _db.CreateContext();
        return await PlanningFtsProbe.CountAsync(
            ctx, "SELECT COUNT(*) FROM feature_fts WHERE feature_fts MATCH $term", term);
    }

    private async Task<long> ArtifactHitsAsync(string term)
    {
        using var ctx = _db.CreateContext();
        return await PlanningFtsProbe.CountAsync(
            ctx, "SELECT COUNT(*) FROM artifact_fts WHERE artifact_fts MATCH $term", term);
    }

    private async Task<int> ArtifactUnionHitsAsync(string term)
    {
        await using var ctx = _db.CreateContext();
        var search = new FtsSearchService(ctx);
        var hits = await search.SearchUnionAsync(null, term, 10, CancellationToken.None);
        return hits.Count(h => h is ArtifactFulltextHit);
    }
}
