using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningFeatureQueuePositionMigrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-queue-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_ExistingFeatureRows_BackfillsContiguousPositionsPerPriorityBand()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate("20260924190411_AddPlanningFeatureDependencies");

            var projectId = Guid.NewGuid();
            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({projectId}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");

            InsertLegacyFeature(db, projectId, "queue-a", FeaturePriority.P2, new DateTime(2026, 9, 10, 12, 0, 0));
            InsertLegacyFeature(db, projectId, "queue-b", FeaturePriority.P0, new DateTime(2026, 9, 12, 12, 0, 0));
            InsertLegacyFeature(db, projectId, "queue-c", FeaturePriority.P2, new DateTime(2026, 9, 11, 12, 0, 0));
            InsertLegacyFeature(db, projectId, "queue-d", FeaturePriority.P1, new DateTime(2026, 9, 9, 12, 0, 0));
            InsertLegacyFeature(db, projectId, "queue-e", FeaturePriority.P0, new DateTime(2026, 9, 1, 12, 0, 0));
            InsertLegacyFeature(db, projectId, "queue-f", FeaturePriority.P2, new DateTime(2026, 9, 10, 12, 0, 0));

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            var positions = db.PlanningFeatures
                .ToDictionary(f => f.Slug, f => (f.Priority, f.QueuePosition));

            positions["queue-e"].ShouldBe((FeaturePriority.P0, 1));
            positions["queue-b"].ShouldBe((FeaturePriority.P0, 2));
            positions["queue-d"].ShouldBe((FeaturePriority.P1, 1));
            positions["queue-a"].ShouldBe((FeaturePriority.P2, 1));
            positions["queue-f"].ShouldBe((FeaturePriority.P2, 2));
            positions["queue-c"].ShouldBe((FeaturePriority.P2, 3));
        }
    }

    [Fact]
    public void Migrate_TwoProjectsWithIdenticalSlugs_BackfillsPositionsIndependentPerProject()
    {
        var projectId = Guid.NewGuid();
        var twinProjectId = Guid.NewGuid();

        using (var db = CreateContext())
        {
            db.Database.Migrate("20260924190411_AddPlanningFeatureDependencies");

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({projectId}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");
            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({twinProjectId}, 'twin', '/tmp/twin', '', {DateTime.UtcNow})");

            InsertLegacyFeature(db, projectId, "queue-a", FeaturePriority.P2, new DateTime(2026, 9, 10, 12, 0, 0));
            InsertLegacyFeature(db, projectId, "queue-b", FeaturePriority.P0, new DateTime(2026, 9, 12, 12, 0, 0));
            InsertLegacyFeature(db, projectId, "queue-c", FeaturePriority.P2, new DateTime(2026, 9, 11, 12, 0, 0));
            InsertLegacyFeature(db, projectId, "queue-d", FeaturePriority.P1, new DateTime(2026, 9, 9, 12, 0, 0));
            InsertLegacyFeature(db, projectId, "queue-e", FeaturePriority.P0, new DateTime(2026, 9, 1, 12, 0, 0));
            InsertLegacyFeature(db, projectId, "queue-f", FeaturePriority.P2, new DateTime(2026, 9, 10, 12, 0, 0));

            InsertLegacyFeature(db, twinProjectId, "queue-a", FeaturePriority.P2, new DateTime(2026, 9, 10, 6, 0, 0));
            InsertLegacyFeature(db, twinProjectId, "queue-b", FeaturePriority.P0, new DateTime(2026, 9, 13, 12, 0, 0));
            InsertLegacyFeature(db, twinProjectId, "queue-c", FeaturePriority.P2, new DateTime(2026, 9, 11, 6, 0, 0));
            InsertLegacyFeature(db, twinProjectId, "queue-d", FeaturePriority.P1, new DateTime(2026, 9, 9, 6, 0, 0));
            InsertLegacyFeature(db, twinProjectId, "queue-e", FeaturePriority.P0, new DateTime(2026, 9, 2, 12, 0, 0));
            InsertLegacyFeature(db, twinProjectId, "queue-f", FeaturePriority.P2, new DateTime(2026, 9, 10, 18, 0, 0));

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            var positions = db.PlanningFeatures
                .ToDictionary(f => (f.ProjectId, f.Slug), f => (f.Priority, f.QueuePosition));

            positions[(projectId, "queue-e")].ShouldBe((FeaturePriority.P0, 1));
            positions[(projectId, "queue-b")].ShouldBe((FeaturePriority.P0, 2));
            positions[(projectId, "queue-d")].ShouldBe((FeaturePriority.P1, 1));
            positions[(projectId, "queue-a")].ShouldBe((FeaturePriority.P2, 1));
            positions[(projectId, "queue-f")].ShouldBe((FeaturePriority.P2, 2));
            positions[(projectId, "queue-c")].ShouldBe((FeaturePriority.P2, 3));

            positions[(twinProjectId, "queue-e")].ShouldBe((FeaturePriority.P0, 1));
            positions[(twinProjectId, "queue-b")].ShouldBe((FeaturePriority.P0, 2));
            positions[(twinProjectId, "queue-d")].ShouldBe((FeaturePriority.P1, 1));
            positions[(twinProjectId, "queue-a")].ShouldBe((FeaturePriority.P2, 1));
            positions[(twinProjectId, "queue-f")].ShouldBe((FeaturePriority.P2, 2));
            positions[(twinProjectId, "queue-c")].ShouldBe((FeaturePriority.P2, 3));
        }
    }

    private static void InsertLegacyFeature(
        OvermindDbContext db, Guid projectId, string slug, FeaturePriority priority, DateTime createdAt)
    {
        db.Database.ExecuteSql(
            $"INSERT INTO PlanningFeatures (Id, ProjectId, Slug, Title, Body, Status, Priority, IsCurrent, Version, CreatedAt, UpdatedAt) VALUES ({Guid.NewGuid()}, {projectId}, {slug}, {slug}, 'body', 0, {(int)priority}, 0, 1, {createdAt}, {createdAt})");
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
