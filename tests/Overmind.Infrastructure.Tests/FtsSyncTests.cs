using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Search;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public class FtsSyncTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly Guid _projectId;
    private readonly Guid _otherProjectId;

    public FtsSyncTests()
    {
        using var ctx = _db.CreateContext();

        var project = Project.Create("main", "/main");
        var other = Project.Create("other", "/other");
        ctx.Projects.AddRange(project, other);
        _projectId = project.Id;
        _otherProjectId = other.Id;
        ctx.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Insert_BecomesSearchable()
    {
        await SeedEntryAsync("walrus title", "the quick brown fox jumps");

        var hits = await SearchAsync(null, "walrus");
        hits.Count.ShouldBe(1);
        hits[0].Title.ShouldBe("walrus title");
        hits[0].Type.ShouldBe(EntryType.Note);
    }

    [Fact]
    public async Task Update_OldContentGone_NewContentFound()
    {
        var entryId = await SeedEntryAsync("notes", "alpha bravo charlie");

        using (var ctx = _db.CreateContext())
        {
            var entry = await ctx.ContextEntries.SingleAsync(e => e.Id == entryId);
            entry.UpdateContent("delta echo foxtrot");
            await ctx.SaveChangesAsync();
        }

        (await SearchAsync(null, "bravo")).ShouldBeEmpty();
        (await SearchAsync(null, "foxtrot")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Delete_EntryNoLongerFound()
    {
        var entryId = await SeedEntryAsync("doomed", "uniquezebrastring appears here");

        using (var ctx = _db.CreateContext())
        {
            var entry = await ctx.ContextEntries.SingleAsync(e => e.Id == entryId);
            ctx.ContextEntries.Remove(entry);
            await ctx.SaveChangesAsync();
        }

        (await SearchAsync(null, "uniquezebrastring")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_FiltersByProject()
    {
        await SeedEntryAsync("shared-term-a", "sharedterm one");
        await SeedEntryAsync("shared-term-b", "sharedterm two", _otherProjectId);

        var all = await SearchAsync(null, "sharedterm");
        all.Count.ShouldBe(2);

        var scoped = await SearchAsync(_projectId, "sharedterm");
        scoped.Count.ShouldBe(1);
        scoped[0].Title.ShouldBe("shared-term-a");
    }

    [Fact]
    public async Task Search_RespectsLimit()
    {
        for (var i = 0; i < 5; i++)
        {
            await SeedEntryAsync($"limit-{i}", "limitme common");
        }

        var hits = await SearchAsync(null, "limitme", 2);
        hits.Count.ShouldBe(2);
    }

    private async Task<Guid> SeedEntryAsync(string title, string content, Guid? projectId = null)
    {
        using var ctx = _db.CreateContext();
        var entry = ContextEntry.Create(projectId ?? _projectId, EntryType.Note, title, content);
        ctx.ContextEntries.Add(entry);
        await ctx.SaveChangesAsync();
        return entry.Id;
    }

    private async Task<IReadOnlyList<EntrySearchHit>> SearchAsync(Guid? projectId, string query, int limit = 10)
    {
        await using var ctx = _db.CreateContext();
        var search = new FtsSearchService(ctx);
        return await search.SearchAsync(projectId, query, limit, CancellationToken.None);
    }
}
