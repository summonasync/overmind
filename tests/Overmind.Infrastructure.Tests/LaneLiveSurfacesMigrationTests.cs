using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class LaneLiveSurfacesMigrationTests : IDisposable
{
    private const string AddWaveRolesMigration = "20261025112000_AddWaveRoles";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"overmind-live-surfaces-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsLiveSurfaces()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("live-surfaces", "/tmp/live-surfaces");
            db.Projects.Add(project);
            db.PlanningLanes.Add(PlanningLane.Create(
                project.Id, "web-boundary", "web src", null, null,
                LiveSurfacesJson.Serialize([new LiveSurfaceEntry("web/src/**", "van-darkholme")])));
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            var lane = db.PlanningLanes.Single();
            LiveSurfacesJson.Deserialize(lane.LiveSurfaces)
                .ShouldBe([new LiveSurfaceEntry("web/src/**", "van-darkholme")]);
        }
    }

    [Fact]
    public void Migrate_LaneCreatedBeforeLiveSurfaces_GetsNullSurfaces()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(AddWaveRolesMigration);

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");
            db.Database.ExecuteSql(
                $"""
                INSERT INTO PlanningLanes (Id, ProjectId, Name, CreatedAt, UpdatedAt)
                SELECT {Guid.NewGuid()}, Id, 'legacy-lane', {DateTime.UtcNow}, {DateTime.UtcNow} FROM Projects
                """);

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            db.PlanningLanes.Single().LiveSurfaces.ShouldBeNull();
        }
    }

    [Fact]
    public void Migrate_DownToWaveRoles_DropsLiveSurfacesColumn()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate(AddWaveRolesMigration);

            ColumnNames(db, "PlanningLanes").ShouldNotContain("LiveSurfaces");
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
