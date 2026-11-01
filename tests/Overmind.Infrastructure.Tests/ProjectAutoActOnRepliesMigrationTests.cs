using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ProjectAutoActOnRepliesMigrationTests : IDisposable
{
    private const string PreFlagMigration = "20261028072159_AddLaneLiveSurfaces";
    private const string PreviousSchemaMigration = "20261029100535_AddProjectAutoActOnReplies";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"overmind-auto-act-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_FreshProject_DefaultsAutoActOn()
    {
        using var db = CreateContext();
        db.Database.Migrate();

        var project = Project.Create("auto-act-default", "/tmp/auto-act-default");
        db.Projects.Add(project);
        db.SaveChanges();

        db.Projects.Single(p => p.Id == project.Id).AutoActOnReplies.ShouldBeTrue();
    }

    [Fact]
    public void Migrate_FlagSet_RoundTrips()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();
            var project = Project.Create("auto-act-on", "/tmp/auto-act-on");
            project.SetAutoActOnReplies(true);
            db.Projects.Add(project);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            db.Projects.Single(p => p.Name == "auto-act-on").AutoActOnReplies.ShouldBeTrue();
        }
    }

    [Fact]
    public void Migrate_ProjectRowCreatedBeforeFlag_GetsBackfilledOn()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(PreFlagMigration);

            using var command = (SqliteCommand)db.Database.GetDbConnection().CreateCommand();
            command.CommandText =
                "INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) "
                + $"VALUES ('{Guid.NewGuid()}', 'legacy', '/tmp/legacy', '', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}')";
            db.Database.OpenConnection();
            command.ExecuteNonQuery();
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate();
            db.Projects.Single(p => p.Name == "legacy").AutoActOnReplies.ShouldBeTrue();
        }
    }

    [Fact]
    public void Migrate_OffRowOnPreviousSchema_GetsBackfilledOn()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(PreviousSchemaMigration);

            using var command = (SqliteCommand)db.Database.GetDbConnection().CreateCommand();
            command.CommandText =
                "INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt, AutoActOnReplies) "
                + $"VALUES ('{Guid.NewGuid()}', 'stale-off', '/tmp/stale-off', '', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', 0)";
            db.Database.OpenConnection();
            command.ExecuteNonQuery();
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate();
            db.Projects.Single(p => p.Name == "stale-off").AutoActOnReplies.ShouldBeTrue();
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
