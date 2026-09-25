using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ProjectMaterializationRunMigrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-mat-run-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_ProjectCreatedBeforeColumns_KeepsAttemptAndRunNull()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate("20260925100000_AddPlanningFeatureQueuePosition");

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            var project = db.Projects.Single();
            project.LastMaterializationAttemptAt.ShouldBeNull();
            project.LastMaterializationRun.ShouldBeNull();
            project.LastMaterializedAt.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Migrate_FreshDatabase_RoundTripsRunReportWith054Fields()
    {
        var run = new MaterializationRunReport(
            new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 25, 10, 0, 1, DateTimeKind.Utc),
            Atomic: true,
            ["BACKLOG/006-demo/feature.md"],
            [new PlanningMaterializationSkip("BACKLOG/006-demo/spec.md", "content already matches the board")],
            []);

        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("roundtrip", "/tmp/roundtrip");
            db.Projects.Add(project);
            db.SaveChanges();

            project.MarkMaterializationAttempt(run.StartedAt);
            project.RecordMaterializationRun(MaterializationRunJson.Serialize(run));
            project.MarkMaterialized([run.Skipped[0]]);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            var project = db.Projects.Single();
            project.LastMaterializationAttemptAt.ShouldBe(run.StartedAt);
            project.LastMaterializedAt.ShouldNotBeNull();
            project.LastMaterializationSkipCount.ShouldBe(1);

            var persisted = MaterializationRunJson.Deserialize(project.LastMaterializationRun);
            persisted.ShouldNotBeNull();
            persisted!.StartedAt.ShouldBe(run.StartedAt);
            persisted.FinishedAt.ShouldBe(run.FinishedAt);
            persisted.Atomic.ShouldBeTrue();
            persisted.Written.ShouldBe(run.Written);
            persisted.Skipped.ShouldBe(run.Skipped);
            persisted.Refused.ShouldBeEmpty();
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
