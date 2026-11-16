using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Entries;

public sealed class EntryLinkNonAsciiTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly CapturingInterceptor _capture = new();
    private readonly OvermindDbContext _context;
    private readonly Guid _journalProjectId;
    private readonly Guid _sourceId;
    private readonly Guid _noteId;
    private readonly Guid _precomposedYoId;
    private readonly Guid _decomposedYoId;

    public EntryLinkNonAsciiTests()
    {
        _context = _temp.CreateContext(_capture);

        var journal = Project.Create("Journal", "/tmp/nonascii/journal");
        var grimoire = Project.Create("Гримоар", "/tmp/nonascii/grimoire");
        _context.Projects.AddRange(journal, grimoire);

        foreach (var title in new[] { "Источник", "Моя Заметка", "Моя Ёлка", "Моя Е\u0308лка" })
        {
            _context.ContextEntries.Add(
                ContextEntry.Create(journal.Id, EntryType.Journal, title, "кириллица"));
        }

        _context.SaveChanges();

        _journalProjectId = journal.Id;
        _noteId = _context.ContextEntries.Single(e => e.Title == "Моя Заметка").Id;
        _precomposedYoId = _context.ContextEntries.Single(e => e.Title == "Моя Ёлка").Id;
        _decomposedYoId = _context.ContextEntries.Single(e => e.Title == "Моя Е\u0308лка").Id;
        _sourceId = _context.ContextEntries.Single(e => e.Title == "Источник").Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _temp.Dispose();
    }

    [Theory]
    [InlineData("[[моя заметка]]")]
    [InlineData("[[Моя Заметка]]")]
    [InlineData("[[МОЯ ЗАМЕТКА]]")]
    public async Task TitleMarker_ResolvesCyrillicRegardlessOfCasing(string marker)
    {
        var links = await ResolveAsync(marker);

        links.ShouldHaveSingleItem();
        links.Single().Kind.ShouldBe(EntryLinkKind.Entry);
        links.Single().ToId.ShouldBe(_noteId);
    }

    [Theory]
    [InlineData("[[project:гримоар]]")]
    [InlineData("[[project:Гримоар]]")]
    [InlineData("[[project:ГРИМОАР]]")]
    public async Task ProjectNameMarker_ResolvesCyrillicRegardlessOfCasing(string marker)
    {
        var links = await ResolveAsync(marker);

        links.ShouldHaveSingleItem();
        links.Single().Kind.ShouldBe(EntryLinkKind.Project);
    }

    [Theory]
    [InlineData("[[Моя Ёлка]]", 1)]
    [InlineData("[[моя ёлка]]", 1)]
    [InlineData("[[МОЯ ЁЛКА]]", 1)]
    [InlineData("[[моя елка]]", 0)]
    [InlineData("[[Моя Елка]]", 0)]
    [InlineData("[[моя е\u0308лка]]", 1)]
    public async Task CyrillicLetterYo_FoldsCaseOnly_NeverTheLetterNorTheCombiningMarks(
        string marker, int expected)
    {
        var links = await ResolveAsync(marker);

        links.Count.ShouldBe(expected);
    }

    [Fact]
    public async Task PrecomposedAndDecomposedTitles_RemainTwoSeparateTargets()
    {
        var precomposed = await ResolveAsync("[[Моя Ёлка]]");
        var decomposed = await ResolveAsync("[[Моя Е\u0308лка]]");

        precomposed.ShouldHaveSingleItem();
        decomposed.ShouldHaveSingleItem();
        precomposed.Single().ToId.ShouldBe(_precomposedYoId);
        decomposed.Single().ToId.ShouldBe(_decomposedYoId);
    }

    [Fact]
    public async Task CyrillicComparison_NeverMovesIntoTheTranslatedQuery()
    {
        _capture.Statements.Clear();
        await ResolveAsync("[[моя заметка]] [[project:гримоар]]");

        var titleQueries = Statements("ContextEntries");
        var projectQueries = Statements("Projects");

        titleQueries.ShouldNotBeEmpty();
        projectQueries.ShouldNotBeEmpty();
        titleQueries.ShouldAllBe(text => !text.Contains("lower(", StringComparison.OrdinalIgnoreCase));
        projectQueries.ShouldAllBe(text => !text.Contains("lower(", StringComparison.OrdinalIgnoreCase));
        titleQueries.ShouldAllBe(text => !AfterWhere(text).Contains("\"Title\""));
        projectQueries.ShouldAllBe(text => !AfterWhere(text).Contains("\"Name\""));
    }

    private List<string> Statements(string table) =>
        [.. _capture.Statements.Where(text => text.Contains(table, StringComparison.Ordinal))];

    private static string AfterWhere(string sql)
    {
        var index = sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
        return index < 0 ? string.Empty : sql[index..];
    }

    [Fact]
    public async Task ReplaceDerived_CyrillicBody_PersistsTheEdge()
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        var entry = await _context.ContextEntries.SingleAsync(e => e.Id == _sourceId);
        entry.UpdateContent("ссылка на [[моя заметка]]");

        var writer = new EntryLinkWriter(
            _context, new EntryLinkResolver(_context, new JournalProjectStore(_context)));
        var written = await writer.ReplaceDerivedAsync(
            _sourceId, WikiLinkParser.Parse(entry.Content), tx);
        await tx.CommitAsync();

        written.ShouldBe(1);
        var row = await _context.EntryLinks.AsNoTracking()
            .Where(link => link.FromEntryId == _sourceId)
            .SingleAsync();
        row.ToId.ShouldBe(_noteId);
    }

    private Task<IReadOnlyList<ResolvedLink>> ResolveAsync(string body) =>
        new EntryLinkResolver(_context, new JournalProjectStore(_context))
            .ResolveAsync(_sourceId, WikiLinkParser.Parse(body));

    private sealed class CapturingInterceptor : DbCommandInterceptor
    {
        public List<string> Statements { get; } = [];

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Statements.Add(command.CommandText);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Statements.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
