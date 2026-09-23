using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ProjectLastMaterializationFailureMigrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-last-mat-fail-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_ProjectCreatedBeforeColumns_KeepsNullFailureState()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate("20260922152553_AddProjectLastMaterializedAt");

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            var project = db.Projects.Single();
            project.LastMaterializationError.ShouldBeNull();
            project.LastMaterializationFailedAt.ShouldBeNull();
        }
    }

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsFailureState()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("roundtrip", "/tmp/roundtrip");
            project.MarkMaterializationFailed("File operation failed for 'x': boom");
            db.Projects.Add(project);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            var project = db.Projects.Single();
            project.LastMaterializationError.ShouldBe("File operation failed for 'x': boom");
            project.LastMaterializationFailedAt!.Value.ShouldBeInRange(
                DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
        }
    }

    [Fact]
    public void MarkMaterializationFailed_TruncatesLongErrorToBoundedLength()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("truncate", "/tmp/truncate");
            project.MarkMaterializationFailed(new string('x', 2000));
            db.Projects.Add(project);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            db.Projects.Single().LastMaterializationError!.Length.ShouldBe(512);
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
