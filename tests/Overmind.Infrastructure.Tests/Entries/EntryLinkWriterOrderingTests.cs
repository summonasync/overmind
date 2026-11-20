using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Entries;

// The recompute deletes its old edges with ExecuteDeleteAsync, which bypasses the change tracker entirely: rows a
// caller already loaded stay tracked as if the database still held them. Task 11's review proved the order in the
// code and pinned it at the HTTP layer, but the behavioural pin it left behind could not fail — a tracked
// Unchanged row is never re-INSERTed, so deleting the AsNoTracking flag kept every test green. This file pins what
// actually holds: the delete runs first, the surviving-edge read runs after it, the fresh rows are inserted after
// that, and the row the tracker still believes in stays dead.
public sealed class EntryLinkWriterOrderingTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly StatementLog _log = new();
    private readonly OvermindDbContext _context;
    private readonly Guid _sourceId;
    private readonly Guid _featureId;

    public EntryLinkWriterOrderingTests()
    {
        _context = _temp.CreateContext(_log);

        var home = Project.Create("Ordering Home", "/tmp/ordering-home");
        var journal = Project.Create("Journal", "/tmp/ordering-journal");
        _context.Projects.AddRange(home, journal);
        _context.PlanningFeatures.Add(PlanningFeature.Create(home.Id, "184-order", "Ordering", "body"));
        _context.ContextEntries.Add(ContextEntry.Create(home.Id, EntryType.Journal, "Source", "seed"));
        _context.SaveChanges();

        _sourceId = _context.ContextEntries.Single().Id;
        _featureId = _context.PlanningFeatures.Single().Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public async Task RecomputeAfterATrackedEdgeRead_DeletesBeforeItReadsOrInserts_AndTheStaleRowStaysDead()
    {
        await WriteAsync("[[feature:184]]");

        // The dangerous pre-condition, built on purpose: the caller reads the edges WITH tracking, so the change
        // tracker holds the exact row ExecuteDeleteAsync is about to remove without telling it.
        var stale = (await _context.EntryLinks
            .Where(link => link.FromEntryId == _sourceId)
            .ToListAsync()).Single();
        stale.Origin.ShouldBe(EntryLinkOrigin.Derived);

        _log.Reset();

        // Same marker as before, so the recompute re-derives the very (FromEntryId, ToKind, ToId) triple the
        // tracker still believes exists — the composite unique index is where a resurrected row would collide.
        var written = await WriteAsync("[[feature:184]]");

        written.ShouldBe(1);
        var survivors = await FreshRowsAsync();
        survivors.ShouldHaveSingleItem("nothing may be left behind and nothing may come back");
        survivors[0].Id.ShouldNotBe(stale.Id,
            "the surviving edge is a freshly inserted row, not the tracked one the delete bypassed");
        survivors[0].ToId.ShouldBe(_featureId);

        var touched = _log.EntryLinkStatements();
        touched.ShouldNotBeEmpty("the recompute has to touch the edge table for any of this to mean anything");
        var deleteAt = IndexOf(touched, IsDelete);
        var selectAt = IndexOf(touched, IsSelect);
        var insertAt = IndexOf(touched, IsInsert);

        // Existence before order: a statement nobody emitted would score -1 and every comparison below would then
        // pass on nothing.
        deleteAt.ShouldBeGreaterThanOrEqualTo(0, $"no delete reached the edge table: {string.Join(" || ", touched)}");
        selectAt.ShouldBeGreaterThanOrEqualTo(0, "the surviving-edge read is part of the unit");
        insertAt.ShouldBeGreaterThanOrEqualTo(0, "the re-derived edge has to land in the same unit");
        deleteAt.ShouldBe(0,
            "the tracked rows must be gone from the database before the recompute reads or writes the table " +
            $"again: {string.Join(" || ", touched)}");
        selectAt.ShouldBeGreaterThan(deleteAt, "the read sees the table as the delete left it");
        insertAt.ShouldBeGreaterThan(selectAt, "the insert is decided by what that read saw");
    }

    [Fact]
    public async Task RecomputeAfterATrackedEdgeRead_KeepsTheManualEdgeAndSkipsTheDuplicate_TheReadRunsOnTheLiveTable()
    {
        // The companion claim: the surviving-edge read is not decoration, and it has to run against the table as
        // the delete left it. The unique index covers (FromEntryId, ToKind, ToId) without Origin, so a manual edge
        // and a derived edge cannot share a target — drop that read, or answer it from anything other than the
        // live table, and the re-derived row collides instead of being skipped.
        var dailyId = await SeedJournalTitleAsync("Daily");
        await SeedManualEdgeAsync(_featureId);

        var both = "[[feature:184]] [[Daily]]";
        await WriteAsync(both);

        var before = await FreshRowsAsync();
        before.Count.ShouldBe(2, "the manual edge owns the feature slot, the derived one lands on the title");
        var derivedBefore = before.Single(row => row.Origin == EntryLinkOrigin.Derived);
        derivedBefore.ToId.ShouldBe(dailyId);

        await _context.EntryLinks.Where(link => link.FromEntryId == _sourceId).ToListAsync();

        _log.Reset();
        var written = await WriteAsync(both);

        written.ShouldBe(1, "only the title edge is re-derivable; the feature slot is taken");
        var after = await FreshRowsAsync();
        after.Count.ShouldBe(2);
        var derivedAfter = after.Single(row => row.Origin == EntryLinkOrigin.Derived);
        derivedAfter.Id.ShouldNotBe(derivedBefore.Id);
        after.Single(row => row.Origin == EntryLinkOrigin.Manual).ToId.ShouldBe(_featureId);

        var touched = _log.EntryLinkStatements();
        var deleteAt = IndexOf(touched, IsDelete);
        var selectAt = IndexOf(touched, IsSelect);
        var insertAt = IndexOf(touched, IsInsert);

        deleteAt.ShouldBe(0,
            $"the delete opens the unit even when a manual edge is in the way: {string.Join(" || ", touched)}");
        selectAt.ShouldBeGreaterThan(deleteAt, "the surviving-edge read must follow the delete");
        insertAt.ShouldBeGreaterThan(selectAt, "and the insert is decided by what that read saw");
    }

    private async Task<Guid> SeedJournalTitleAsync(string title)
    {
        var journalProjectId = await _context.Projects
            .Where(p => p.Name == JournalProjectStore.ProjectName)
            .Select(p => p.Id)
            .SingleAsync();

        var seeded = ContextEntry.Create(journalProjectId, EntryType.Journal, title, "the owner's line");
        _context.ContextEntries.Add(seeded);
        await _context.SaveChangesAsync();
        return seeded.Id;
    }

    private Task SeedManualEdgeAsync(Guid targetId) =>
        // The provider writes Guid as uppercase TEXT, so a raw insert has to match that casing.
        _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO EntryLinks (Id, FromEntryId, ToKind, ToId, Origin, CreatedAt)
            VALUES ({0}, {1}, {2}, {3}, {4}, {5})
            """,
            Guid.NewGuid().ToString().ToUpperInvariant(),
            _sourceId.ToString().ToUpperInvariant(),
            (int)EntryLinkKind.Feature,
            targetId.ToString().ToUpperInvariant(),
            (int)EntryLinkOrigin.Manual,
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffff"));

    private async Task<int> WriteAsync(string body)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();

        var entry = await _context.ContextEntries.SingleAsync(e => e.Id == _sourceId);
        entry.UpdateContent(body);
        var written = await Writer().ReplaceDerivedAsync(_sourceId, WikiLinkParser.Parse(body), tx);

        await tx.CommitAsync();
        return written;
    }

    private async Task<List<EntryLink>> FreshRowsAsync()
    {
        _context.ChangeTracker.Clear();
        return await _context.EntryLinks.AsNoTracking()
            .Where(link => link.FromEntryId == _sourceId)
            .ToListAsync();
    }

    private EntryLinkWriter Writer() =>
        new(_context, new EntryLinkResolver(_context, new JournalProjectStore(_context)));

    private static bool IsDelete(string statement) =>
        statement.Contains("DELETE FROM \"EntryLinks\"", StringComparison.Ordinal);

    private static bool IsSelect(string statement) =>
        statement.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
        statement.Contains("\"EntryLinks\"", StringComparison.Ordinal);

    private static bool IsInsert(string statement) =>
        statement.StartsWith("INSERT INTO \"EntryLinks\"", StringComparison.Ordinal);

    private static int IndexOf(string[] statements, Func<string, bool> match)
    {
        for (var index = 0; index < statements.Length; index++)
        {
            if (match(statements[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private sealed class StatementLog : DbCommandInterceptor
    {
        private readonly List<string> _statements = [];

        public void Reset() => _statements.Clear();

        public string[] EntryLinkStatements() =>
            [.. _statements.Where(statement => statement.Contains("EntryLinks", StringComparison.Ordinal))];

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Record(command);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Record(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Record(command);
            return base.ScalarExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Record(DbCommand command) => _statements.Add(command.CommandText.Replace('\n', ' ').Trim());
    }
}

// INJECTION PROOF (task 12, AC-13 transferred from task 11). Edits to
// src/Overmind.Infrastructure/Entries/EntryLinkWriter.cs, each run against this file, each restored:
//
//   1. Order flip — hoist the manual-key SELECT above ExecuteDeleteAsync: the first fact goes red on
//      "the tracked rows must be gone from the database before the recompute reads or writes the table again",
//      the second on "the surviving-edge read must follow the delete".
//   2. Delete last — move ExecuteDeleteAsync below AddRange/SaveChanges: red on the order assertion and on the
//      composite unique index, because the re-derived (FromEntryId, ToKind, ToId) triple collides with the row the
//      delete has not removed yet.
//   3. Read dropped — delete the manual-key SELECT and the skip that consumes it: the second fact goes red with
//      UNIQUE constraint failed: EntryLinks.FromEntryId, EntryLinks.ToKind, EntryLinks.ToId.
//   4. The AsNoTracking arm — delete `.AsNoTracking()` at the manual-key read: BOTH facts stay green, which is the
//      point. That read projects into an anonymous type, so no EntryLink entity enters the tracker in either mode
//      and the flag cannot discriminate; the contract forbids presenting it as the pin. The tracker hazard is
//      covered by the tracked pre-read these facts perform themselves.
//
// Runs: ~/scratch-184/t12-inject-order-*.log and ~/scratch-184/t12-inject-tracking-*.log.
