using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ProjectRescuePolicyMigrationTests : IDisposable
{
    private const string AddProjectHumanDigestMigration = "20261113111428_AddProjectHumanDigest";
    private const string OwnerDirective =
        "everything unpushed may be pushed, everything unmerged I allow merging";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"overmind-rescue-policy-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_LegacyProjectRows_SeededWithOwnerDirective()
    {
        Project.DefaultRescuePolicy.ShouldBe(OwnerDirective);

        using (var db = CreateContext())
        {
            db.Database.Migrate(AddProjectHumanDigestMigration);

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'legacy-a', '/tmp/legacy-a', '', {DateTime.UtcNow})");
            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'legacy-b', '/tmp/legacy-b', '', {DateTime.UtcNow})");

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            var policies = db.Projects.Select(p => p.RescuePolicy).ToList();
            policies.Count.ShouldBe(2);
            policies.ShouldBe([OwnerDirective, OwnerDirective]);
        }
    }

    [Fact]
    public void Migrate_ProjectCreatedAfterMigration_CarriesOwnerDirective()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("fresh", "/tmp/fresh");
            db.Projects.Add(project);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            db.Projects.Single().RescuePolicy.ShouldBe(OwnerDirective);
        }
    }

    [Fact]
    public void SeedStatement_HealsNullRowsAndKeepsHumanSetText()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var healed = Project.Create("heal-me", "/tmp/heal-me");
            var human = Project.Create("human-set", "/tmp/human-set");
            human.SetClaimSettings(15, null, "custom rescue rule");
            db.Projects.AddRange(healed, human);
            db.SaveChanges();

            db.Database.ExecuteSqlRaw("UPDATE Projects SET RescuePolicy = NULL WHERE Name = 'heal-me'");
            var seed = $"""
                UPDATE Projects
                SET RescuePolicy = '{OwnerDirective}'
                WHERE RescuePolicy IS NULL
                """;
            db.Database.ExecuteSqlRaw(seed);
            db.Database.ExecuteSqlRaw(seed);
        }

        using (var db = CreateContext())
        {
            db.Projects.Single(p => p.Name == "heal-me").RescuePolicy.ShouldBe(OwnerDirective);
            db.Projects.Single(p => p.Name == "human-set").RescuePolicy.ShouldBe("custom rescue rule");
        }
    }

    [Fact]
    public void Migrate_DownToHumanDigest_DropsRescuePolicyColumn()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate(AddProjectHumanDigestMigration);

            var columns = db.Database
                .SqlQuery<string>($"SELECT name FROM pragma_table_info('Projects')")
                .AsEnumerable()
                .ToList();
            columns.ShouldNotContain("RescuePolicy");
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
