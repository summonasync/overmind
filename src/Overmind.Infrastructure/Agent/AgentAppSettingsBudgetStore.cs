using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Agent;

public sealed class AgentAppSettingsBudgetStore(OvermindDbContext db) : IAgentBudgetStore
{
    public async Task<long> GetUsedAsync(DateOnly day, CancellationToken ct)
    {
        var raw = await db.AppSettings
            .AsNoTracking()
            .Where(s => s.Key == Key(day))
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
        return long.TryParse(raw, CultureInfo.InvariantCulture, out var used) ? used : 0;
    }

    public async Task<long> AddUsageAsync(DateOnly day, long tokens, CancellationToken ct)
    {
        var key = Key(day);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO AppSettings (Key, Value)
            VALUES ({key}, {tokens.ToString(CultureInfo.InvariantCulture)})
            ON CONFLICT(Key) DO UPDATE SET Value = CAST(CAST(Value AS INTEGER) + {tokens.ToString(CultureInfo.InvariantCulture)} AS TEXT)
            """, ct);
        return await GetUsedAsync(day, ct);
    }

    private static string Key(DateOnly day) =>
        SettingKeys.AgentDailyBudgetUsedPrefix + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
}
