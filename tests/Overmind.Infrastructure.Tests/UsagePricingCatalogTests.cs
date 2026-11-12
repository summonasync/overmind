using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class UsagePricingCatalogTests : IDisposable
{
    private readonly TempDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Defaults_CoverTheClaudeRows()
    {
        UsagePricingCatalog.Defaults.Count.ShouldBe(3);

        var opus = UsagePricingCatalog.Defaults["claude-opus-5"];
        opus.Input.ShouldBe(5.00m);
        opus.Output.ShouldBe(25.00m);
        opus.CacheRead.ShouldBe(0.50m);
        opus.CacheWrite.ShouldBe(1.25m);

        var sonnet = UsagePricingCatalog.Defaults["claude-sonnet-5"];
        sonnet.Input.ShouldBe(2.00m);
        sonnet.Output.ShouldBe(10.00m);

        var haiku = UsagePricingCatalog.Defaults["claude-haiku-4-5"];
        haiku.Input.ShouldBe(1.00m);
        haiku.Output.ShouldBe(5.00m);
        haiku.CacheRead.ShouldBe(0.10m);
        haiku.CacheWrite.ShouldBe(0.25m);
    }

    [Fact]
    public void Parse_LooksUpModelNamesCaseInsensitively()
    {
        var catalog = UsagePricingCatalog.Parse(
            """{ "GLM-5": { "input": 1, "output": 2, "cacheRead": 0, "cacheWrite": 0 } }""");

        catalog.TryGetValue("glm-5", out var price).ShouldBeTrue();
        price!.Input.ShouldBe(1m);
    }

    [Fact]
    public async Task LoadAsync_WithoutSetting_ReturnsDefaults()
    {
        await using var context = _db.CreateContext();

        var pricing = await new UsagePricingCatalog(new AppSettingsService(context)).LoadAsync(default);

        pricing.Count.ShouldBe(3);
        pricing["claude-sonnet-5"].Output.ShouldBe(10.00m);
    }

    [Fact]
    public async Task LoadAsync_CustomSetting_ReturnsTheParsedTable()
    {
        await using var context = _db.CreateContext();
        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.UsagePricing, """{ "glm-5": { "input": 3, "output": 9, "cacheRead": 0, "cacheWrite": 0 } }""", default);

        var pricing = await new UsagePricingCatalog(settings).LoadAsync(default);

        pricing.Count.ShouldBe(1);
        pricing["glm-5"].Input.ShouldBe(3m);
        pricing["glm-5"].Output.ShouldBe(9m);
    }

    [Fact]
    public async Task LoadAsync_CorruptSetting_FallsBackToDefaults()
    {
        await using var context = _db.CreateContext();
        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.UsagePricing, "{ not json", default);

        var pricing = await new UsagePricingCatalog(settings).LoadAsync(default);

        pricing.Count.ShouldBe(3);
        pricing["claude-haiku-4-5"].Input.ShouldBe(1.00m);
    }
}
