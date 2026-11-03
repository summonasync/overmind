using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class SettingsTelegramApiTests : IDisposable
{
    private const string SecretTokenPart = "ABCDEFsecretvalue99";
    private const string TestToken = $"111222:{SecretTokenPart}";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public SettingsTelegramApiTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Get_FreshInstance_ReportsDisabledDefaults()
    {
        var settings = await _client.GetFromJsonAsync<TelegramSettingsJson>("/api/settings", JsonOptions);

        settings!.TelegramEnabled.ShouldBeFalse();
        settings.TelegramBotTokenConfigured.ShouldBeFalse();
        settings.TelegramAllowedChatIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_ConfiguresToggleTokenAndAllowlist_Roundtrips()
    {
        var put = await _client.PutAsJsonAsync("/api/settings", new
        {
            TelegramEnabled = true,
            TelegramBotToken = TestToken,
            TelegramAllowedChatIds = new[] { 42L, 43L },
        });

        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await put.Content.ReadFromJsonAsync<TelegramSettingsJson>(JsonOptions);
        updated!.TelegramEnabled.ShouldBeTrue();
        updated.TelegramBotTokenConfigured.ShouldBeTrue();
        updated.TelegramAllowedChatIds.ShouldBe([42L, 43L]);

        var fetched = await _client.GetFromJsonAsync<TelegramSettingsJson>("/api/settings", JsonOptions);
        fetched!.TelegramEnabled.ShouldBeTrue();
        fetched.TelegramBotTokenConfigured.ShouldBeTrue();
        fetched.TelegramAllowedChatIds.ShouldBe([42L, 43L]);
    }

    [Fact]
    public async Task Responses_NeverEchoTheBotToken()
    {
        await _client.PutAsJsonAsync("/api/settings", new
        {
            TelegramEnabled = true,
            TelegramBotToken = TestToken,
        });

        var putBody = await (await _client.PutAsJsonAsync("/api/settings", new
        {
            TelegramEnabled = false,
        })).Content.ReadAsStringAsync();
        var getBody = await _client.GetStringAsync("/api/settings");

        putBody.ShouldNotContain(SecretTokenPart);
        getBody.ShouldNotContain(SecretTokenPart);
        getBody.ShouldContain("\"telegramBotTokenConfigured\":true");
    }

    [Fact]
    public async Task Put_EmptyToken_KeepsStoredToken()
    {
        await _client.PutAsJsonAsync("/api/settings", new { TelegramBotToken = TestToken });
        await _client.PutAsJsonAsync("/api/settings", new { TelegramBotToken = string.Empty });

        var fetched = await _client.GetFromJsonAsync<TelegramSettingsJson>("/api/settings", JsonOptions);
        fetched!.TelegramBotTokenConfigured.ShouldBeTrue();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private sealed record TelegramSettingsJson(
        bool TelegramEnabled,
        bool TelegramBotTokenConfigured,
        long[] TelegramAllowedChatIds);
}
