using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

internal static class EntryWriteCommit
{
    private const int ReservedProjectRaceAttempts = 3;
    private const string ReservedProjectConstraint = "Projects.Name";
    private const int SqliteConstraintErrorCode = 19;

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
        ILoggerFactory logs,
        Func<CancellationToken, Task<Work>> plan,
        CancellationToken ct)
    {
        var logger = logs.CreateLogger("EntryWriteCommit");
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
                // Two sessions racing for the reserved Journal project both see this. Without the line an
                // operator only sees a save that took three tries, so the loser of the race names itself.
                logger.LogWarning(
                    "Entry write attempt {Attempt} of {Attempts} lost the reserved-project race on constraint " +
                    "{Constraint}; retrying.",
                    attempt,
                    ReservedProjectRaceAttempts,
                    ReservedProjectConstraint);
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
            }
        }
    }

    internal static bool IsReservedProjectRace(DbUpdateException exception) =>
        UniqueViolation.IsOn(exception, ReservedProjectConstraint) &&
        exception.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintErrorCode };
}
