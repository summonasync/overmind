using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Entries;

public sealed class WikiLinkFidelityTheoryTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly OvermindDbContext _context;
    private readonly Guid _projectId;
    private readonly Guid _journalProjectId;
    private readonly Guid _sourceId;
    private readonly Guid _featureId;
    private readonly Guid _dailyId;

    public WikiLinkFidelityTheoryTests()
    {
        _context = _temp.CreateContext();

        var project = Project.Create("Overmind", "/tmp/overmind-fidelity");
        _context.Projects.Add(project);
        _context.Projects.Add(Project.Create("Journal", "/tmp/overmind-fidelity/journal"));
        _context.PlanningFeatures.Add(PlanningFeature.Create(
            project.Id, "184-journal-journal", "Journal edges", "wikilinks"));
        _projectId = project.Id;

        _context.SaveChanges();
        _journalProjectId = _context.Projects.Single(p => p.Name == "Journal").Id;
        _featureId = _context.PlanningFeatures.Single().Id;

        foreach (var title in new[] { "Source", "Daily", "МОЯ ЗАМЕТКА" })
        {
            _context.ContextEntries.Add(
                ContextEntry.Create(_journalProjectId, EntryType.Journal, title, "fidelity seed"));
        }
        _context.SaveChanges();

        _sourceId = _context.ContextEntries.Single(e => e.Title == "Source").Id;
        _dailyId = _context.ContextEntries.Single(e => e.Title == "Daily").Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _temp.Dispose();
    }

    [Theory]
    [InlineData("```csharp\n[[feature:184]]\n```\n[[feature:184]]", 1)]
    [InlineData("~~~\n[[feature:184]]\n```\n[[feature:184]]", 0)]
    [InlineData("   ```\n[[feature:184]]\n   ```\n[[feature:184]]", 1)]
    [InlineData("`[[feature:184]]` [[feature:184]]", 1)]
    [InlineData("`` [[feature:184]] `` [[feature:184]]", 1)]
    [InlineData("a ` b [[feature:184]]", 1)]
    [InlineData("[[feature:184", 0)]
    [InlineData("[[a and [[b]]", 0)]
    [InlineData("[[FEATURE:184]] [[feature:184]]", 1)]
    [InlineData("[[feature:0184]]", 1)]
    [InlineData("[[feature:184-JOURNAL-JOURNAL]]", 0)]
    [InlineData("[[feature:184]] [[feature:184-journal-journal]]", 1)]
    [InlineData("[[Моя заметка]] [[моя заметка]] [[МОЯ ЗАМЕТКА]]", 1)]
    public async Task ResolverCountsEdgesFromWhatTheParserHandedIt(string body, int expected)
    {
        var links = await Resolver().ResolveForWriteAsync(_sourceId, WikiLinkParser.Parse(body));

        links.Count.ShouldBe(expected);
    }

    [Theory]
    [InlineData("[[Daily]]", EntryLinkKind.Entry)]
    [InlineData("[[daily]]", EntryLinkKind.Entry)]
    [InlineData("[[Моя Заметка]]", EntryLinkKind.Entry)]
    [InlineData("[[моя заметка]]", EntryLinkKind.Entry)]
    [InlineData("[[PROJECT:Overmind]]", EntryLinkKind.Project)]
    [InlineData("[[project:overmind]]", EntryLinkKind.Project)]
    [InlineData("[[Feature:184]]", EntryLinkKind.Feature)]
    public async Task PrefixFormsFoldCase_KeysDoNot(string body, EntryLinkKind kind)
    {
        var links = await Resolver().ResolveForWriteAsync(_sourceId, WikiLinkParser.Parse(body));

        links.ShouldHaveSingleItem();
        links.Single().Kind.ShouldBe(kind);
    }

    [Fact]
    public async Task CaseVariantMarkers_ShareTheReservedProjectRow()
    {
        var links = await Resolver().ResolveForWriteAsync(
            _sourceId, WikiLinkParser.Parse("[[Daily]] [[daily]]"));

        links.ShouldHaveSingleItem();
        links.Single().ToId.ShouldBe(_dailyId);
    }

    [Fact]
    public async Task BracedGuid_ReachesTheResolverAsNormalizedId()
    {
        var links = await Resolver().ResolveForWriteAsync(
            _sourceId, WikiLinkParser.Parse($"[[project:{{{_projectId}}}]]"));

        links.ShouldHaveSingleItem();
        links.Single().ToId.ShouldBe(_projectId);
    }

    [Fact]
    public async Task OverflowedDigits_FollowTheSlugPath_AndNeverThrow()
    {
        var links = await Resolver().ResolveForWriteAsync(
            _sourceId, WikiLinkParser.Parse("[[feature:99999999999999999999]]"));

        links.ShouldBeEmpty();
    }

    [Fact]
    public async Task Writer_WritesExactlyWhatTheParserAndResolverAgreedOn()
    {
        var body = "```csharp\n[[Daily]]\n```\n\n`[[Daily]]` and [[Daily]] [[feature:184]]";
        await using var tx = await _context.Database.BeginTransactionAsync();

        var entry = await _context.ContextEntries.SingleAsync(e => e.Id == _sourceId);
        entry.UpdateContent(body);
        var written = await Writer().ReplaceDerivedAsync(_sourceId, WikiLinkParser.Parse(body), tx);
        await tx.CommitAsync();

        written.ShouldBe(2);
        WikiLinkParser.Parse(body).Count.ShouldBe(2);

        var rows = await _context.EntryLinks.AsNoTracking()
            .Where(link => link.FromEntryId == _sourceId)
            .ToListAsync();

        TargetId(rows, EntryLinkKind.Entry).ShouldBe(_dailyId);
        TargetId(rows, EntryLinkKind.Feature).ShouldBe(_featureId);
    }

    private EntryLinkResolver Resolver() => new(_context, new JournalProjectStore(_context));

    private EntryLinkWriter Writer() => new(_context, Resolver());

    private static Guid TargetId(IReadOnlyList<EntryLink> rows, EntryLinkKind kind) =>
        rows.Single(link => link.ToKind == kind).ToId;
}
