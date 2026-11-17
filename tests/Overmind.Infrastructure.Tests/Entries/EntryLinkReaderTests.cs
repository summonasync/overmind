using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Entries;

public sealed class EntryLinkReaderTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly StatementCapture _capture = new();
    private readonly OvermindDbContext _context;
    private readonly Project _project;
    private readonly Guid _targetId;
    private readonly Guid _firstSourceId;
    private readonly Guid _secondSourceId;
    private readonly Guid _featureId;

    public EntryLinkReaderTests()
    {
        _context = _temp.CreateContext(_capture);
        _project = Project.Create("Overmind", "/tmp/reader/project");
        var journal = Project.Create("Journal", "/tmp/reader/journal");
        var feature = PlanningFeature.Create(_project.Id, "184-reader-edges", "Reader edges", "wikilinks");
        _context.Projects.AddRange(_project, journal);
        _context.PlanningFeatures.Add(feature);

        var target = ContextEntry.Create(journal.Id, EntryType.Journal, "Anchor Note", "target");
        var first = ContextEntry.Create(journal.Id, EntryType.Journal, "First Source", "links [[Anchor Note]]");
        var second = ContextEntry.Create(journal.Id, EntryType.Journal, "Second Source", "links [[Anchor Note]]");
        _context.ContextEntries.AddRange(target, first, second);
        _context.SaveChanges();

        _targetId = target.Id;
        _firstSourceId = first.Id;
        _secondSourceId = second.Id;
        _featureId = feature.Id;

        _context.EntryLinks.AddRange(
            EntryLink.CreateDerived(first.Id, EntryLinkKind.Entry, target.Id),
            EntryLink.CreateDerived(second.Id, EntryLinkKind.Entry, target.Id),
            EntryLink.CreateDerived(first.Id, EntryLinkKind.Feature, feature.Id),
            EntryLink.CreateDerived(first.Id, EntryLinkKind.Project, _project.Id));
        _context.SaveChanges();
        _capture.Statements.Clear();
    }

    public void Dispose()
    {
        _context.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public async Task Backlinks_OneQuery_ForBothSources_FilteredOnTargetKeysNotOnBodies()
    {
        var sources = await Reader().LoadBacklinkSourcesAsync(_targetId);

        sources.Count.ShouldBe(2);
        sources.ShouldContain(source => source.Id == _firstSourceId);
        sources.ShouldContain(source => source.Id == _secondSourceId);
        sources.ShouldAllBe(source => source.Content.Contains("[[Anchor Note]]", StringComparison.Ordinal));

        var sql = _capture.Statements.ShouldHaveSingleItem();
        sql.ShouldContain("EntryLinks", Case.Sensitive);
        sql.ShouldContain("ContextEntries", Case.Sensitive);
        sql.ShouldNotContain("LIKE");
    }

    [Fact]
    public async Task Backlinks_ExcludeNonActiveSources()
    {
        var superseded = await _context.ContextEntries.SingleAsync(e => e.Id == _secondSourceId);
        superseded.Lift("replaced", "ui", null, _firstSourceId);
        await _context.SaveChangesAsync();
        _capture.Statements.Clear();

        var sources = await Reader().LoadBacklinkSourcesAsync(_targetId);

        sources.ShouldHaveSingleItem();
        sources[0].Id.ShouldBe(_firstSourceId);
    }

    [Fact]
    public async Task Backlinks_QueryRidesTheToKindToIdIndex()
    {
        await Reader().LoadBacklinkSourcesAsync(_targetId);

        var plan = await ExplainAsync(_capture.LastCommand!);

        plan.ShouldContain("IX_EntryLinks_ToKind_ToId", Case.Sensitive);
    }

    [Fact]
    public async Task Labels_OneQueryPerPresentKind_NothingForEmptyInput()
    {
        var labels = await Reader().LoadTargetLabelsAsync(
        [
            new ResolvedLink(EntryLinkKind.Entry, _targetId),
            new ResolvedLink(EntryLinkKind.Feature, _featureId),
            new ResolvedLink(EntryLinkKind.Project, _project.Id)
        ]);

        labels[(EntryLinkKind.Entry, _targetId)].ShouldBe("Anchor Note");
        labels[(EntryLinkKind.Feature, _featureId)].ShouldBe("Reader edges");
        labels[(EntryLinkKind.Project, _project.Id)].ShouldBe("Overmind");
        _capture.Statements.Count.ShouldBe(3);

        _capture.Statements.Clear();
        await Reader().LoadTargetLabelsAsync([new ResolvedLink(EntryLinkKind.Entry, _targetId)]);
        _capture.Statements.ShouldHaveSingleItem();

        _capture.Statements.Clear();
        (await Reader().LoadTargetLabelsAsync([])).ShouldBeEmpty();
        _capture.Statements.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadMode_NoJournalProject_FindsNoTitle_AndWritesNothing()
    {
        using var fresh = new TempDatabase();
        using var context = fresh.CreateContext(_capture);

        _capture.Statements.Clear();
        var outcomes = await new EntryLinkResolver(context, new JournalProjectStore(context))
            .ResolveOutcomesAsync(Guid.NewGuid(), WikiLinkParser.Parse("links [[Anchor Note]]"), false);
        var readStatements = _capture.Statements.ToArray();

        outcomes.ShouldHaveSingleItem();
        outcomes[0].ToId.ShouldBeNull();
        readStatements.ShouldHaveSingleItem();
        readStatements[0].ShouldContain("Projects", Case.Sensitive);
        readStatements.ShouldAllBe(statement => !statement.Contains("INSERT", StringComparison.Ordinal));
        (await context.Projects.AnyAsync(p => p.Name == JournalProjectStore.ProjectName)).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteMode_StillReservesTheJournalProject()
    {
        using var fresh = new TempDatabase();
        using var context = fresh.CreateContext();

        await new EntryLinkResolver(context, new JournalProjectStore(context))
            .ResolveAsync(Guid.NewGuid(), WikiLinkParser.Parse("links [[Anchor Note]]"));

        (await context.Projects.AnyAsync(p => p.Name == JournalProjectStore.ProjectName)).ShouldBeTrue();
    }

    [Fact]
    public async Task ResolveOutcomes_SelfTarget_ComesBackResolvedNotUnresolved()
    {
        var outcomes = await new EntryLinkResolver(_context, new JournalProjectStore(_context))
            .ResolveOutcomesAsync(_targetId, WikiLinkParser.Parse("self [[Anchor Note]]"), true);

        outcomes.ShouldHaveSingleItem();
        outcomes[0].ToId.ShouldBe(_targetId);
    }

    private EntryLinkReader Reader() => new(_context);

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

    private sealed class StatementCapture : DbCommandInterceptor
    {
        public List<string> Statements { get; } = [];

        public CapturedCommand? LastCommand { get; private set; }

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
            Statements.Add(command.CommandText);
            LastCommand = new CapturedCommand(
                command.CommandText,
                [.. command.Parameters.Cast<DbParameter>().Select(p => (p.ParameterName, p.Value))]);
        }
    }
}
