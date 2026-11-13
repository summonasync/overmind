using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ProjectHumanDigestMigrationTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"overmind-human-digest-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_FreshProject_DefaultsDigestOff()
    {
        using var db = CreateContext();
        db.Database.Migrate();

        var project = Project.Create("digest-default", "/tmp/digest-default");
        db.Projects.Add(project);
        db.SaveChanges();

        db.Projects.Single(p => p.Id == project.Id).HumanDigestEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Migrate_FlagSet_RoundTrips()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();
            var project = Project.Create("digest-on", "/tmp/digest-on");
            project.SetHumanDigest(true);
            db.Projects.Add(project);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            db.Projects.Single(p => p.Name == "digest-on").HumanDigestEnabled.ShouldBeTrue();
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
