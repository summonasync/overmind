using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Entries;

public sealed class JournalProjectStoreTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task EnsureId_CalledTwice_ReusesTheOneReservedRow()
    {
        using var context = _temp.CreateContext();
        var store = new JournalProjectStore(context);

        var first = await store.EnsureIdAsync();
        var second = await store.EnsureIdAsync();

        first.ShouldBe(second);
        (await context.Projects.CountAsync(p => p.Name == JournalProjectStore.ProjectName)).ShouldBe(1);
    }

    [Fact]
    public async Task EnsureId_ParallelFirstWrites_CollapseOntoTheWinnersRow()
    {
        using var left = _temp.CreateContext();
        using var right = _temp.CreateContext();

        var ids = await Task.WhenAll(
            new JournalProjectStore(left).EnsureIdAsync(),
            new JournalProjectStore(right).EnsureIdAsync());

        var rows = await left.Projects
            .AsNoTracking()
            .Where(p => p.Name == JournalProjectStore.ProjectName)
            .Select(p => p.Id)
            .ToListAsync();

        rows.ShouldHaveSingleItem();
        ids[0].ShouldBe(rows[0]);
        ids[1].ShouldBe(rows[0]);
    }

    [Fact]
    public async Task EnsureId_FindsAnExistingJournalProjectUnderADifferentCasing()
    {
        using var context = _temp.CreateContext();
        context.Projects.Add(Project.Create("journal", "/tmp/journal", "hand-cased row"));
        await context.SaveChangesAsync();

        var store = new JournalProjectStore(context);
        var ensured = await store.EnsureIdAsync();

        (await store.FindIdAsync()).ShouldBe(ensured);
        (await context.Projects.CountAsync(p => p.Name == "journal")).ShouldBe(1);
    }

    [Fact]
    public async Task FindId_NoReservedProjectYet_ReturnsEmpty()
    {
        using var context = _temp.CreateContext();

        (await new JournalProjectStore(context).FindIdAsync()).ShouldBe(null);
    }

    [Fact]
    public async Task ReplaceDerived_TwoJournalWrites_OnAStoreWithoutJournal_BirthTheProjectOnce()
    {
        using var context = _temp.CreateContext();
        var home = Project.Create("Writer Home", "/tmp/writer-home");
        context.Projects.Add(home);
        var source = ContextEntry.Create(home.Id, EntryType.Journal, "Desk note", "seed");
        context.ContextEntries.Add(source);
        await context.SaveChangesAsync();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var tx = await context.Database.BeginTransactionAsync();
            var entry = await context.ContextEntries.SingleAsync(e => e.Id == source.Id);
            var body = "[[Shopping list]] [[feature:404]]";
            entry.UpdateContent(body);

            await new EntryLinkWriter(
                context,
                new EntryLinkResolver(context, new JournalProjectStore(context)))
                .ReplaceDerivedAsync(source.Id, WikiLinkParser.Parse(body), tx);

            await tx.CommitAsync();
        }

        (await context.Projects.CountAsync(p => p.Name == JournalProjectStore.ProjectName)).ShouldBe(1);
        var born = await context.Projects.SingleAsync(p => p.Name == JournalProjectStore.ProjectName);
        born.DiskPath.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ReplaceDerived_TwoWritesToBareTitles_ReuseTheOneBornProject()
    {
        using var context = _temp.CreateContext();
        var home = Project.Create("Second Home", "/tmp/second-home");
        context.Projects.Add(home);
        var source = ContextEntry.Create(home.Id, EntryType.Journal, "Desk note", "seed");
        context.ContextEntries.Add(source);
        await context.SaveChangesAsync();

        var writer = new EntryLinkWriter(
            context,
            new EntryLinkResolver(context, new JournalProjectStore(context)));

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var tx = await context.Database.BeginTransactionAsync();
            var entry = await context.ContextEntries.SingleAsync(e => e.Id == source.Id);
            var body = "[[Shopping list]]";
            entry.UpdateContent(body);
            await writer.ReplaceDerivedAsync(source.Id, WikiLinkParser.Parse(body), tx);
            await tx.CommitAsync();
        }

        var born = await context.Projects
            .Where(p => p.Name == JournalProjectStore.ProjectName)
            .ToListAsync();

        born.ShouldHaveSingleItem();
        (await context.ContextEntries.CountAsync(e => e.ProjectId == born[0].Id)).ShouldBe(0);
    }
}
