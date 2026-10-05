using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ProjectManifestMigrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-project-manifest-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_ProjectCreatedBeforeColumn_KeepsNullManifest()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate("20261004112512_AddArtifactDeletion");

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            db.Projects.Single().Manifest.ShouldBeNull();
        }
    }

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsManifest()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("manifest", "/tmp/manifest");
            project.SetManifest("<!-- imported from AGENTS.md -->\n\n# Manifest");
            db.Projects.Add(project);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            db.Projects.Single().Manifest.ShouldBe("<!-- imported from AGENTS.md -->\n\n# Manifest");
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
