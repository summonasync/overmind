using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class FeatureCodeSurfacesMigrationTests : IDisposable
{
    private const string AddLanesMigration = "20261017161452_AddLanes";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"overmind-surfaces-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsSurfaces()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("surfaces", "/tmp/surfaces");
            db.Projects.Add(project);
            var feature = PlanningFeature.Create(project.Id, "001-surfaces", "Surfaces", "body");
            feature.SetCodeSurfaces(CodeSurfacesJson.Serialize(["src/web/**", "src/api/**/*.cs"]));
            db.PlanningFeatures.Add(feature);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            var feature = db.PlanningFeatures.Single();
            CodeSurfacesJson.Deserialize(feature.CodeSurfaces).ShouldBe(["src/web/**", "src/api/**/*.cs"]);
        }
    }

    [Fact]
    public void Migrate_FeatureCreatedBeforeSurfaces_GetsNullSurfaces()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(AddLanesMigration);

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
            db.PlanningFeatures.Single().CodeSurfaces.ShouldBeNull();
        }
    }

    [Fact]
    public void Migrate_DownToAddLanes_DropsSurfacesColumn()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate(AddLanesMigration);

            ColumnNames(db, "PlanningFeatures").ShouldNotContain("CodeSurfaces");
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
