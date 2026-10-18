using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ProjectFavoriteMigrationTests : IDisposable
{
    private const string AddEntryAcksMigration = "20261017201904_AddEntryAcks";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-project-favorite-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_ProjectCreatedBeforeColumn_GetsFalseDefault()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(AddEntryAcksMigration);

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            db.Projects.Single().IsFavorite.ShouldBeFalse();
        }
    }

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsFavorite()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var starred = Project.Create("starred", "/tmp/starred");
            starred.SetFavorite(true);
            var plain = Project.Create("plain", "/tmp/plain");
            db.Projects.AddRange(starred, plain);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            db.Projects.Single(p => p.Name == "starred").IsFavorite.ShouldBeTrue();
            db.Projects.Single(p => p.Name == "plain").IsFavorite.ShouldBeFalse();
        }
    }

    [Fact]
    public void Migrate_DownToEntryAcks_DropsFavoriteColumn()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var starred = Project.Create("downgraded", "/tmp/downgraded");
            starred.SetFavorite(true);
            db.Projects.Add(starred);
            db.SaveChanges();

            db.Database.Migrate(AddEntryAcksMigration);
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate();

            db.Projects.Single().IsFavorite.ShouldBeFalse();
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
