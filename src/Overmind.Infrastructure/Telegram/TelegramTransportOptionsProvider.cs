using System.Globalization;
using Overmind.Infrastructure.Configuration;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramTransportOptionsProvider(AppSettingsService settings)
{
    public async Task<TelegramTransportOptions> GetAsync(CancellationToken ct)
    {
        var enabledRaw = await settings.GetOrDefaultAsync(
            SettingKeys.TelegramEnabled, SettingDefaults.TelegramEnabled, ct);
        var token = await settings.GetAsync(SettingKeys.TelegramBotToken, ct);
        var chatIdsRaw = await settings.GetOrDefaultAsync(
            SettingKeys.TelegramAllowedChatIds, SettingDefaults.TelegramAllowedChatIds, ct);

        return new TelegramTransportOptions
        {
            Enabled = bool.TryParse(enabledRaw, out var enabled) && enabled,
            BotToken = token,
            AllowedChatIds = ParseChatIds(chatIdsRaw),
        };
    }

    public static IReadOnlyList<long> ParseChatIds(string raw) => raw
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(id => long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : (long?)null)
        .Where(value => value is not null)
        .Select(value => value!.Value)
        .Distinct()
        .ToArray();
}
