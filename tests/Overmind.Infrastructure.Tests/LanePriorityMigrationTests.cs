using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class LanePriorityMigrationTests : IDisposable
{
    private const string AddProjectFavoriteMigration = "20261018113000_AddProjectFavorite";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-lane-priority-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_LaneCreatedBeforeColumn_GetsNullPriority()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(AddProjectFavoriteMigration);

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");
            db.Database.ExecuteSql(
                $"""
                INSERT INTO PlanningLanes (Id, ProjectId, Name, Contour, SerialOrder, CreatedAt, UpdatedAt)
                SELECT {Guid.NewGuid()}, Id, 'frontend', 'web/src', 1, {DateTime.UtcNow}, {DateTime.UtcNow} FROM Projects
                """);

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            db.PlanningLanes.Single().Priority.ShouldBeNull();
        }
    }

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsPriority()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("prioritized", "/tmp/prioritized");
            db.Projects.Add(project);
            db.SaveChanges();

            db.PlanningLanes.Add(PlanningLane.Create(project.Id, "hot", null, null, 0));
            db.PlanningLanes.Add(PlanningLane.Create(project.Id, "cold", null, null));
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            db.PlanningLanes.Single(l => l.Name == "hot").Priority.ShouldBe(0);
            db.PlanningLanes.Single(l => l.Name == "cold").Priority.ShouldBeNull();
        }
    }

    [Fact]
    public void Migrate_DownToProjectFavorite_DropsPriorityColumn()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("downgraded", "/tmp/downgraded");
            db.Projects.Add(project);
            db.SaveChanges();
            db.PlanningLanes.Add(PlanningLane.Create(project.Id, "hot", null, null, 0));
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate(AddProjectFavoriteMigration);

            ColumnNames(db, "PlanningLanes").ShouldNotContain("Priority");
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate();

            db.PlanningLanes.Single().Name.ShouldBe("hot");
            db.PlanningLanes.Single().Priority.ShouldBeNull();
        }
    }

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
