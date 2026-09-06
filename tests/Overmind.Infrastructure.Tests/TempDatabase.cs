using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Tests;

public sealed class TempDatabase : IDisposable
{
    private readonly string _path;

    public TempDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"overmind-test-{Guid.NewGuid():N}.db");
        using var context = CreateContext();
        context.Database.Migrate();
    }

    public OvermindDbContext CreateContext()
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
