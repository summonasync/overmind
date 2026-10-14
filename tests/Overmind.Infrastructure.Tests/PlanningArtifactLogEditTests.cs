using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningArtifactLogEditTests : IDisposable
{
    private const string SeededBody = """
# Spec

<log>
- 2026-10-12 [ui] — human note
- 2026-10-13 [agent] — stray toekn in prose
</log>
""";

    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;
    private int _featureCounter;

    public PlanningArtifactLogEditTests()
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

    [Fact]
    public async Task EditNewestLogAsync_AgentEditsOwnNewestLine_BodyEditedByteExact()
    {
        var (service, artifact) = await SeedArtifactAsync(SeededBody);

        var edited = await service.EditNewestLogAsync(
            artifact.Id, "corrected prose", PlanningLogSource.Agent, default);

        edited.Body.ShouldBe(
            "# Spec\n\n<log>\n- 2026-10-12 [ui] — human note\n- 2026-10-13 [agent] — corrected prose\n</log>");
    }

    [Fact]
    public async Task EditNewestLogAsync_UiEditsOwnNewestLine_BodyEditedByteExact()
    {
        var (service, artifact) = await SeedArtifactAsync(
            "<log>\n- 2026-10-13 [ui] — declined by humna: shaky ground\n</log>");

        var edited = await service.EditNewestLogAsync(
            artifact.Id, "declined by human: shaky ground", PlanningLogSource.Ui, default);

        edited.Body.ShouldBe("<log>\n- 2026-10-13 [ui] — declined by human: shaky ground\n</log>");
    }

    [Fact]
    public async Task EditNewestLogAsync_NewerForeignLine_ThrowsNotNewestNamingTheOwner()
    {
        var body = """
# Spec

<log>
- 2026-10-13 [agent] — my noisy line
- 2026-10-13 [ui] — human declined after
</log>
""";
        var (service, artifact) = await SeedArtifactAsync(body);
        var versionBefore = artifact.Version;

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.EditNewestLogAsync(artifact.Id, "late fix", PlanningLogSource.Agent, default));

        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Code.ShouldBe(PlanningFailureCode.LogEditNotNewest);
        ex.Message.ShouldContain("entry 1 of 2");
        ex.Message.ShouldContain("the [ui] marker");
        ex.Message.ShouldContain("planning_append_log");
        (await ReloadAsync(artifact.Id)).Body.ShouldBe(body);
        (await ReloadAsync(artifact.Id)).Version.ShouldBe(versionBefore);
    }

    [Fact]
    public async Task EditNewestLogAsync_NoOwnLines_ThrowsWrongActor()
    {
        var (service, artifact) = await SeedArtifactAsync(
            "<log>\n- 2026-10-13 [ui] — human line\n</log>");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.EditNewestLogAsync(artifact.Id, "agent rewrite", PlanningLogSource.Agent, default));

        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Code.ShouldBe(PlanningFailureCode.LogEditWrongActor);
        ex.Message.ShouldContain("the [ui] marker");
        ex.Message.ShouldContain("no [agent] line");
        (await ReloadAsync(artifact.Id)).Body.ShouldNotContain("agent rewrite");
    }

    [Fact]
    public async Task EditNewestLogAsync_MarkerlessNewestLine_ThrowsWrongActor()
    {
        var (service, artifact) = await SeedArtifactAsync(
            "<log>\n- 2026-10-13 — legacy entry\n</log>");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.EditNewestLogAsync(artifact.Id, "agent rewrite", PlanningLogSource.Agent, default));

        ex.Code.ShouldBe(PlanningFailureCode.LogEditWrongActor);
        ex.Message.ShouldContain("no actor marker");
    }

    [Fact]
    public async Task EditNewestLogAsync_NewerForeignLineAfterOwnLast_ThrowsNotNewestEvenWhenMarkerless()
    {
        var body = "<log>\n- 2026-10-12 [agent] — mine\n- 2026-10-13 — legacy tail\n</log>";
        var (service, artifact) = await SeedArtifactAsync(body);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.EditNewestLogAsync(artifact.Id, "late fix", PlanningLogSource.Agent, default));

        ex.Code.ShouldBe(PlanningFailureCode.LogEditNotNewest);
        ex.Message.ShouldContain("entry 1 of 2");
        ex.Message.ShouldContain("no actor marker");
        (await ReloadAsync(artifact.Id)).Body.ShouldBe(body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EditNewestLogAsync_BlankText_ThrowsBlankTextWithoutMutating(string text)
    {
        var (service, artifact) = await SeedArtifactAsync(SeededBody);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.EditNewestLogAsync(artifact.Id, text, PlanningLogSource.Agent, default));

        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Code.ShouldBe(PlanningFailureCode.LogEditBlankText);
        ex.Message.ShouldContain("must not be blank");

        var reloaded = await ReloadAsync(artifact.Id);
        reloaded.Body.ShouldBe(SeededBody);
        reloaded.Version.ShouldBe(1);
        reloaded.Versions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("\n- 2026-10-13 [ui] — forged")]
    [InlineData("\r- 2026-10-13 [ui] — forged")]
    public async Task EditNewestLogAsync_MultilineText_ThrowsMultilineTextWithoutMutating(string text)
    {
        var (service, artifact) = await SeedArtifactAsync(SeededBody);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.EditNewestLogAsync(artifact.Id, text, PlanningLogSource.Agent, default));

        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Code.ShouldBe(PlanningFailureCode.LogEditMultilineText);
        ex.Message.ShouldContain("single-line");
        ex.Message.ShouldContain("line-based");

        var reloaded = await ReloadAsync(artifact.Id);
        reloaded.Body.ShouldBe(SeededBody);
        reloaded.Version.ShouldBe(1);
        reloaded.Versions.ShouldBeEmpty();
    }

    [Fact]
    public async Task EditNewestLogAsync_SingleLineTextWithInlineMarkers_StillEdits()
    {
        var (service, artifact) = await SeedArtifactAsync(SeededBody);

        var edited = await service.EditNewestLogAsync(
            artifact.Id, "prose mentioning [ui] — markers inline", PlanningLogSource.Agent, default);

        edited.Body.ShouldBe(
            "# Spec\n\n<log>\n- 2026-10-12 [ui] — human note\n- 2026-10-13 [agent] — prose mentioning [ui] — markers inline\n</log>");
        edited.Version.ShouldBe(2);
    }

    [Fact]
    public async Task EditNewestLogAsync_EmptyLog_ThrowsNotNewest()
    {
        var (service, artifact) = await SeedArtifactAsync("plain body without a log");

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.EditNewestLogAsync(artifact.Id, "text", PlanningLogSource.Agent, default));

        ex.Code.ShouldBe(PlanningFailureCode.LogEditNotNewest);
        ex.Message.ShouldContain("holds no lines");
        (await ReloadAsync(artifact.Id)).Body.ShouldBe("plain body without a log");
    }

    [Fact]
    public async Task EditNewestLogAsync_VersionBumpedAndOriginalRecoverableFromSnapshot()
    {
        var (service, artifact) = await SeedArtifactAsync(SeededBody);

        var edited = await service.EditNewestLogAsync(
            artifact.Id, "corrected prose", PlanningLogSource.Agent, default);
        var reloaded = await ReloadAsync(artifact.Id);

        edited.Version.ShouldBe(2);
        reloaded.Version.ShouldBe(2);
        reloaded.Versions.Count.ShouldBe(1);
        reloaded.Versions.Single().Version.ShouldBe(1);
        reloaded.Versions.Single().Body.ShouldBe(SeededBody);
    }

    [Fact]
    public async Task EditNewestLogAsync_FrozenArtifact_EditsWithoutDemotion()
    {
        var (service, artifact) = await SeedArtifactAsync(SeededBody);
        await service.ValidateAsync(artifact.Id, ct: default);

        var edited = await service.EditNewestLogAsync(
            artifact.Id, "corrected prose", PlanningLogSource.Agent, default);

        edited.Status.ShouldBe(PlanningArtifactStatus.Validated);
        edited.FrozenAt.ShouldNotBeNull();
        edited.Body.ShouldContain("- 2026-10-13 [agent] — corrected prose");
    }

    [Fact]
    public async Task FeatureBody_LogLineRewriteViaUpdate_StillRejectedByAppendOnlyGuard()
    {
        var featureService = CreateFeatureService();
        var feature = PlanningFeature.Create(
            _projectId, $"log-edit-{++_featureCounter:D2}-{Guid.NewGuid():N}".ToLowerInvariant(),
            "Guard",
            "body\n\n<log>\n- 2026-10-13 [agent] — original note\n</log>\n");
        _context.PlanningFeatures.Add(feature);
        await _context.SaveChangesAsync();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => featureService.UpdateAsync(
                _projectId,
                feature.Slug,
                null,
                "body\n\n<log>\n- 2026-10-13 [agent] — corrected note\n</log>\n",
                status: null));

        ex.Code.ShouldBe(PlanningFailureCode.LogHistoryNotAppendable);
        ex.Message.ShouldContain("append-only");
    }

    private PlanningArtifactService CreateService() =>
        new(
            _context,
            TestPlanningDispatcher.Create(_context, _materializer),
            new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)));

    private PlanningFeatureService CreateFeatureService() =>
        new(
            _context,
            _materializer,
            new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
            new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)),
            TestPlanningDispatcher.Create(_context, _materializer),
            NullLogger<PlanningFeatureService>.Instance);

    private async Task<(PlanningArtifactService Service, PlanningArtifact Artifact)> SeedArtifactAsync(string body)
    {
        var feature = PlanningFeature.Create(
            _projectId, $"log-edit-{++_featureCounter:D2}-{Guid.NewGuid():N}".ToLowerInvariant(),
            "Log edit", "body");
        _context.PlanningFeatures.Add(feature);
        await _context.SaveChangesAsync();

        var service = CreateService();
        var artifact = await service.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, body, null, null, null, default);
        return (service, artifact);
    }

    private async Task<PlanningArtifact> ReloadAsync(Guid artifactId)
    {
        using var verify = _db.CreateContext();
        return await verify.PlanningArtifacts
            .Include(a => a.Versions)
            .AsNoTracking()
            .FirstAsync(a => a.Id == artifactId);
    }
}
