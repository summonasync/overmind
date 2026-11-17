using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

internal static class EntryWriteCommit
{
    private const int ReservedProjectRaceAttempts = 3;

    internal sealed record Work(
        Guid EntryId,
        string SavedBody,
        Func<CancellationToken, Task<IResult>> SaveAsync,
        IResult? Refused = null)
    {
        public static Work Rejected(IResult result) => new(Guid.Empty, string.Empty, _ => Task.FromResult(result), result);
    }

    public static async Task<IResult> RunAsync(
        OvermindDbContext db,
        EntryLinkWriter writer,
        Func<CancellationToken, Task<Work>> plan,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var work = await plan(ct);
                if (work.Refused is { } refused)
                {
                    return refused;
                }

                var written = await work.SaveAsync(ct);
                await writer.ReplaceDerivedAsync(
                    work.EntryId, WikiLinkParser.Parse(work.SavedBody), transaction, ct);

                await transaction.CommitAsync(ct);
                return written;
            }
            catch (DbUpdateException ex) when (IsReservedProjectRace(ex) && attempt < ReservedProjectRaceAttempts)
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
            }
        }
    }

    private static bool IsReservedProjectRace(DbUpdateException exception) =>
        exception.InnerException is { } inner &&
        inner.Message.Contains("UNIQUE constraint failed: Projects.Name", StringComparison.Ordinal);
}
