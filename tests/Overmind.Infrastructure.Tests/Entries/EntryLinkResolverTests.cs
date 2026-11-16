using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Entries;

public sealed class EntryLinkResolverTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly OvermindDbContext _context;
    private readonly Guid _journalProjectId;
    private readonly Guid _sourceId;

    public EntryLinkResolverTests()
    {
        _context = _temp.CreateContext();

        var project = Project.Create("Overmind", "/tmp/overmind-resolver");
        var journal = Project.Create("Journal", "/tmp/overmind-resolver/journal");
        _context.Projects.AddRange(project, journal);
        _context.PlanningFeatures.Add(PlanningFeature.Create(
            project.Id, "184-journal-journal", "Journal edges", "wikilinks"));

        _context.ContextEntries.Add(
            ContextEntry.Create(journal.Id, EntryType.Journal, "Source", "seed"));
        _context.SaveChanges();

        _journalProjectId = journal.Id;
        _sourceId = _context.ContextEntries.Single().Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _temp.Dispose();
    }

    [Theory]
    [InlineData("[[feature:184]]", 1)]
    [InlineData("[[FEATURE:184]]", 1)]
    [InlineData("[[Feature:0184]]", 1)]
    [InlineData("[[feature:184-journal-journal]]", 1)]
    [InlineData("[[FEATURE:184-journal-journal]]", 1)]
    [InlineData("[[project:Overmind]]", 1)]
    [InlineData("[[PROJECT:Overmind]]", 1)]
    [InlineData("[[project:overmind]]", 1)]
    [InlineData("[[Some Note]]", 1)]
    [InlineData("[[some note]]", 1)]
    [InlineData("[[feature:404]]", 0)]
    [InlineData("[[feature:184-journal-journal-journal]]", 0)]
    [InlineData("[[project:Ghost]]", 0)]
    [InlineData("[[feature:99999999999999]]", 0)]
    [InlineData("[[project:22222222-2222-2222-2222-222222222222]]", 0)]
    public async Task Resolve_KnownAndUnknownMarkers_AgreeWithTheParser(string marker, int expected)
    {
        var seeded = ContextEntry.Create(_journalProjectId, EntryType.Journal, "Some Note", "body");
        _context.ContextEntries.Add(seeded);
        await _context.SaveChangesAsync();

        var links = await ResolveAsync(marker);

        links.Count.ShouldBe(expected);
    }

    [Theory]
    [InlineData("```csharp\n[[feature:184]]\n```")]
    [InlineData("~~~\n[[feature:184]]\n```")]
    [InlineData("    ```\n[[feature:184]]\n```")]
    [InlineData("[[feature:184]\n```\nno close")]
    [InlineData("`[[feature:184]]`")]
    [InlineData("``` [[feature:184]]")]
    public async Task Resolve_FencedAndInlinedMarkers_AreSkippedByTheParser(string body)
    {
        var links = await ResolveAsync(body);

        links.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolve_LinkAfterAnUnopenedCodeRun_StaysLive()
    {
        var links = await ResolveAsync("a ` [[feature:184]]");

        links.Single().Kind.ShouldBe(EntryLinkKind.Feature);
    }

    [Fact]
    public async Task Resolve_BraceGuidForm_ResolvesThroughTheNormalizedKey()
    {
        var project = Project.Create("Grimoire", "/tmp/grimoire");
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        var links = await ResolveAsync("[[project:{" + project.Id + "}]]");

        links.ShouldNotBeEmpty();
        links.Single().Kind.ShouldBe(EntryLinkKind.Project);
        links.Single().ToId.ShouldBe(project.Id);
    }

    [Fact]
    public async Task Resolve_NestedOpenMarker_KeepsTheParsersKeyAndStaysUnresolved()
    {
        var links = await ResolveAsync("[[a and [[b]]");

        links.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolve_SameNumberInTwoProjects_IsAmbiguousAndUnresolved()
    {
        var other = Project.Create("Other", "/tmp/other");
        _context.Projects.Add(other);
        _context.PlanningFeatures.Add(PlanningFeature.Create(other.Id, "184-other", "Other", "body"));
        await _context.SaveChangesAsync();

        var links = await ResolveAsync("[[feature:184]]");

        links.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Journal")]
    [InlineData("journal")]
    [InlineData("JOURNAL")]
    public async Task ResolveProjectMarker_CaseVariantsCoexist_ExactCaseWinsThenEarliestCreatedAt(string marker)
    {
        var variantId = Guid.NewGuid();
        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt)
            VALUES ({0}, {1}, {2}, {3}, {4})
            """,
            variantId.ToString().ToUpperInvariant(),
            "journal",
            "/tmp/lowercase-journal",
            "case-variant seed",
            "2026-01-01 00:00:00");

        var links = await ResolveAsync($"[[project:{marker}]]");

        var expected = marker == "journal" || marker == "JOURNAL" ? variantId : _journalProjectId;
        links.ShouldHaveSingleItem();
        links.Single().Kind.ShouldBe(EntryLinkKind.Project);
        links.Single().ToId.ShouldBe(expected);
    }

    [Fact]
    public async Task Resolve_SameTitleTwice_TakesTheMostRecentlyUpdated()
    {
        var older = ContextEntry.Create(_journalProjectId, EntryType.Journal, "Daily", "older");
        var newer = ContextEntry.Create(_journalProjectId, EntryType.Journal, "daily", "newer");
        _context.ContextEntries.AddRange(older, newer);
        await _context.SaveChangesAsync();
        await Task.Delay(10);

        newer.UpdateContent("newest");
        await _context.SaveChangesAsync();

        var links = await ResolveAsync("[[Daily]]");

        links.Single().ToId.ShouldBe(newer.Id);
    }

    [Fact]
    public async Task Resolve_MarkerThatResolvesToTheSourceEntry_IsDropped()
    {
        var links = await ResolveAsync("[[Source]]");

        links.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolve_PaddedDigits_MatchTheThreeDigitSlugPrefix()
    {
        var padded = PlanningFeature.Create(_journalProjectId, "007-padded", "Padded", "body");
        _context.PlanningFeatures.Add(padded);
        await _context.SaveChangesAsync();

        (await ResolveAsync("[[feature:7]]")).Single().ToId.ShouldBe(padded.Id);
        (await ResolveAsync("[[feature:007]]")).Single().ToId.ShouldBe(padded.Id);
    }

    private async Task<IReadOnlyList<ResolvedLink>> ResolveAsync(string body) =>
        await Resolver().ResolveAsync(_sourceId, WikiLinkParser.Parse(body));

    private EntryLinkResolver Resolver() =>
        new(_context, new JournalProjectStore(_context));
}
