using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class EntryLifecycleMigrationTests : IDisposable
{
    private const string AddEntryLifecycleMigration = "20261112101240_AddEntryLifecycle";

    private readonly TempDatabase _db = new();

    [Fact]
    public void Migrate_FreshDatabase_RecordsLifecycleMigrationAndStartsActive()
    {
        using var ctx = _db.CreateContext();

        ctx.Database.GetAppliedMigrations().ShouldContain(AddEntryLifecycleMigration);

        var project = Project.Create("lifecycle", "/tmp/lifecycle");
        ctx.Projects.Add(project);
        var entry = ContextEntry.Create(project.Id, EntryType.Decision, "keep sqlite", "metadata stays here");
        ctx.ContextEntries.Add(entry);
        ctx.SaveChanges();

        entry.State.ShouldBe(ContextEntry.ActiveState);
        entry.Enforces.ShouldBeFalse();
        entry.StateAt.ShouldBeNull();
    }

    [Fact]
    public async Task Migrate_RowsWrittenOutsideEf_PickUpDatabaseDefaults()
    {
        Guid projectId;
        using (var seed = _db.CreateContext())
        {
            var project = Project.Create("legacy", "/tmp/legacy");
            seed.Projects.Add(project);
            await seed.SaveChangesAsync();
            projectId = project.Id;

            await seed.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO ContextEntries (Id, ProjectId, Type, Title, Content, CreatedAt, UpdatedAt, CurrentVersion)
                VALUES ({Guid.NewGuid()}, {projectId}, 0, 'pre-lifecycle row', 'old body', '2026-11-01 10:00:00', '2026-11-01 10:00:00', 0)
                """);
        }

        using var verify = _db.CreateContext();
        var row = await verify.ContextEntries.SingleAsync(e => e.ProjectId == projectId);
        row.State.ShouldBe(ContextEntry.ActiveState);
        row.Enforces.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAndReload_RoundTripsLiftedProvenance()
    {
        Guid liftedId;
        Guid successorId;
        using (var seed = _db.CreateContext())
        {
            var project = Project.Create("lifted", "/tmp/lifted");
            seed.Projects.Add(project);
            var lifted = ContextEntry.Create(project.Id, EntryType.Decision, "old take", "first body");
            var successor = ContextEntry.Create(project.Id, EntryType.Decision, "new take", "second body");
            lifted.Lift("replaced by the sqlite decision", "Van (claude)", "reply 7", successor.Id);
            seed.ContextEntries.AddRange(lifted, successor);
            await seed.SaveChangesAsync();
            liftedId = lifted.Id;
            successorId = successor.Id;
        }

        using var verify = _db.CreateContext();
        var loaded = await verify.ContextEntries.SingleAsync(e => e.Id == liftedId);
        loaded.State.ShouldBe(ContextEntry.SupersededState);
        loaded.StateAt.ShouldNotBeNull();
        loaded.StateBy.ShouldBe("Van (claude)");
        loaded.StateReason.ShouldBe("replaced by the sqlite decision");
        loaded.StateProof.ShouldBe("reply 7");
        loaded.SupersededById.ShouldBe(successorId);

        var successorRow = await verify.ContextEntries.SingleAsync(e => e.Id == successorId);
        successorRow.State.ShouldBe(ContextEntry.ActiveState);
    }

    public void Dispose() => _db.Dispose();
}
