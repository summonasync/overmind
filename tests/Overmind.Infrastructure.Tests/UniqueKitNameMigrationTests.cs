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

            InsertBeforeUniqueIndex(db, firstAlpha);
            InsertBeforeUniqueIndex(db, firstBeta);
            InsertBeforeUniqueIndex(db, SkillKit.Create("gamma", 1, string.Empty));
            InsertBeforeUniqueIndex(db, SkillKit.Create("alpha", 1, string.Empty));
            InsertBeforeUniqueIndex(db, SkillKit.Create("alpha", 1, string.Empty));
            InsertBeforeUniqueIndex(db, SkillKit.Create("beta", 1, string.Empty));

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

    // The rows that predate the unique index go in as raw SQL: the historical schema lacks the
    // columns the current model maps (Mandatory since 250/T2, Legacy since 250/T3), so SaveChanges
    // would fail on the schema instead of proving what the migration does to duplicate names.
    private static void InsertBeforeUniqueIndex(OvermindDbContext db, SkillKit kit) =>
        db.Database.ExecuteSqlRaw(
            "INSERT INTO SkillKits (Id, Name, Version, Description, SourceKind) VALUES ({0}, {1}, {2}, {3}, {4})",
            kit.Id, kit.Name, kit.Version, kit.Description, (int)kit.SourceKind);

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
