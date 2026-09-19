using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class FrontmatterKeyMigrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-fmkey-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_CreatesFrontmatterKeysTable_WithCaseInsensitiveUniqueName()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();
        }

        using var context = CreateContext();
        context.Database.ExecuteSql(
            $"INSERT INTO FrontmatterKeys (Id, Name, Color, CreatedAt) VALUES ({Guid.NewGuid()}, 'STATUS', '#111111', {DateTime.UtcNow})");

        var stored = context.FrontmatterKeys.Single(k => k.Name == "status");
        stored.Name.ShouldBe("STATUS");
        stored.Color.ShouldBe("#111111");

        Should.Throw<SqliteException>(() =>
            context.Database.ExecuteSql(
                $"INSERT INTO FrontmatterKeys (Id, Name, Color, CreatedAt) VALUES ({Guid.NewGuid()}, 'status', '#222222', {DateTime.UtcNow})"));
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
