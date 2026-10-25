using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class EntryTypeIdeaRenameMigrationTests : IDisposable
{
    private const string LastRenamePrecedingMigration = "20261025135605_AddIssuesKind";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-idea-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_ExistingTypeSixRows_ReadBackAsIdeaWithNoSqlRetyping()
    {
        var projectId = Guid.NewGuid();
        var legacyIdeaId = Guid.NewGuid();
        var auditId = Guid.NewGuid();

        using (var db = CreateContext())
        {
            db.Database.Migrate(LastRenamePrecedingMigration);

            db.Database.ExecuteSql(
                $"INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) VALUES ({projectId}, 'legacy', '/tmp/legacy', '', {DateTime.UtcNow})");
            db.Database.ExecuteSql(
                $"INSERT INTO ContextEntries (Id, ProjectId, Type, Title, Content, CreatedAt, UpdatedAt, CurrentVersion) VALUES ({legacyIdeaId}, {projectId}, 6, 'vector cache', 'Origin: chat, 2026-10-13.', {DateTime.UtcNow}, {DateTime.UtcNow}, 0)");
            db.Database.ExecuteSql(
                $"INSERT INTO ContextEntries (Id, ProjectId, Type, Title, Content, CreatedAt, UpdatedAt, CurrentVersion) VALUES ({auditId}, {projectId}, 7, 'audit row', 'body', {DateTime.UtcNow}, {DateTime.UtcNow}, 0)");

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            var entries = db.ContextEntries.ToDictionary(e => e.Id, e => e.Type);

            entries[legacyIdeaId].ShouldBe(EntryType.Idea);
            entries[auditId].ShouldBe(EntryType.Audit);
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
