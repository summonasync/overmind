using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningFeatureDependencyMigrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-deps-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_DatabaseAtPlanningFts_AddsDependencyStorageWithoutTouchingExistingRows()
    {
        var projectId = Guid.NewGuid();
        var legacyFeatureId = Guid.NewGuid();
        using (var db = CreateContext())
        {
            db.Database.Migrate("20260924151854_AddPlanningFts");

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({projectId}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");
            db.Database.ExecuteSql(
                $"INSERT INTO PlanningFeatures (Id, ProjectId, Slug, Title, Body, Status, IsCurrent, Version, CreatedAt, UpdatedAt) VALUES ({legacyFeatureId}, {projectId}, '001-legacy', 'Legacy', 'body', 0, 0, 1, {DateTime.UtcNow}, {DateTime.UtcNow})");

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            db.PlanningFeatures.Count(f => f.ProjectId == projectId).ShouldBe(1);

            var legacy = db.PlanningFeatures.Single(f => f.Id == legacyFeatureId);
            var blocker = PlanningFeature.Create(projectId, "002-blocker", "Blocker", "body");
            legacy.AddBlockedBy(blocker);
            db.PlanningFeatures.Add(blocker);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            var legacy = db.PlanningFeatures
                .Include(f => f.BlockedBy)
                .Single(f => f.Id == legacyFeatureId);
            legacy.BlockedBy.Single().BlockedByFeatureId.ShouldBe(db.PlanningFeatures.Single(f => f.Slug == "002-blocker").Id);
        }
    }

    [Fact]
    public void BlockedBy_RoundTripsThroughContext_BlocksDerivedInMemory()
    {
        using var db = new TempDatabase();
        var projectId = SeedProject(db);
        var first = PlanningFeature.Create(projectId, "001-auth", "Auth", "body");
        var second = PlanningFeature.Create(projectId, "002-api", "Api", "body");
        second.AddBlockedBy(first);

        using (var ctx = db.CreateContext())
        {
            ctx.PlanningFeatures.AddRange(first, second);
            ctx.SaveChanges();
        }

        using (var ctx = db.CreateContext())
        {
            var loaded = ctx.PlanningFeatures
                .Include(f => f.BlockedBy)
                .Where(f => f.ProjectId == projectId)
                .ToList();

            var blocker = loaded.Single(f => f.Slug == "001-auth");
            var blocked = loaded.Single(f => f.Slug == "002-api");

            blocked.BlockedBy.Single().BlockedByFeatureId.ShouldBe(blocker.Id);
            blocker.GetBlocks(loaded).Select(f => f.Slug).ShouldBe(["002-api"]);
            blocked.GetBlocks(loaded).ShouldBeEmpty();
        }
    }

    [Fact]
    public void Delete_BlockerFeature_CascadesLinkRowAway()
    {
        using var db = new TempDatabase();
        var projectId = SeedProject(db);
        var first = PlanningFeature.Create(projectId, "001-auth", "Auth", "body");
        var second = PlanningFeature.Create(projectId, "002-api", "Api", "body");
        second.AddBlockedBy(first);

        using (var ctx = db.CreateContext())
        {
            ctx.PlanningFeatures.AddRange(first, second);
            ctx.SaveChanges();
        }

        using (var ctx = db.CreateContext())
        {
            ctx.PlanningFeatures.Remove(ctx.PlanningFeatures.Single(f => f.Slug == "001-auth"));
            ctx.SaveChanges();
        }

        using (var ctx = db.CreateContext())
        {
            ctx.PlanningFeatureDependencies.ShouldBeEmpty();
            ctx.PlanningFeatures
                .Include(f => f.BlockedBy)
                .Single(f => f.Slug == "002-api")
                .BlockedBy.ShouldBeEmpty();
        }
    }

    [Fact]
    public void Delete_DependentFeature_CascadesLinkRowAway()
    {
        using var db = new TempDatabase();
        var projectId = SeedProject(db);
        var first = PlanningFeature.Create(projectId, "001-auth", "Auth", "body");
        var second = PlanningFeature.Create(projectId, "002-api", "Api", "body");
        second.AddBlockedBy(first);

        using (var ctx = db.CreateContext())
        {
            ctx.PlanningFeatures.AddRange(first, second);
            ctx.SaveChanges();
        }

        using (var ctx = db.CreateContext())
        {
            ctx.PlanningFeatures.Remove(ctx.PlanningFeatures.Single(f => f.Slug == "002-api"));
            ctx.SaveChanges();
        }

        using (var ctx = db.CreateContext())
        {
            ctx.PlanningFeatureDependencies.ShouldBeEmpty();
            ctx.PlanningFeatures
                .Include(f => f.BlockedBy)
                .Single(f => f.Slug == "001-auth")
                .BlockedBy.ShouldBeEmpty();
        }
    }

    [Fact]
    public void RemoveBlockedBy_SavedThroughContext_DeletesLinkRowFromDatabase()
    {
        using var db = new TempDatabase();
        var projectId = SeedProject(db);
        var first = PlanningFeature.Create(projectId, "001-auth", "Auth", "body");
        var second = PlanningFeature.Create(projectId, "002-api", "Api", "body");
        second.AddBlockedBy(first);

        using (var ctx = db.CreateContext())
        {
            ctx.PlanningFeatures.AddRange(first, second);
            ctx.SaveChanges();
        }

        using (var ctx = db.CreateContext())
        {
            var blocker = ctx.PlanningFeatures.Single(f => f.Slug == "001-auth");
            var dependent = ctx.PlanningFeatures
                .Include(f => f.BlockedBy)
                .Single(f => f.Slug == "002-api");

            dependent.RemoveBlockedBy(blocker);
            ctx.SaveChanges();
        }

        using (var ctx = db.CreateContext())
        {
            ctx.PlanningFeatureDependencies.ShouldBeEmpty();
            ctx.PlanningFeatures
                .Include(f => f.BlockedBy)
                .Single(f => f.Slug == "002-api")
                .BlockedBy.ShouldBeEmpty();
        }
    }

    private static Guid SeedProject(TempDatabase db)
    {
        using var ctx = db.CreateContext();
        var project = Project.Create($"deps-{Guid.NewGuid():N}", $"/tmp/deps-{Guid.NewGuid():N}", string.Empty);
        ctx.Projects.Add(project);
        ctx.SaveChanges();
        return project.Id;
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
