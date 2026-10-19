using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class GrimoireSettingsTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    [Fact]
    public async Task GetOrDefaultAsync_UnsetGrimoireKeys_ReturnsSaneDefaults()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);

        (await settings.GetOrDefaultAsync(
            SettingKeys.GrimoireOverridePath, SettingDefaults.GrimoireOverridePath, CancellationToken.None))
            .ShouldBe(string.Empty);
        (await settings.GetOrDefaultAsync(
            SettingKeys.GrimoireBookName, SettingDefaults.GrimoireBookName, CancellationToken.None))
            .ShouldBe(string.Empty);
        (await settings.GetOrDefaultAsync(
            SettingKeys.GrimoireWired, SettingDefaults.GrimoireWired, CancellationToken.None))
            .ShouldBe("false");
        (await settings.GetOrDefaultAsync(
            SettingKeys.GrimoireInstallChannel, SettingDefaults.GrimoireInstallChannel, CancellationToken.None))
            .ShouldBe(string.Empty);
        (await settings.HasAsync(SettingKeys.GrimoireWired, CancellationToken.None)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(SettingKeys.GrimoireOverridePath, "/opt/grimoire/grimoire")]
    [InlineData(SettingKeys.GrimoireBookName, "owner-book")]
    [InlineData(SettingKeys.GrimoireWired, "true")]
    [InlineData(SettingKeys.GrimoireInstallChannel, "dotnet-tool")]
    public async Task SetAsync_ThenGetAsync_RoundtripsGrimoireKeys(string key, string value)
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);

        await settings.SetAsync(key, value, CancellationToken.None);

        (await settings.GetAsync(key, CancellationToken.None)).ShouldBe(value);
    }

    public void Dispose() => _temp.Dispose();
}
