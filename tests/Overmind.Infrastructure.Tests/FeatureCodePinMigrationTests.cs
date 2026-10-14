using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class FeatureCodePinMigrationTests : IDisposable
{
    private const string AddProjectNumberStyleMigration = "20261005160124_AddProjectNumberStyle";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-feature-code-pin-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsPinColumns()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("pinned", "/tmp/pinned");
            db.Projects.Add(project);
            db.SaveChanges();

            var feature = PlanningFeature.Create(project.Id, "001-pinned", "Pinned", "body");
            feature.SetCodePin("feat/pin-branch", "/repo/wt", PlanningLogSource.Agent);
            db.PlanningFeatures.Add(feature);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            var feature = db.PlanningFeatures.Single();
            feature.PinnedBranch.ShouldBe("feat/pin-branch");
            feature.PinnedWorktreePath.ShouldBe("/repo/wt");
        }
    }

    [Fact]
    public void Migrate_FeatureCreatedBeforeColumns_GetsNullPinDefaults()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(AddProjectNumberStyleMigration);

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
            var feature = db.PlanningFeatures.Single();
            feature.PinnedBranch.ShouldBeNull();
            feature.PinnedWorktreePath.ShouldBeNull();
        }
    }

    [Fact]
    public void Migrate_DownToProjectNumberStyle_DropsPinColumns()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate(AddProjectNumberStyleMigration);

            var columns = ColumnNames(db, "PlanningFeatures");
            columns.ShouldNotContain("PinnedBranch");
            columns.ShouldNotContain("PinnedWorktreePath");
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
