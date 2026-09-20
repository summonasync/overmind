using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class FeaturePriorityMigrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-priority-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_ExistingFeatureRows_BackfilledToP2()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate("20260920120929_AddAgentNotifications");

            var projectId = Guid.NewGuid();
            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({projectId}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");
            for (var i = 1; i <= 3; i++)
            {
                var slug = $"legacy-{i}";
                var title = $"Legacy {i}";
                db.Database.ExecuteSql(
                    $"INSERT INTO PlanningFeatures (Id, ProjectId, Slug, Title, Body, Status, IsCurrent, Version, CreatedAt, UpdatedAt) VALUES ({Guid.NewGuid()}, {projectId}, {slug}, {title}, 'body', 0, 0, 1, {DateTime.UtcNow}, {DateTime.UtcNow})");
            }

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            var priorities = db.PlanningFeatures.Select(f => f.Priority).ToList();
            priorities.Count.ShouldBe(3);
            priorities.ShouldAllBe(p => p == FeaturePriority.P2);
        }
    }

    [Fact]
    public void Migrate_FreshDatabase_StoresExplicitPriorityRoundTrip()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var projectId = Guid.NewGuid();
            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({projectId}, 'fresh', '/tmp/fresh', '', {DateTime.UtcNow})");
            db.PlanningFeatures.Add(PlanningFeature.Create(projectId, "pinned", "Title", "body", FeaturePriority.P0));
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            db.PlanningFeatures.Single().Priority.ShouldBe(FeaturePriority.P0);
        }
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
