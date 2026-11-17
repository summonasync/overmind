using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Overmind.Infrastructure.Tests.Entries;

public sealed class EntryLinkTriageTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly TriageCapture _capture = new();
    private readonly OvermindDbContext _context;
    private readonly ITestOutputHelper _output;

    private readonly Guid _journalId;
    private readonly Guid _otherProjectId;
    private readonly Guid _tombstoneId;

    private readonly Guid _unlinkedId;
    private readonly Guid _outgoingId;
    private readonly Guid _incomingId;
    private readonly Guid _incomingSourceId;
    private readonly Guid _buriedTargetId;
    private readonly Guid _danglingSourceId;
    private readonly Guid _inactiveSourceId;
    private readonly Guid _namedByInactiveId;
    private readonly Guid _manualNoteId;
    private readonly Guid _manualCounterpartId;
    private readonly Guid _projectLinkId;

    public EntryLinkTriageTests(ITestOutputHelper output)
    {
        _output = output;
        _context = _temp.CreateContext(_capture);

        var journal = Project.Create("Journal", "/tmp/triage/journal");
        var other = Project.Create("triage-other", "/tmp/triage/other");
        var feature = PlanningFeature.Create(other.Id, "184-triage", "Triage pin", "edges");
        _context.Projects.AddRange(journal, other);
        _context.PlanningFeatures.Add(feature);

        var unlinked = AddNote(journal.Id, "Unlinked Note", "no edge anywhere");
        var outgoing = AddNote(journal.Id, "Outgoing Only", "links [[feature:184]]");
        var incomingSource = AddNote(journal.Id, "Incoming Source", "reads [[Incoming Only]]");
        var incoming = AddNote(journal.Id, "Incoming Only", "no marker of its own");
        var buriedTarget = AddNote(journal.Id, "Buried Target", "only a buried note mentions it");
        var danglingSource = AddNote(journal.Id, "Dangling Source", "mentions a note that no longer exists");
        var inactiveSource = AddNote(journal.Id, "Inactive Source", "lifted after naming [[Named By Inactive]]");
        var namedByInactive = AddNote(journal.Id, "Named By Inactive", "plain body");
        var manualNote = AddNote(journal.Id, "Manual Note", "edge inserted by hand");
        var manualCounterpart = AddNote(journal.Id, "Manual Counterpart", "hand-linked back");
        var projectLinker = AddNote(journal.Id, "Project Linker", "links [[project:triage-other]]");
        var doomed = AddNote(journal.Id, "Deleted Target", "row the fixture removes");
        _context.SaveChanges();

        _journalId = journal.Id;
        _otherProjectId = other.Id;
        _unlinkedId = unlinked.Id;
        _outgoingId = outgoing.Id;
        _incomingId = incoming.Id;
        _incomingSourceId = incomingSource.Id;
        _buriedTargetId = buriedTarget.Id;
        _danglingSourceId = danglingSource.Id;
        _inactiveSourceId = inactiveSource.Id;
        _namedByInactiveId = namedByInactive.Id;
        _manualNoteId = manualNote.Id;
        _manualCounterpartId = manualCounterpart.Id;
        _projectLinkId = projectLinker.Id;

        var doomedId = doomed.Id;
        _context.ContextEntries.Remove(doomed);

        var tombstone = ContextEntry.CreateTombstone(buriedTarget, PlanningLogSource.Ui, "ui", "buried on purpose");
        _context.ContextEntries.Add(tombstone);
        _context.SaveChanges();
        _tombstoneId = tombstone.Id;

        _context.EntryLinks.AddRange(
            EntryLink.CreateDerived(outgoing.Id, EntryLinkKind.Feature, feature.Id),
            EntryLink.CreateDerived(incomingSource.Id, EntryLinkKind.Entry, incoming.Id),
            EntryLink.CreateDerived(danglingSource.Id, EntryLinkKind.Entry, doomedId),
            EntryLink.CreateDerived(tombstone.Id, EntryLinkKind.Entry, buriedTarget.Id),
            EntryLink.CreateDerived(tombstone.Id, EntryLinkKind.Project, other.Id),
            EntryLink.CreateDerived(projectLinker.Id, EntryLinkKind.Project, other.Id));
        _context.SaveChanges();

        // The lift is what stops the source from counting; the edge it carries stays in the table untouched.
        inactiveSource.Lift("replaced", "ui", null, namedByInactive.Id);
        _context.EntryLinks.Add(EntryLink.CreateDerived(inactiveSource.Id, EntryLinkKind.Entry, namedByInactive.Id));
        _context.SaveChanges();

        ManualEdge(manualNote.Id, manualCounterpart.Id);
    }

    public void Dispose()
    {
        _context.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public async Task OutgoingOnlyNote_IsLinked_ByTheSourceArm()
    {
        (await LinkedIds()).ShouldContain(_outgoingId);
        (await UnlinkedIds()).ShouldNotContain(_outgoingId);
    }

    [Fact]
    public async Task IncomingOnlyNote_IsLinked_ByTheTargetArm()
    {
        (await LinkedIds()).ShouldContain(_incomingId);
        (await UnlinkedIds()).ShouldNotContain(_incomingId);
    }

    [Fact]
    public async Task HandWrittenEdge_CountsExactlyLikeADerivedOne()
    {
        var linked = await LinkedIds();

        linked.ShouldContain(_manualNoteId);
        linked.ShouldContain(_manualCounterpartId);
    }

    [Fact]
    public async Task EdgeFromATombstoneRow_LeavesItsTargetUnsorted()
    {
        (await UnlinkedIds()).ShouldContain(_buriedTargetId);
    }

    [Fact]
    public async Task EdgeFromALiftedRow_LinksNothing_OnEitherEnd()
    {
        var unlinked = await UnlinkedIds();

        unlinked.ShouldContain(_namedByInactiveId);
        unlinked.ShouldContain(_inactiveSourceId);
    }

    [Fact]
    public async Task EdgeWhoseTargetRowIsGone_LeavesTheSourceUnsorted()
    {
        (await UnlinkedIds()).ShouldContain(_danglingSourceId);
    }

    [Fact]
    public async Task LinkedAndUnlinked_PartitionTheStream_NoRowInBoth_NoRowMissing()
    {
        var linked = await LinkedIds();
        var unlinked = await UnlinkedIds();
        var every = await NoteIdsAsync();

        linked.Intersect(unlinked).ShouldBeEmpty();
        linked.Concat(unlinked).OrderBy(id => id).ShouldBe(every.OrderBy(id => id));
        unlinked.ShouldContain(_unlinkedId);
    }

    [Fact]
    public async Task ProjectFilter_FindsTheDerivedProjectEdge_HoldsTheTombstoneOut()
    {
        var matched = await Triage()
            .WhereLinkedToProject(_context.ContextEntries, _otherProjectId)
            .Select(entry => entry.Id)
            .ToListAsync();

        matched.ShouldContain(_projectLinkId);
        matched.ShouldNotContain(_tombstoneId);
    }

    [Fact]
    public async Task TriageFilter_IsOneCorrelatedExistsPair_NoPerRowRoundTrip()
    {
        _capture.Clear();
        await Triage().WhereLinked(Notes()).ToListAsync();

        var sql = _capture.Statements.ShouldHaveSingleItem();
        _output.WriteLine(sql);

        CountOf(sql, "FROM \"EntryLinks\" AS").ShouldBe(2, "one arm per direction, and nothing else reads the table");
        sql.ShouldContain("\"e\".\"FromEntryId\" = \"c\".\"Id\"", Case.Sensitive);
        sql.ShouldContain("\"e0\".\"ToKind\" = 0 AND \"e0\".\"ToId\" = \"c\".\"Id\"", Case.Sensitive);
        sql.ShouldNotContain("IN (SELECT", Case.Sensitive);
    }

    [Fact]
    public async Task TriageArms_RideTheExistingEdgeIndexes_NothingScansTheEdgeTable()
    {
        await Triage().WhereLinked(Notes()).ToListAsync();

        var plan = await ExplainAsync(_capture.LastCommand!);
        _output.WriteLine(plan);

        plan.ShouldContain("IX_EntryLinks_ToKind_ToId", Case.Sensitive);
        plan.ShouldNotContain("SCAN EntryLinks", Case.Sensitive);
    }

    [Fact]
    public async Task ProjectFilterArm_RidesTheOutgoingIndex()
    {
        await Triage().WhereLinkedToProject(_context.ContextEntries, _otherProjectId).ToListAsync();

        var plan = await ExplainAsync(_capture.LastCommand!);
        _output.WriteLine(plan);

        plan.ShouldContain("IX_EntryLinks_FromEntryId_ToKind_ToId", Case.Sensitive);
    }

    private IQueryable<ContextEntry> Notes() =>
        _context.ContextEntries.Where(entry => entry.ProjectId == _journalId && entry.Type == EntryType.Journal);

    private Task<List<Guid>> LinkedIds() => Triage().WhereLinked(Notes()).Select(e => e.Id).ToListAsync();

    private Task<List<Guid>> UnlinkedIds() => Triage().WhereUnlinked(Notes()).Select(e => e.Id).ToListAsync();

    private Task<List<Guid>> NoteIdsAsync() => Notes().Select(e => e.Id).ToListAsync();

    private EntryLinkTriage Triage() => new(_context);

    private ContextEntry AddNote(Guid projectId, string title, string content)
    {
        var note = ContextEntry.Create(projectId, EntryType.Journal, title, content);
        _context.ContextEntries.Add(note);
        return note;
    }

    private void ManualEdge(Guid fromEntryId, Guid toEntryId) =>
        _context.Database.ExecuteSqlRaw(
            """
            INSERT INTO EntryLinks (Id, FromEntryId, ToKind, ToId, Origin, CreatedAt)
            VALUES ({0}, {1}, {2}, {3}, {4}, {5})
            """,
            Guid.NewGuid().ToString().ToUpperInvariant(),
            fromEntryId.ToString().ToUpperInvariant(),
            (int)EntryLinkKind.Entry,
            toEntryId.ToString().ToUpperInvariant(),
            (int)EntryLinkOrigin.Manual,
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffff"));

    private static int CountOf(string haystack, string needle)
    {
        var count = 0;
        var index = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private async Task<string> ExplainAsync(CapturedCommand captured)
    {
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + captured.Sql;

        foreach (var parameter in captured.Parameters)
        {
            var copy = command.CreateParameter();
            copy.ParameterName = parameter.Name;
            copy.Value = parameter.Value ?? DBNull.Value;
            command.Parameters.Add(copy);
        }

        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(3));
        }

        return string.Join(" | ", rows);
    }

    private sealed record CapturedCommand(string Sql, IReadOnlyList<(string Name, object? Value)> Parameters);

    private sealed class TriageCapture : DbCommandInterceptor
    {
        public List<string> Statements { get; } = [];

        public CapturedCommand? LastCommand { get; private set; }

        public void Clear() => Statements.Clear();

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Record(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Record(DbCommand command)
        {
            Statements.Add(command.CommandText.Replace('\n', ' '));
            LastCommand = new CapturedCommand(
                command.CommandText,
                [.. command.Parameters.Cast<DbParameter>().Select(p => (p.ParameterName, p.Value))]);
        }
    }
}
