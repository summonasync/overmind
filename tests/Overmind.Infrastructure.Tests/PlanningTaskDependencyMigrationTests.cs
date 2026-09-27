using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningTaskDependencyMigrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-task-deps-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_DatabaseAtLastFeatureLevelMigration_CreatesTaskDependencyStorage()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate("20260925123000_AddProjectLastMaterializationRun");

            Should.Throw<Microsoft.Data.Sqlite.SqliteException>(() => db.PlanningTaskDependencies.ToList());
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate();

            db.PlanningTaskDependencies.ShouldBeEmpty();
        }
    }

    [Fact]
    public void BlockedBy_RoundTripsThroughContext_EdgeBetweenTaskArtifactsPersists()
    {
        using var db = new TempDatabase();
        var (blockedId, blockingId) = SeedTaskEdge(db);

        using (var ctx = db.CreateContext())
        {
            var blocked = ctx.PlanningArtifacts
                .Include(a => a.BlockedBy)
                .Single(a => a.Id == blockedId);

            blocked.BlockedBy.Single().BlockingId.ShouldBe(blockingId);
        }
    }

    [Fact]
    public void Insert_DuplicateTaskDependencyPair_RejectedByPrimaryKey()
    {
        using var db = new TempDatabase();
        var (blockedId, blockingId) = SeedTaskEdge(db);

        using var ctx = db.CreateContext();
        Should.Throw<Microsoft.Data.Sqlite.SqliteException>(() =>
            ctx.Database.ExecuteSql(
                $"INSERT INTO PlanningTaskDependencies (BlockedById, BlockingId) VALUES ({blockedId}, {blockingId})"))
            .Message.ShouldContain("UNIQUE");
    }

    [Fact]
    public void Delete_BlockedTaskArtifact_CascadesEdgeAway()
    {
        using var db = new TempDatabase();
        var (blockedId, _) = SeedTaskEdge(db);

        using (var ctx = db.CreateContext())
        {
            ctx.PlanningArtifacts.Remove(ctx.PlanningArtifacts.Single(a => a.Id == blockedId));
            ctx.SaveChanges();
        }

        using (var ctx = db.CreateContext())
        {
            ctx.PlanningTaskDependencies.ShouldBeEmpty();
        }
    }

    [Fact]
    public void Delete_BlockingTaskArtifact_CascadesEdgeAway()
    {
        using var db = new TempDatabase();
        var (_, blockingId) = SeedTaskEdge(db);

        using (var ctx = db.CreateContext())
        {
            ctx.PlanningArtifacts.Remove(ctx.PlanningArtifacts.Single(a => a.Id == blockingId));
            ctx.SaveChanges();
        }

        using (var ctx = db.CreateContext())
        {
            ctx.PlanningTaskDependencies.ShouldBeEmpty();
        }
    }

    private static (Guid BlockedId, Guid BlockingId) SeedTaskEdge(TempDatabase db)
    {
        Guid blockedId;
        Guid blockingId;

        using (var ctx = db.CreateContext())
        {
            var project = Project.Create($"task-deps-{Guid.NewGuid():N}", $"/tmp/task-deps-{Guid.NewGuid():N}", string.Empty);
            var feature = PlanningFeature.Create(project.Id, "081-tasks", "Tasks", "body");
            var blocked = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Task, "blocked body", order: 0);
            var blocking = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Task, "blocking body", order: 1);

            blocked.BlockedBy.Add(PlanningTaskDependency.Create(blocked.Id, blocking.Id));

            ctx.Projects.Add(project);
            ctx.PlanningFeatures.Add(feature);
            ctx.PlanningArtifacts.AddRange(blocked, blocking);
            ctx.SaveChanges();

            blockedId = blocked.Id;
            blockingId = blocking.Id;
        }

        return (blockedId, blockingId);
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
