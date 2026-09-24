using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ProjectLastMaterializedAtMigrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-last-mat-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_ProjectCreatedBeforeColumn_KeepsNullLastMaterializedAt()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate("20260920185100_AddFeaturePriority");

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            var project = db.Projects.Single();
            project.Name.ShouldBe("legacy");
            project.LastMaterializedAt.ShouldBeNull();
        }
    }

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsLastMaterializedAt()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("roundtrip", "/tmp/roundtrip");
            db.Projects.Add(project);
            db.SaveChanges();

            project.MarkMaterialized([]);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            var stamp = db.Projects.Single().LastMaterializedAt;
            stamp.ShouldNotBeNull();
            stamp!.Value.ShouldBeInRange(DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
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
