using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Overmind.Infrastructure.Tests;

public sealed class GraphNodePositionMigrationTests : IDisposable
{
    private const string AnchorMigration = "20261122113146_AddMaterializationErrorSeenAt";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-graph-pos-{Guid.NewGuid():N}.db");
    private readonly ITestOutputHelper _output;

    public GraphNodePositionMigrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Migrate_FreshDatabase_CreatesTheTableOnTheCompositeKey()
    {
        using var db = CreateContext();
        db.Database.Migrate();

        (await TableNames(db)).ShouldContain("GraphNodePositions");

        var shared = Guid.NewGuid();
        db.GraphNodePositions.AddRange(
            GraphNodePosition.Write(EntryLinkKind.Entry, shared, 1, 2, pinned: true, DateTime.UtcNow),
            GraphNodePosition.Write(EntryLinkKind.Feature, shared, 3, 4, pinned: true, DateTime.UtcNow));
        await db.SaveChangesAsync();

        await Should.ThrowAsync<Microsoft.Data.Sqlite.SqliteException>(async () => await db.Database.ExecuteSqlAsync(
                $"INSERT INTO GraphNodePositions (NodeKind, NodeId, X, Y, Pinned, UpdatedAt) VALUES (0, {shared.ToString().ToUpperInvariant()}, 5, 6, 1, {DateTime.UtcNow})"),
            "the composite key holds at the store, not only in the identity map");
    }

    [Fact]
    public async Task Migrate_FromTheAnchorMigration_CreatesTheTableAlongsideLiveShapedRows()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(AnchorMigration);
            (await TableNames(db)).ShouldNotContain("GraphNodePositions");

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({Guid.NewGuid()}, 'Pinned field', '/tmp/pinned-field', '', {DateTime.UtcNow})");

            db.Database.Migrate();
        }

        using var verify = CreateContext();
        (await TableNames(verify)).ShouldContain("GraphNodePositions");
        verify.Projects.Single().Name.ShouldBe("Pinned field");
    }

    [Fact]
    public async Task GuidNodeIds_AreStoredAsUppercaseText_AndComparedCaseSensitively()
    {
        using var db = CreateContext();
        db.Database.Migrate();

        var nodeId = Guid.NewGuid();
        db.GraphNodePositions.Add(
            GraphNodePosition.Write(EntryLinkKind.Entry, nodeId, 1, 2, pinned: true, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var stored = await ReadScalarsAsync(db,
            $"SELECT NodeId FROM GraphNodePositions WHERE NodeId = '{nodeId.ToString().ToUpperInvariant()}'");
        stored.ShouldHaveSingleItem("the EF-written row is findable by its uppercase text form");

        var raw = await ReadScalarsAsync(db, "SELECT NodeId FROM GraphNodePositions");
        raw.ShouldHaveSingleItem();
        raw[0].ShouldBe(nodeId.ToString().ToUpperInvariant());

        var lower = Guid.NewGuid();
        db.Database.ExecuteSql(
            $"INSERT INTO GraphNodePositions (NodeKind, NodeId, X, Y, Pinned, UpdatedAt) VALUES (0, {lower.ToString().ToLowerInvariant()}, 1, 2, 0, {DateTime.UtcNow})");

        var byLower = await db.GraphNodePositions
            .FromSql($"SELECT * FROM GraphNodePositions WHERE NodeId = {lower.ToString().ToLowerInvariant()}")
            .AnyAsync();
        byLower.ShouldBeTrue("the raw row is findable by the exact case it was stored in");

        var byUpper = await db.GraphNodePositions
            .FromSql($"SELECT * FROM GraphNodePositions WHERE NodeId = {lower.ToString().ToUpperInvariant()}")
            .AnyAsync();
        byUpper.ShouldBeFalse("the TEXT column has no NOCASE collation, so a case variant is a different key");
    }

    [Fact]
    public async Task Migrate_OnCopyOfLiveProfile_AddsTheTableAndKeepsEveryExistingRow()
    {
        var livePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".overmind",
            "overmind.db");

        if (!File.Exists(livePath))
        {
            _output.WriteLine("Live Overmind profile not found; skipping live migration test.");
            return;
        }

        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var source = livePath + suffix;
            if (File.Exists(source))
            {
                File.Copy(source, _path + suffix);
            }
        }

        int projectsBefore;
        await using (var db = CreateContext())
        {
            projectsBefore = await db.Projects.CountAsync();
        }

        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
            (await TableNames(db)).ShouldContain("GraphNodePositions");
        }

        await using var verify = CreateContext();
        (await verify.Projects.CountAsync()).ShouldBe(projectsBefore);
        (await verify.GraphNodePositions.CountAsync()).ShouldBe(0);
    }

    private OvermindDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OvermindDbContext>()
            .UseSqlite($"Data Source={_path};Pooling=false")
            .Options;

        return new OvermindDbContext(options);
    }

    private static async Task<List<string>> TableNames(OvermindDbContext db)
    {
        var values = new List<string>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
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

    private static async Task<List<string>> ReadScalarsAsync(OvermindDbContext db, string sql)
    {
        var values = new List<string>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.IsDBNull(0) ? string.Empty : reader.GetString(0));
        }

        return values;
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
