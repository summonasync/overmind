using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host;

// One-shot startup pass over the search-normalized titles. The migration adds the columns with an
// empty default because SQLite's lower() cannot fold Cyrillic inside SQL — the C# invariant lower
// is the only fold that works, so existing rows get re-folded here and every later write keeps the
// value correct at the entity level. Cheap after the first boot: the scan reads three light columns
// per row and the update pass shrinks to nothing. StartAsync runs the pass to completion on purpose —
// the API must not serve searches until the stored fold is consistent (this is not a BackgroundService).
public sealed class EntryTitleBackfillService(
    IServiceScopeFactory scopeFactory,
    ILogger<EntryTitleBackfillService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();

            // Hosts built without the Program.cs migration step can point at a store the migration
            // has not reached yet; the fold pass is meaningless there and must not fail the boot.
            if (!await HasColumnAsync(db, "ContextEntries", "TitleNormalized", cancellationToken))
            {
                logger.LogWarning(
                    "TitleNormalized is missing on ContextEntries; migrations have not reached this store, skipping the backfill");
                return;
            }

            var entries = await db.ContextEntries
                .Select(entry => new { entry.Id, entry.Title, entry.TitleNormalized })
                .ToListAsync(cancellationToken);
            var fixedEntries = 0;
            foreach (var row in entries.Where(row =>
                         !string.Equals(row.TitleNormalized, ContextEntry.NormalizeTitle(row.Title), StringComparison.Ordinal)))
            {
                await db.ContextEntries
                    .Where(entry => entry.Id == row.Id)
                    .ExecuteUpdateAsync(
                        set => set.SetProperty(entry => entry.TitleNormalized, ContextEntry.NormalizeTitle(row.Title)),
                        cancellationToken);
                fixedEntries++;
            }

            if (fixedEntries > 0)
            {
                logger.LogInformation("Title backfill re-folded {Entries} entries", fixedEntries);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Title backfill cancelled mid-pass; the stored fold is partially repaired");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Entry title backfill failed; refusing to serve searches over an inconsistent fold");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<bool> HasColumnAsync(
        OvermindDbContext db, string table, string column, CancellationToken ct)
    {
        var connection = (Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table})";
        var reader = await command.ExecuteReaderAsync(ct);
        await using var borrowed = reader;
        while (await reader.ReadAsync(ct))
        {
            if (reader.GetString(1) == column)
            {
                return true;
            }
        }

        return false;
    }
}
