using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ProjectLastMaterializationSkipsMigrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-mat-skips-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_ProjectCreatedBeforeColumns_KeepsNullSkipSummary()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate("20260923155142_AddProjectLastMaterializationFailure");

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            var project = db.Projects.Single();
            project.LastMaterializationSkipCount.ShouldBeNull();
            project.LastMaterializationSkipHead.ShouldBeNull();
        }
    }

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsSkipCountWithCappedHead()
    {
        var skips = Enumerable.Range(1, 7)
            .Select(i => new PlanningMaterializationSkip(
                $"BACKLOG/006-demo/tasks/{i:D2}-task.md",
                "tracked file has local edits that are not on the board"))
            .ToArray();

        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("roundtrip", "/tmp/roundtrip");
            db.Projects.Add(project);
            db.SaveChanges();

            project.MarkMaterialized(skips);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            var project = db.Projects.Single();
            project.LastMaterializationSkipCount.ShouldBe(7);
            project.LastMaterializationSkipHead.ShouldNotBeNull();
            var head = project.LastMaterializationSkipHead!.Split('\n');
            head.Length.ShouldBe(5);
            head[0].ShouldBe("BACKLOG/006-demo/tasks/01-task.md: tracked file has local edits that are not on the board");
            head[4].ShouldBe("BACKLOG/006-demo/tasks/05-task.md: tracked file has local edits that are not on the board");
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
