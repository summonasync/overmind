using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class LaneMigrationTests : IDisposable
{
    private const string AddFeatureCodePinMigration = "20261014120212_AddFeatureCodePin";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-lanes-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsLanes()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("laned", "/tmp/laned");
            db.Projects.Add(project);
            db.SaveChanges();

            db.PlanningLanes.Add(PlanningLane.Create(project.Id, "frontend", "web/src", 1));
            var feature = PlanningFeature.Create(project.Id, "001-laned", "Laned", "body");
            feature.SetLane("frontend");
            db.PlanningFeatures.Add(feature);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            var lane = db.PlanningLanes.Single();
            lane.Name.ShouldBe("frontend");
            lane.Contour.ShouldBe("web/src");
            lane.SerialOrder.ShouldBe(1);
            db.PlanningFeatures.Single().Lane.ShouldBe("frontend");
        }
    }

    [Fact]
    public void Migrate_FeatureCreatedBeforeLane_GetsNullLane()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(AddFeatureCodePinMigration);

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");
            db.Database.ExecuteSql(
                $"""
                INSERT INTO PlanningFeatures (Id, ProjectId, Slug, Title, Body, Status, Priority, QueuePosition, ClaimedByHuman, Version, CreatedAt, UpdatedAt)
                SELECT {Guid.NewGuid()}, Id, '002-legacy', 'Legacy', '', 0, 2, 1, 0, 1, {DateTime.UtcNow}, {DateTime.UtcNow} FROM Projects
                """);

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            db.PlanningFeatures.Single().Lane.ShouldBeNull();
        }
    }

    [Fact]
    public void Migrate_DownToFeatureCodePin_DropsLanesTableAndColumn()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate(AddFeatureCodePinMigration);

            TableNames(db).ShouldNotContain("PlanningLanes");
            ColumnNames(db, "PlanningFeatures").ShouldNotContain("Lane");
        }
    }

    private static List<string> TableNames(OvermindDbContext db) =>
        [.. db.Database
            .SqlQuery<string>($"SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'PlanningLanes'")
            .AsEnumerable()];

    private static List<string> ColumnNames(OvermindDbContext db, string table) =>
        [.. db.Database
            .SqlQuery<string>($"SELECT name FROM pragma_table_info({table})")
            .AsEnumerable()];

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
