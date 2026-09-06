using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class AppSettingsServiceTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundtripsValue()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);

        await settings.SetAsync(SettingKeys.EmbeddingProvider, "ollama", CancellationToken.None);
        var value = await settings.GetAsync(SettingKeys.EmbeddingProvider, CancellationToken.None);

        value.ShouldBe("ollama");
    }

    [Fact]
    public async Task GetOrDefaultAsync_ReturnsDefault_WhenKeyMissing()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);

        var value = await settings.GetOrDefaultAsync(
            SettingKeys.QdrantEndpoint, SettingDefaults.QdrantEndpoint, CancellationToken.None);

        value.ShouldBe(SettingDefaults.QdrantEndpoint);
        (await settings.HasAsync(SettingKeys.QdrantEndpoint, CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task SetAsync_OverwritesExistingKey_WithoutDuplicating()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);

        await settings.SetAsync(SettingKeys.QdrantEndpoint, "http://127.0.0.1:6334", CancellationToken.None);
        await settings.SetAsync(SettingKeys.QdrantEndpoint, "http://127.0.0.1:7777", CancellationToken.None);

        await using var verify = _temp.CreateContext();
        var value = await new AppSettingsService(verify).GetAsync(SettingKeys.QdrantEndpoint, CancellationToken.None);

        value.ShouldBe("http://127.0.0.1:7777");
    }

    public void Dispose() => _temp.Dispose();
}
