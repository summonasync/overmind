using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    public async Task EnsureId_AdoptsAnExactSpellingRowThatAlreadyExists()
    {
        using var context = _temp.CreateContext();
        var seeded = Project.Create(JournalProjectStore.ProjectName, "/tmp/journal", "exact seed");
        context.Projects.Add(seeded);
        await context.SaveChangesAsync();

        var store = new JournalProjectStore(context);
        var ensured = await store.EnsureIdAsync();

        ensured.ShouldBe(seeded.Id);
        (await store.FindIdAsync()).ShouldBe(ensured);
        (await context.Projects.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task FindId_NoReservedProjectYet_ReturnsEmpty()
    {
        using var context = _temp.CreateContext();

        (await new JournalProjectStore(context).FindIdAsync()).ShouldBe(null);
    }

    [Fact]
    public async Task ExactJournalContainer_TheProjectMarkerResolvesOntoIt()
    {
        using var context = _temp.CreateContext();
        var container = Project.Create(JournalProjectStore.ProjectName, "/tmp/journal");
        context.Projects.Add(container);
        var home = Project.Create("Collision Home", "/tmp/collision-home");
        context.Projects.Add(home);
        var source = ContextEntry.Create(home.Id, EntryType.Note, "journal probe", "seed");
        context.ContextEntries.Add(source);
        await context.SaveChangesAsync();

        var found = await new JournalProjectStore(context).FindIdAsync();
        var marker = await new EntryLinkResolver(context, new JournalProjectStore(context))
            .ResolveForWriteAsync(source.Id, WikiLinkParser.Parse("[[project:Journal]]"));

        found.ShouldBe(container.Id);
        marker.ShouldHaveSingleItem();
        marker[0].ShouldBe(new ResolvedLink(EntryLinkKind.Project, container.Id));
    }

    [Fact]
    public async Task EnsureId_RaceInsideAnExplicitTransaction_DetachesAndRethrowsForTheCallerToRetry()
    {
        using var connection = new SqliteConnection($"Data Source={DatabasePath()};Pooling=false");
        await connection.OpenAsync();

        var winnerId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<OvermindDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new WinnerLandsFirstInterceptor(connection, winnerId))
            .Options;

        using var loser = new OvermindDbContext(options);
        await using var tx = await loser.Database.BeginTransactionAsync();

        // A row that lands mid-write inside an open transaction is not adoptable: the re-find reads
        // the same snapshot. The guard's job here is to leave nothing behind and let the caller retry.
        var surfaced = await Should.ThrowAsync<DbUpdateException>(
            () => new JournalProjectStore(loser).EnsureIdAsync());
        surfaced.InnerException!.Message.ShouldContain("UNIQUE constraint failed: Projects.Name");
        loser.ChangeTracker.Entries<Project>().ShouldBeEmpty();

        await tx.RollbackAsync();

        var recovered = await new JournalProjectStore(loser).EnsureIdAsync();
        recovered.ShouldNotBe(Guid.Empty);
        (await loser.Projects.CountAsync(p => p.Name == JournalProjectStore.ProjectName)).ShouldBe(1);
    }

    [Fact]
    public async Task ReplaceDerived_FeatureOnlyBody_LeavesTheReservedProjectUnborn()
    {
        using var context = _temp.CreateContext();
        var home = Project.Create("Feature Only Home", "/tmp/feature-only");
        context.Projects.Add(home);
        var source = ContextEntry.Create(home.Id, EntryType.Journal, "Desk note", "seed");
        context.ContextEntries.Add(source);
        context.PlanningFeatures.Add(PlanningFeature.Create(home.Id, "184-f", "F", "body"));
        await context.SaveChangesAsync();

        await using (var tx = await context.Database.BeginTransactionAsync())
        {
            var entry = await context.ContextEntries.SingleAsync(e => e.Id == source.Id);
            entry.UpdateContent("[[feature:184]]");
            await new EntryLinkWriter(
                context, new EntryLinkResolver(context, new JournalProjectStore(context)))
                .ReplaceDerivedAsync(source.Id, WikiLinkParser.Parse(entry.Content), tx);
            await tx.CommitAsync();
        }

        (await context.EntryLinks.CountAsync(link => link.FromEntryId == source.Id)).ShouldBe(1);
        (await context.Projects.CountAsync(
            p => p.Name == JournalProjectStore.ProjectName)).ShouldBe(0);
    }

    private string DatabasePath()
    {
        using var probe = _temp.CreateContext();
        return probe.Database.GetDbConnection().DataSource;
    }

    private sealed class WinnerLandsFirstInterceptor(
        SqliteConnection connection,
        Guid winnerId) : DbCommandInterceptor
    {
        private bool _fired;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            LandWinner(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            LandWinner(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void LandWinner(DbCommand command)
        {
            if (_fired || !command.CommandText.Contains("INSERT INTO \"Projects\""))
            {
                return;
            }

            _fired = true;
            using var steal = new SqliteCommand(
                """
                INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt)
                VALUES (@id, 'Journal', '/tmp/stolen', 'winner lands mid-write', @createdAt)
                """,
                connection,
                (SqliteTransaction?)command.Transaction);
            steal.Parameters.AddWithValue("@id", winnerId.ToString().ToUpperInvariant());
            steal.Parameters.AddWithValue(
                "@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffff"));
            steal.ExecuteNonQuery();
        }
    }
}
