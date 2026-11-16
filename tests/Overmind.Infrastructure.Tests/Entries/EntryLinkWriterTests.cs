using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Entries;

public sealed class EntryLinkWriterTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly OvermindDbContext _context;
    private readonly Guid _projectId;
    private readonly Guid _featureId;
    private readonly Guid _journalProjectId;
    private readonly Guid _sourceId;

    public EntryLinkWriterTests()
    {
        _context = _temp.CreateContext();

        var project = Project.Create("Overmind", "/tmp/overmind-writer");
        _context.Projects.Add(project);
        _context.PlanningFeatures.Add(PlanningFeature.Create(
            project.Id, "184-journal-journal", "Journal edges", "wikilinks"));
        _projectId = project.Id;
        var journalProject = Project.Create("Journal", "/tmp/overmind-writer/journal");
        _context.Projects.Add(journalProject);
        _journalProjectId = journalProject.Id;

        foreach (var title in new[] { "Source", "Some Note", "Daily" })
        {
            _context.ContextEntries.Add(
                ContextEntry.Create(_journalProjectId, EntryType.Journal, title, "seed"));
        }

        _context.SaveChanges();

        _sourceId = _context.ContextEntries.Single(e => e.Title == "Source").Id;
        _featureId = _context.PlanningFeatures.Single().Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public async Task ReplaceDerived_ThreeMarkerBody_WritesOneRowPerResolvedTarget()
    {
        await WriteAsync("[[feature:184]] [[project:Overmind]] [[Some Note]]");

        var rows = await EdgesAsync();

        rows.Count.ShouldBe(3);
        rows.ShouldAllBe(link => link.Origin == EntryLinkOrigin.Derived);
        TargetId(rows, EntryLinkKind.Feature).ShouldBe(_featureId);
        TargetId(rows, EntryLinkKind.Project).ShouldBe(_projectId);
        TargetId(rows, EntryLinkKind.Entry).ShouldNotBe(_sourceId);
    }

    [Fact]
    public async Task ReplaceDerived_MarkerDroppedFromTheBody_RemovesExactlyThatRow()
    {
        await WriteAsync("[[feature:184]] [[project:Overmind]] [[Some Note]]");
        await WriteAsync("[[feature:184]] [[Some Note]]");

        var rows = await EdgesAsync();

        rows.Select(link => link.ToKind).OrderBy(kind => kind).ShouldBe([EntryLinkKind.Entry, EntryLinkKind.Feature]);
    }

    [Fact]
    public async Task ReplaceDerived_RepeatedWrite_RewritesTheSameEdgeSet()
    {
        await WriteAsync("[[feature:184]] [[project:Overmind]] [[Some Note]]");
        var first = await EdgesAsync();

        await WriteAsync("[[Some Note]] [[project:Overmind]] [[feature:184]]");
        var second = await EdgesAsync();

        second.Count.ShouldBe(first.Count);
        second.Select(link => (link.ToKind, link.ToId)).OrderBy(pair => pair.Item1)
            .ShouldBe(first.Select(link => (link.ToKind, link.ToId)).OrderBy(pair => pair.Item1));
    }

    [Fact]
    public async Task ReplaceDerived_TwoCaseVariantMarkers_CollapseIntoOneRow()
    {
        await WriteAsync("[[Daily]] [[daily]]");

        var rows = await EdgesAsync();

        rows.Count.ShouldBe(1);
        rows[0].ToKind.ShouldBe(EntryLinkKind.Entry);
    }

    [Fact]
    public async Task ReplaceDerived_MarkerResolvingToTheSourceEntry_IsDropped()
    {
        await WriteAsync("[[feature:184]] [[Source]]");

        var rows = await EdgesAsync();

        rows.Single().ToKind.ShouldBe(EntryLinkKind.Feature);
    }

    [Fact]
    public async Task ReplaceDerived_UnresolvedTargets_WriteNothing_AndDoNotThrow()
    {
        await WriteAsync("[[feature:404]] [[project:no-such-project]] [[Nowhere Note]]");

        (await EdgesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceDerived_DigitsBeyondInt32_FallToTheSlugPath_AndDoNotThrow()
    {
        await WriteAsync("[[feature:99999999999999999999]]");

        (await EdgesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceDerived_DeletesOnlyDerivedEdges_ManualEdgeSurvives()
    {
        var dailyId = _context.ContextEntries.Single(e => e.Title == "Daily").Id;
        // The provider writes Guid as uppercase TEXT, so a raw insert has to match that casing.
        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO EntryLinks (Id, FromEntryId, ToKind, ToId, Origin, CreatedAt)
            VALUES ({0}, {1}, {2}, {3}, {4}, {5})
            """,
            Guid.NewGuid().ToString().ToUpperInvariant(),
            _sourceId.ToString().ToUpperInvariant(),
            (int)EntryLinkKind.Entry,
            dailyId.ToString().ToUpperInvariant(),
            (int)EntryLinkOrigin.Manual,
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffff"));

        await WriteAsync("[[feature:184]]");

        var rows = await EdgesAsync();
        rows.Count.ShouldBe(2);
        rows.Single(link => link.Origin == EntryLinkOrigin.Manual).ToId.ShouldBe(dailyId);
        rows.Single(link => link.Origin == EntryLinkOrigin.Derived).ToKind.ShouldBe(EntryLinkKind.Feature);
    }

    [Fact]
    public async Task ReplaceDerived_RolledBackTransaction_LeavesBodyAndEdgesUntouched()
    {
        await WriteAsync("[[feature:184]]");

        var before = await _context.ContextEntries.AsNoTracking().SingleAsync(e => e.Id == _sourceId);
        await using (var tx = await _context.Database.BeginTransactionAsync())
        {
            var entry = await _context.ContextEntries.SingleAsync(e => e.Id == _sourceId);
            entry.UpdateContent("[[Some Note]] [[project:Overmind]]");
            await Writer().ReplaceDerivedAsync(
                _sourceId, WikiLinkParser.Parse(entry.Content), tx);
            await tx.RollbackAsync();
        }

        var after = await _context.ContextEntries.AsNoTracking().SingleAsync(e => e.Id == _sourceId);
        after.Content.ShouldBe(before.Content);

        var rows = await EdgesAsync();
        rows.Single().ToKind.ShouldBe(EntryLinkKind.Feature);
    }

    [Fact]
    public async Task ReplaceDerived_TransactionNotCurrentOnTheContext_Throws()
    {
        await using var opened = await _context.Database.BeginTransactionAsync();
        await opened.CommitAsync();

        var writer = Writer();
        await Should.ThrowAsync<InvalidOperationException>(() =>
            writer.ReplaceDerivedAsync(_sourceId, WikiLinkParser.Parse("[[Daily]]"), opened));
    }

    private EntryLinkWriter Writer() =>
        new(_context, new EntryLinkResolver(_context, new JournalProjectStore(_context)));

    private async Task WriteAsync(string body)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();

        var entry = await _context.ContextEntries.SingleAsync(e => e.Id == _sourceId);
        entry.UpdateContent(body);
        await Writer().ReplaceDerivedAsync(_sourceId, WikiLinkParser.Parse(body), tx);

        await tx.CommitAsync();
    }

    private Task<List<EntryLink>> EdgesAsync() =>
        _context.EntryLinks.AsNoTracking()
            .Where(link => link.FromEntryId == _sourceId)
            .ToListAsync();

    private static Guid TargetId(IReadOnlyList<EntryLink> rows, EntryLinkKind kind) =>
        rows.Single(link => link.ToKind == kind).ToId;
}
