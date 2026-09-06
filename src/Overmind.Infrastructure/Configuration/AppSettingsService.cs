using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Configuration;

public sealed class AppSettingsService
{
    private readonly OvermindDbContext _dbContext;

    public AppSettingsService(OvermindDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct)
    {
        return await _dbContext.AppSettings
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string> GetOrDefaultAsync(string key, string defaultValue, CancellationToken ct)
    {
        return await GetAsync(key, ct) ?? defaultValue;
    }

    public async Task<bool> HasAsync(string key, CancellationToken ct)
    {
        return await _dbContext.AppSettings.AnyAsync(s => s.Key == key, ct);
    }

    public async Task SetAsync(string key, string value, CancellationToken ct)
    {
        var existing = await _dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing is null)
        {
            _dbContext.AppSettings.Add(AppSetting.Create(key, value));
        }
        else
        {
            existing.UpdateValue(value);
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
