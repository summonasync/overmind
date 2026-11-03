using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramTransportOptionsProviderTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    [Fact]
    public async Task GetAsync_FreshStore_ReturnsDisabledDefaults()
    {
        var options = await ReadOptionsAsync();

        options.Enabled.ShouldBeFalse();
        options.BotToken.ShouldBeNull();
        options.AllowedChatIds.ShouldBeEmpty();
        options.IsRunnable.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAsync_ConfiguredValues_Roundtrip()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.TelegramEnabled, "true", CancellationToken.None);
        await settings.SetAsync(SettingKeys.TelegramBotToken, "123456:SECRET", CancellationToken.None);
        await settings.SetAsync(SettingKeys.TelegramAllowedChatIds, "42,43", CancellationToken.None);

        var options = await ReadOptionsAsync();

        options.Enabled.ShouldBeTrue();
        options.BotToken.ShouldBe("123456:SECRET");
        options.AllowedChatIds.ShouldBe([42L, 43L]);
        options.IsRunnable.ShouldBeTrue();
    }

    [Theory]
    [InlineData("42,abc,", 1)]
    [InlineData("42,42,7", 2)]
    [InlineData(" , ,", 0)]
    public async Task ParseChatIds_SkipsMalformedAndDuplicates(string raw, int expectedCount)
    {
        TelegramTransportOptionsProvider.ParseChatIds(raw).Count.ShouldBe(expectedCount);
    }

    private async Task<TelegramTransportOptions> ReadOptionsAsync()
    {
        await using var context = _temp.CreateContext();
        var provider = new TelegramTransportOptionsProvider(new AppSettingsService(context));
        return await provider.GetAsync(CancellationToken.None);
    }

    public void Dispose() => _temp.Dispose();
}
