using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ProjectNumberStyleMigrationTests : IDisposable
{
    private const string AddProjectManifestMigration = "20261005153758_AddProjectManifest";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-project-number-style-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_ProjectCreatedBeforeColumns_GetsZeroImpactDefaults()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(AddProjectManifestMigration);

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            var project = db.Projects.Single();
            project.NumberStyle.ShouldBe(ProjectNumberStyle.Hash);
            project.NumberPrefix.ShouldBeNull();
        }
    }

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsKeyStyle()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("keyed", "/tmp/keyed");
            project.SetNumbering(ProjectNumberStyle.Key, "OM");
            db.Projects.Add(project);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            var project = db.Projects.Single();
            project.NumberStyle.ShouldBe(ProjectNumberStyle.Key);
            project.NumberPrefix.ShouldBe("OM");
        }
    }

    [Fact]
    public void Migrate_DownToProjectManifest_DropsNumberColumns()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("downgraded", "/tmp/downgraded");
            project.SetNumbering(ProjectNumberStyle.Key, "OM");
            db.Projects.Add(project);
            db.SaveChanges();

            db.Database.Migrate(AddProjectManifestMigration);
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate();

            db.Projects.Single().NumberStyle.ShouldBe(ProjectNumberStyle.Hash);
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
