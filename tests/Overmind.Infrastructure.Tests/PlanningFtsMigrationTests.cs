using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningFtsMigrationTests : IDisposable
{
    private const string PreFtsHead = "20260924143916_AddProjectLastMaterializationSkips";

    private const string TwinObjectsInSchema =
        """
        SELECT COUNT(*) FROM sqlite_master
        WHERE type IN ('table', 'trigger') AND name IN (
            'feature_fts', 'artifact_fts',
            'trg_feature_fts_insert', 'trg_feature_fts_update', 'trg_feature_fts_delete',
            'trg_artifact_fts_insert', 'trg_artifact_fts_update', 'trg_artifact_fts_delete')
        """;

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-planning-fts-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Migrate_LegacyShape_BackfillsBothTwinsWithCorrectCounts()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(PreFtsHead);
            SeedLegacyShape(db);
            db.Database.Migrate();
        }

        using var verify = CreateContext();
        (await PlanningFtsProbe.CountAsync(verify, "SELECT COUNT(*) FROM feature_fts")).ShouldBe(2);
        (await PlanningFtsProbe.CountAsync(verify, "SELECT COUNT(*) FROM artifact_fts")).ShouldBe(3);

        (await PlanningFtsProbe.CountAsync(
            verify, "SELECT COUNT(*) FROM feature_fts WHERE feature_fts MATCH $term", "ftsdemo")).ShouldBe(2);
        (await PlanningFtsProbe.CountAsync(
            verify, "SELECT COUNT(*) FROM feature_fts WHERE feature_fts MATCH $term", "legacyseedterm")).ShouldBe(2);
        (await PlanningFtsProbe.CountAsync(
            verify, "SELECT COUNT(*) FROM artifact_fts WHERE artifact_fts MATCH $term", "artifactseedterm")).ShouldBe(3);
        (await PlanningFtsProbe.CountAsync(
            verify, "SELECT COUNT(*) FROM artifact_fts WHERE artifact_fts MATCH $term", "gamma")).ShouldBe(1);
        (await PlanningFtsProbe.CountAsync(
            verify, "SELECT COUNT(*) FROM feature_fts WHERE feature_fts MATCH $term", "artifactseedterm")).ShouldBe(0);

        (await PlanningFtsProbe.CountAsync(
            verify, "SELECT COUNT(*) FROM feature_fts JOIN PlanningFeatures f ON f.rowid = feature_fts.rowid")).ShouldBe(2);
        (await PlanningFtsProbe.CountAsync(
            verify, "SELECT COUNT(*) FROM artifact_fts JOIN PlanningArtifacts a ON a.rowid = artifact_fts.rowid")).ShouldBe(3);
    }

    [Fact]
    public async Task Migrate_DownDropsTwins_UpRebuildsBackfillAndRearmsTriggers()
    {
        using var db = CreateContext();
        db.Database.Migrate(PreFtsHead);
        SeedLegacyShape(db);
        db.Database.Migrate();

        db.Database.Migrate(PreFtsHead);
        (await PlanningFtsProbe.CountAsync(db, TwinObjectsInSchema)).ShouldBe(0);

        db.Database.Migrate();
        (await PlanningFtsProbe.CountAsync(
            db, "SELECT COUNT(*) FROM feature_fts WHERE feature_fts MATCH $term", "legacyseedterm")).ShouldBe(2);
        (await PlanningFtsProbe.CountAsync(
            db, "SELECT COUNT(*) FROM artifact_fts WHERE artifact_fts MATCH $term", "artifactseedterm")).ShouldBe(3);

        var project = await db.Projects.SingleAsync();
        db.PlanningFeatures.Add(PlanningFeature.Create(
            project.Id, "rearm-probe", "Rearm probe", "rearmed quokka body"));
        await db.SaveChangesAsync();
        (await PlanningFtsProbe.CountAsync(
            db, "SELECT COUNT(*) FROM feature_fts WHERE feature_fts MATCH $term", "quokka")).ShouldBe(1);
    }

    private static void SeedLegacyShape(OvermindDbContext db)
    {
        var now = DateTime.UtcNow;
        var projectId = Guid.NewGuid();
        var featureOne = Guid.NewGuid();
        var featureTwo = Guid.NewGuid();
        var spec = (int)PlanningArtifactKind.Spec;
        var plan = (int)PlanningArtifactKind.Plan;
        var taskKind = (int)PlanningArtifactKind.Task;
        var draft = (int)PlanningArtifactStatus.Draft;
        var todo = (int)PlanningArtifactStatus.Todo;
        var light = (int)PlanningReviewLevel.Light;

        db.Database.ExecuteSql(
            $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({projectId}, 'legacy-fts', '/tmp/legacy-fts', '', {now})");

        db.Database.ExecuteSql(
            $"""
             INSERT INTO PlanningFeatures (rowid, Id, ProjectId, Slug, Title, Body, Status, Version, CreatedAt, UpdatedAt)
             VALUES
                 (11, {featureOne}, {projectId}, 'legacy-fts-one', 'Ftsdemo legacy alpha', 'legacyseedterm alpha', 0, 1, {now}, {now}),
                 (12, {featureTwo}, {projectId}, 'legacy-fts-two', 'Ftsdemo legacy beta', 'legacyseedterm beta', 0, 1, {now}, {now})
             """);

        db.Database.ExecuteSql(
            $"""
             INSERT INTO PlanningArtifacts (rowid, Id, FeatureId, Kind, Status, Review, Body, "Order", Version, CreatedAt, UpdatedAt)
             VALUES
                 (21, {Guid.NewGuid()}, {featureOne}, {spec}, {draft}, {light}, 'artifactseedterm alpha', 0, 1, {now}, {now}),
                 (22, {Guid.NewGuid()}, {featureOne}, {plan}, {draft}, {light}, 'artifactseedterm beta', 0, 1, {now}, {now}),
                 (23, {Guid.NewGuid()}, {featureTwo}, {taskKind}, {todo}, {light}, 'artifactseedterm gamma', 1, 1, {now}, {now})
             """);
    }

    private OvermindDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OvermindDbContext>()
            .UseSqlite($"Data Source={_path};Pooling=false")
            .Options;

        return new OvermindDbContext(options);
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var file = _path + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
