using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class UniqueKitNameMigrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-migration-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_WithDuplicateKitNames_RenamesDuplicatesAndKeepsEarliestIntact()
    {
        var firstAlpha = SkillKit.Create("alpha", 1, string.Empty);
        var firstBeta = SkillKit.Create("beta", 1, string.Empty);
        using (var db = CreateContext())
        {
            db.Database.Migrate("20260914151947_SkillFileAgnosticPlatform");

            db.SkillKits.AddRange(firstAlpha, firstBeta, SkillKit.Create("gamma", 1, string.Empty));
            db.SaveChanges();

            db.SkillKits.AddRange(
                SkillKit.Create("alpha", 1, string.Empty),
                SkillKit.Create("alpha", 1, string.Empty),
                SkillKit.Create("beta", 1, string.Empty));
            db.SaveChanges();

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            var names = db.SkillKits.Select(k => k.Name).OrderBy(n => n).ToList();
            names.ShouldBe(["alpha", "alpha (2)", "alpha (3)", "beta", "beta (2)", "gamma"]);

            db.SkillKits.Find(firstAlpha.Id)!.Name.ShouldBe("alpha");
            db.SkillKits.Find(firstBeta.Id)!.Name.ShouldBe("beta");

            db.SkillKits.Add(SkillKit.Create("alpha", 2, string.Empty));
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
