using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Entries;

public sealed class EntryLinksMigrationTests : IDisposable
{
    private const string AddEntryLinksMigration = "20261116134208_AddEntryLinks";
    private const string PreviousMigration = "20261113111428_AddProjectHumanDigest";

    private const string EdgeIndex = "IX_EntryLinks_FromEntryId_ToKind_ToId";
    private const string BacklinkIndex = "IX_EntryLinks_ToKind_ToId";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"overmind-entry-links-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Migrate_FreshDatabase_CreatesTheTableWithBothIndexes()
    {
        using var db = CreateContext();
        db.Database.Migrate();

        (await ReadAsync(db, "name", "table", "EntryLinks")).ShouldContain("EntryLinks");

        var edge = await ReadAsync(db, "sql", "index", EdgeIndex);
        edge.ShouldHaveSingleItem();
        edge[0].ShouldContain("UNIQUE");

        var backlink = await ReadAsync(db, "sql", "index", BacklinkIndex);
        backlink.ShouldHaveSingleItem();
        backlink[0].ShouldNotContain("UNIQUE");
    }

    [Fact]
    public async Task Migrate_AppliedTwice_LeavesTheRowsUntouched()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("journal-probe", "/tmp/journal-probe");
            db.Projects.Add(project);
            var source = ContextEntry.Create(project.Id, EntryType.Journal, "probe", "one");
            var target = ContextEntry.Create(project.Id, EntryType.Journal, "target", "two");
            db.ContextEntries.AddRange(source, target);
            await db.SaveChangesAsync();

            db.EntryLinks.Add(EntryLink.CreateDerived(source.Id, EntryLinkKind.Entry, target.Id));
            await db.SaveChangesAsync();

            db.Database.Migrate();
        }

        using var verify = CreateContext();
        (await verify.EntryLinks.CountAsync()).ShouldBe(1);
        verify.Database.GetAppliedMigrations().ShouldContain(AddEntryLinksMigration);
    }

    [Fact]
    public async Task Migrate_RolledBackToOneMigration_DropsTheTable_AndReapplyingBringsItBack()
    {
        using var db = CreateContext();
        db.Database.Migrate();

        db.Database.Migrate(PreviousMigration);
        (await ReadAsync(db, "name", "table", "EntryLinks")).ShouldBeEmpty();

        db.Database.Migrate();
        (await ReadAsync(db, "name", "table", "EntryLinks")).ShouldContain("EntryLinks");
    }

    private static async Task<List<string>> ReadAsync(
        OvermindDbContext db, string column, string kind, string name)
    {
        var values = new List<string>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT {column} FROM sqlite_master WHERE type = '{kind}' AND name = '{name}'";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
            {
                values.Add(reader.GetString(0));
            }
        }

        return values;
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
