using System.Globalization;
using Overmind.Infrastructure.Configuration;

namespace Overmind.Infrastructure.Agent;

public sealed class AgentAppSettingsBudgetStore(AppSettingsService settings) : IAgentBudgetStore
{
    public async Task<long> GetUsedAsync(DateOnly day, CancellationToken ct)
    {
        var raw = await settings.GetAsync(Key(day), ct);
        return long.TryParse(raw, CultureInfo.InvariantCulture, out var used) ? used : 0;
    }

    public async Task<long> AddUsageAsync(DateOnly day, long tokens, CancellationToken ct)
    {
        var used = await GetUsedAsync(day, ct) + tokens;
        await settings.SetAsync(Key(day), used.ToString(CultureInfo.InvariantCulture), ct);
        return used;
    }

    private static string Key(DateOnly day) =>
        SettingKeys.AgentDailyBudgetUsedPrefix + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
}
