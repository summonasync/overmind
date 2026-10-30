using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ArtifactContentUpdatedAtMigrationTests : IDisposable
{
    private const string AddIssuesKindMigration = "20261025135605_AddIssuesKind";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"overmind-content-freshness-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_FromPreviousVersion_BackfillsContentUpdatedAtFromUpdatedAt()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(AddIssuesKindMigration);

            var projectId = Guid.NewGuid();
            var featureId = Guid.NewGuid();
            db.Database.ExecuteSql(
                $"""
                INSERT INTO "Projects" ("Id", "Name", "DiskPath", "Description", "CreatedAt")
                VALUES ({projectId}, 'freshness', '/tmp/freshness', '', {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss})
                """);
            db.Database.ExecuteSql(
                $"""
                INSERT INTO "PlanningFeatures" ("Id", "ProjectId", "Slug", "Title", "Body", "Status", "Version", "CreatedAt", "UpdatedAt")
                VALUES ({featureId}, {projectId}, '001-backfill', 'Backfill', 'backfill body', 0, 1, '2020-03-03 04:05:06', '2020-03-03 04:05:06')
                """);
            db.Database.ExecuteSql(
                $"""
                INSERT INTO "PlanningArtifacts" ("Id", "FeatureId", "Kind", "Status", "Review", "Body", "Order", "Version", "CreatedAt", "UpdatedAt")
                VALUES ({Guid.NewGuid()}, {featureId}, 1, 0, 0, '<goal>backfilled</goal>', 0, 1, '2020-03-03 04:05:06', '2020-03-04 05:06:07')
                """);
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var contentUpdatedAt = db.Database
                .SqlQuery<DateTime>(
                    $"""
                    SELECT "ContentUpdatedAt" AS Value FROM "PlanningArtifacts" WHERE "DeletedAt" IS NULL
                    """)
                .AsEnumerable().Single();
            var updatedAt = db.Database
                .SqlQuery<DateTime>(
                    $"""
                    SELECT "UpdatedAt" AS Value FROM "PlanningArtifacts" WHERE "DeletedAt" IS NULL
                    """)
                .AsEnumerable().Single();

            contentUpdatedAt.ShouldBe(new DateTime(2020, 3, 4, 5, 6, 7));
            updatedAt.ShouldBe(contentUpdatedAt);
        }
    }

    [Fact]
    public void Migrate_FreshDatabase_InitializesContentUpdatedAtOnCreate()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("fresh-create", "/tmp/fresh-create");
            db.Projects.Add(project);
            var feature = PlanningFeature.Create(project.Id, "002-fresh", "Fresh", "fresh body");
            db.PlanningFeatures.Add(feature);
            db.SaveChanges();

            db.PlanningArtifacts.Add(PlanningArtifact.Create(
                feature.Id, PlanningArtifactKind.Spec, "<goal>\nfresh\n</goal>\n"));
            db.SaveChanges();
        }

        using var verify = CreateContext();
        var artifact = verify.PlanningArtifacts.Single();
        artifact.ContentUpdatedAt.ShouldBe(artifact.UpdatedAt);
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
