using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class FrontmatterKeyRegistry
{
    private readonly OvermindDbContext _db;
    private readonly ILogger<FrontmatterKeyRegistry> _logger;

    public FrontmatterKeyRegistry(OvermindDbContext db, ILogger<FrontmatterKeyRegistry> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FrontmatterKey>> ListAsync(CancellationToken ct)
    {
        await EnsureSeededAsync(ct);

        return await _db.FrontmatterKeys
            .AsNoTracking()
            .OrderBy(k => k.Name)
            .ToListAsync(ct);
    }

    public async Task<FrontmatterKeyRegistration> GetOrRegisterAsync(string name, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalized = name.Trim().ToLowerInvariant();

        await EnsureSeededAsync(ct);

        var existing = await _db.FrontmatterKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Name == normalized, ct);
        if (existing is not null)
        {
            return new FrontmatterKeyRegistration(existing.Color, IsNew: false);
        }

        var key = FrontmatterKey.Create(normalized, FrontmatterKeyPalette.ColorFor(normalized));
        _db.FrontmatterKeys.Add(key);

        try
        {
            await _db.SaveChangesAsync(ct);

            return new FrontmatterKeyRegistration(key.Color, IsNew: true);
        }
        catch (DbUpdateException ex) when (UniqueViolation.IsOn(ex, "FrontmatterKeys.Name"))
        {
            _logger.LogWarning(ex, "Concurrent registration of frontmatter key {KeyName}; taking the existing row", normalized);
            DetachTrackedKeys();

            var raced = await _db.FrontmatterKeys
                .AsNoTracking()
                .FirstAsync(k => k.Name == normalized, ct);

            return new FrontmatterKeyRegistration(raced.Color, IsNew: false);
        }
    }

    private async Task EnsureSeededAsync(CancellationToken ct)
    {
        if (await _db.FrontmatterKeys.AnyAsync(ct))
        {
            return;
        }

        foreach (var seed in FrontmatterKeyPalette.SeedKeys)
        {
            _db.FrontmatterKeys.Add(FrontmatterKey.Create(seed.Name, seed.Color));
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (UniqueViolation.IsOn(ex, "FrontmatterKeys.Name"))
        {
            _logger.LogWarning(ex, "Concurrent seeding of frontmatter keys; keeping the existing rows");
            DetachTrackedKeys();
        }
    }

    private void DetachTrackedKeys()
    {
        foreach (var entry in _db.ChangeTracker.Entries<FrontmatterKey>())
        {
            entry.State = EntityState.Detached;
        }
    }
}
