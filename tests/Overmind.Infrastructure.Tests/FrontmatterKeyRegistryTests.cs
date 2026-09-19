using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class FrontmatterKeyRegistryTests : IDisposable
{
    private readonly TempDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task ListAsync_FreshDatabase_SeedsAllSeedKeysWithTheirColors()
    {
        using var db = _database.CreateContext();
        var registry = CreateRegistry(db);

        var keys = await registry.ListAsync(CancellationToken.None);

        keys.Count.ShouldBe(FrontmatterKeyPalette.SeedKeys.Count);
        foreach (var seed in FrontmatterKeyPalette.SeedKeys)
        {
            var key = keys.Single(k => k.Name == seed.Name);
            key.Color.ShouldBe(seed.Color);
        }
    }

    [Fact]
    public async Task ListAsync_CalledTwice_SeedsExactlyOnce()
    {
        using var db = _database.CreateContext();
        var registry = CreateRegistry(db);

        await registry.ListAsync(CancellationToken.None);
        var keys = await registry.ListAsync(CancellationToken.None);

        keys.Count.ShouldBe(FrontmatterKeyPalette.SeedKeys.Count);
    }

    [Fact]
    public async Task GetOrRegisterAsync_UnknownKey_ReturnsNewWithPaletteColorAndPersists()
    {
        FrontmatterKeyRegistration first;
        using (var db = _database.CreateContext())
        {
            first = await CreateRegistry(db).GetOrRegisterAsync("my_key", CancellationToken.None);
        }

        first.IsNew.ShouldBeTrue();
        FrontmatterKeyPalette.Colors.ShouldContain(first.Color);
        first.Color.ShouldBe(FrontmatterKeyPalette.ColorFor("my_key"));

        using var verify = _database.CreateContext();
        var stored = await verify.FrontmatterKeys.SingleAsync(k => k.Name == "my_key");
        stored.Color.ShouldBe(first.Color);
    }

    [Fact]
    public async Task GetOrRegisterAsync_ExistingKey_ReturnsNotNewWithSameColor()
    {
        string firstColor;
        using (var db = _database.CreateContext())
        {
            firstColor = (await CreateRegistry(db).GetOrRegisterAsync("my_key", CancellationToken.None)).Color;
        }

        using var secondDb = _database.CreateContext();
        var second = await CreateRegistry(secondDb).GetOrRegisterAsync("my_key", CancellationToken.None);

        second.IsNew.ShouldBeFalse();
        second.Color.ShouldBe(firstColor);
    }

    [Fact]
    public async Task GetOrRegisterAsync_SameNameAcrossDatabases_ReturnsSameColor()
    {
        using var otherDatabase = new TempDatabase();

        string firstColor;
        using (var db = _database.CreateContext())
        {
            firstColor = (await CreateRegistry(db).GetOrRegisterAsync("profile_key", CancellationToken.None)).Color;
        }

        using var otherDb = otherDatabase.CreateContext();
        var second = await CreateRegistry(otherDb).GetOrRegisterAsync("profile_key", CancellationToken.None);

        second.Color.ShouldBe(firstColor);
    }

    [Fact]
    public async Task GetOrRegisterAsync_CaseVariants_DedupeToSingleKey()
    {
        using (var db = _database.CreateContext())
        {
            var seeded = await CreateRegistry(db).GetOrRegisterAsync("Depends_On", CancellationToken.None);
            seeded.IsNew.ShouldBeFalse();
            seeded.Color.ShouldBe(FrontmatterKeyPalette.SeedKeys.Single(s => s.Name == "depends_on").Color);
        }

        using (var db = _database.CreateContext())
        {
            var first = await CreateRegistry(db).GetOrRegisterAsync("MY_Key", CancellationToken.None);
            first.IsNew.ShouldBeTrue();
        }

        using (var db = _database.CreateContext())
        {
            var second = await CreateRegistry(db).GetOrRegisterAsync(" my_key ", CancellationToken.None);
            second.IsNew.ShouldBeFalse();
        }

        using var verify = _database.CreateContext();
        var count = await verify.FrontmatterKeys.CountAsync(k => k.Name == "my_key");
        count.ShouldBe(1);
    }

    private static FrontmatterKeyRegistry CreateRegistry(OvermindDbContext db) =>
        new(db, Substitute.For<ILogger<FrontmatterKeyRegistry>>());
}
