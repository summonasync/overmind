using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Overmind.Infrastructure.Tests;

public sealed class FrontmatterKeyLiveProfileTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-fmkey-live-{Guid.NewGuid():N}.db");

    public FrontmatterKeyLiveProfileTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Migrate_OnCopyOfLiveProfile_CreatesRegistryAndSeeds()
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

        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        await using var verify = CreateContext();
        var registry = new FrontmatterKeyRegistry(verify, Substitute.For<ILogger<FrontmatterKeyRegistry>>());
        var keys = await registry.ListAsync(CancellationToken.None);

        keys.Count.ShouldBe(FrontmatterKeyPalette.SeedKeys.Count);
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
