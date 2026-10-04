using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ArtifactDeletionMigrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-artifact-del-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_DatabaseAtLastPreDeletionLevel_AddsDeletionColumns()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate("20261003152603_AddIssues");

            ArtifactColumns(db).ShouldNotContain("DeletedAt");
            ArtifactColumns(db).ShouldNotContain("DeletedBy");
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate();

            ArtifactColumns(db).ShouldContain("DeletedAt");
            ArtifactColumns(db).ShouldContain("DeletedBy");
        }
    }

    private static List<string> ArtifactColumns(OvermindDbContext db) =>
        db.Database.SqlQuery<string>(
            $"SELECT name AS Value FROM pragma_table_info('PlanningArtifacts')").ToList();

    [Fact]
    public void Migrate_RoundTrip_IndexIsPartialOnDeletedAt()
    {
        using var db = CreateContext();
        db.Database.Migrate();

        var sql = db.Database.SqlQuery<string>(
            $"SELECT sql AS Value FROM sqlite_master WHERE type = 'index' AND name = 'IX_PlanningArtifacts_FeatureId_Kind_Order'")
            .Single();

        sql.ShouldContain("UNIQUE");
        sql.ShouldContain("\"DeletedAt\" IS NULL");
    }

    [Fact]
    public void Migrate_DownFromDeletionLevel_DropsColumnsAndRestoresNonPartialIndex()
    {
        using var db = CreateContext();
        db.Database.Migrate();
        db.Database.Migrate("20261003152603_AddIssues");

        ArtifactColumns(db).ShouldNotContain("DeletedAt");
        ArtifactColumns(db).ShouldNotContain("DeletedBy");

        var sql = db.Database.SqlQuery<string>(
            $"SELECT sql AS Value FROM sqlite_master WHERE type = 'index' AND name = 'IX_PlanningArtifacts_FeatureId_Kind_Order'")
            .Single();

        sql.ShouldContain("UNIQUE");
        sql.ShouldNotContain("DeletedAt");
    }

    [Fact]
    public void Insert_SecondLiveArtifactAtSameOrder_StillRejected()
    {
        using var db = CreateContext();
        db.Database.Migrate();
        var featureId = SeedFeature(db);
        SeedArtifact(db, featureId, PlanningArtifactKind.Task, order: 0, deleted: false);

        var ex = Should.Throw<DbUpdateException>(() =>
            SeedArtifact(db, featureId, PlanningArtifactKind.Task, order: 0, deleted: false));

        ex.InnerException!.Message.ShouldContain("UNIQUE");
    }

    [Fact]
    public void Insert_ArtifactAtOrderOfSoftDeletedRow_AcceptedByPartialIndex()
    {
        using var db = CreateContext();
        db.Database.Migrate();
        var featureId = SeedFeature(db);
        SeedArtifact(db, featureId, PlanningArtifactKind.Task, order: 0, deleted: true);

        var reused = SeedArtifact(db, featureId, PlanningArtifactKind.Task, order: 0, deleted: false);
        reused.ShouldNotBe(Guid.Empty);
    }

    private static Guid SeedFeature(OvermindDbContext db)
    {
        var project = Project.Create($"mig-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        var feature = PlanningFeature.Create(project.Id, "094-deletion", "Deletion", "body");
        db.Projects.Add(project);
        db.PlanningFeatures.Add(feature);
        db.SaveChanges();
        return feature.Id;
    }

    private static Guid SeedArtifact(OvermindDbContext db, Guid featureId, PlanningArtifactKind kind, int order, bool deleted)
    {
        var artifact = PlanningArtifact.Create(featureId, kind, "body", order);
        if (deleted)
        {
            artifact.Delete(byHuman: true);
        }

        db.PlanningArtifacts.Add(artifact);
        db.SaveChanges();
        return artifact.Id;
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
