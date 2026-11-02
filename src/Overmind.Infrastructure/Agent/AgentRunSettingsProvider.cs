using System.Globalization;
using Overmind.Infrastructure.Configuration;

namespace Overmind.Infrastructure.Agent;

public sealed class AgentRunSettingsProvider(AppSettingsService settings) : IAgentRunSettingsProvider
{
    public async Task<AgentRunSettings> GetAsync(CancellationToken ct)
    {
        var alias = await settings.GetOrDefaultAsync(SettingKeys.AgentAlias, SettingDefaults.AgentAlias, ct);
        var perTurn = await ReadIntAsync(SettingKeys.AgentPerTurnMaxTokens, SettingDefaults.AgentPerTurnMaxTokens, ct);
        var daily = await ReadIntAsync(SettingKeys.AgentDailyBudget, SettingDefaults.AgentDailyBudget, ct);
        return new AgentRunSettings(alias.Trim(), perTurn, daily);
    }

    private async Task<int> ReadIntAsync(string key, int fallback, CancellationToken ct)
    {
        var raw = await settings.GetOrDefaultAsync(key, fallback.ToString(CultureInfo.InvariantCulture), ct);
        return int.TryParse(raw, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;
    }
}
