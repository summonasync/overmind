using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class EntryAcksMigrationTests : IDisposable
{
    private const string AddPlanningDirectivesMigration = "20261017183000_AddPlanningDirectives";
    private const string AddEntryAcksMigration = "20261017201904_AddEntryAcks";

    private const string FtsTriggersInSchema =
        """
        SELECT COUNT(*) FROM sqlite_master
        WHERE type = 'trigger' AND name IN (
            'trg_feature_fts_insert', 'trg_feature_fts_update', 'trg_feature_fts_delete',
            'trg_artifact_fts_insert', 'trg_artifact_fts_update', 'trg_artifact_fts_delete')
        """;

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"overmind-entry-acks-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsEntryAcks()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("acked", "/tmp/acked");
            db.Projects.Add(project);
            var entry = ContextEntry.Create(project.Id, EntryType.Note, "marshalling", "read first");
            db.ContextEntries.Add(entry);
            db.EntryAcks.Add(EntryAck.Create(entry.Id, "Dan", "qwen", DateTime.UtcNow));
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            var ack = db.EntryAcks.Single();
            ack.Alias.ShouldBe("Dan");
            ack.Vendor.ShouldBe("qwen");
            ack.At.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
        }
    }

    [Fact]
    public void Migrate_AppliesEntryAcks_RecordsTheMigrationHistoryRow()
    {
        using var db = CreateContext();
        db.Database.Migrate();

        db.Database.GetAppliedMigrations().ShouldContain(AddEntryAcksMigration);
    }

    [Fact]
    public async Task Migrate_DownToDirectives_DropsEntryAcks_AndUpRearmsFtsTriggers()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();
            db.Projects.Add(Project.Create("fts-probe", "/tmp/fts-probe"));
            db.SaveChanges();

            db.Database.Migrate(AddPlanningDirectivesMigration);

            TableNames(db).ShouldNotContain("EntryAcks");
            (await TriggerCountAsync(db)).ShouldBe(6);

            db.Database.Migrate();
            (await TriggerCountAsync(db)).ShouldBe(6);
        }

        using (var db = CreateContext())
        {
            db.PlanningFeatures.Add(PlanningFeature.Create(
                db.Projects.Single().Id, "001-rearm", "Rearm", "rearmed capybara body"));
            db.SaveChanges();
        }

        using var verify = CreateContext();
        verify.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM feature_fts WHERE feature_fts MATCH 'capybara'")
            .AsEnumerable().Single().ShouldBe(1);
    }

    private static List<string> TableNames(OvermindDbContext db) =>
        [.. db.Database
            .SqlQuery<string>($"SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'EntryAcks'")
            .AsEnumerable()];

    private static Task<long> TriggerCountAsync(OvermindDbContext db) =>
        PlanningFtsProbe.CountAsync(db, FtsTriggersInSchema);

    private OvermindDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OvermindDbContext>()
            .UseSqlite($"Data Source={_path};Pooling=false")
            .Options;

        return new OvermindDbContext(options);
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var file = _path + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
