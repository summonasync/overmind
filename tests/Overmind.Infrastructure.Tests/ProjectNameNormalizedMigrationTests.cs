using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ProjectNameNormalizedMigrationTests : IDisposable
{
    private const string AnchorMigration = "20261120211418_AddAgentSessionReplyCursor";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-name-norm-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_ProjectsCreatedBeforeColumn_BackfillsLowerName()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(AnchorMigration);

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'Retro', '/tmp/retro', '', {DateTime.UtcNow})");

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            var project = db.Projects.Single();
            project.Name.ShouldBe("Retro");
            project.NameNormalized.ShouldBe("retro");
        }
    }

    [Fact]
    public void Migrate_ThenSaveCaseVariant_RejectedByUniqueIndex()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(AnchorMigration);

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'Alpha', '/tmp/alpha', '', {DateTime.UtcNow})");

            db.Database.Migrate();

            db.Projects.Add(Project.Create("ALPHA", "/tmp/alpha-upper"));
            Should.Throw<DbUpdateException>(() => db.SaveChanges());
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
