using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class WaveChatMigrationTests : IDisposable
{
    private const string PreviousMigration = "20261022155645_AddPlanningOperationRecords";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"overmind-wave-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsMessagesAndWatermarks()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("waved", "/tmp/waved");
            db.Projects.Add(project);
            db.SaveChanges();

            db.WaveMessages.Add(WaveMessage.Create(project.Id, "backend", "Dan", "hello @marshal"));
            db.WaveWatermarks.Add(WaveWatermark.Create(project.Id, "marshal", DateTime.UtcNow));
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            var message = db.WaveMessages.Single();
            message.Room.ShouldBe("backend");
            message.AuthorAlias.ShouldBe("Dan");
            message.MentionList.ShouldBe(["marshal"]);
            db.WaveWatermarks.Single().Alias.ShouldBe("marshal");
        }
    }

    [Fact]
    public void Migrate_DownToPlanningOperationRecords_DropsWaveTables()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate(PreviousMigration);

            TableNames(db).ShouldNotContain("WaveMessages");
            TableNames(db).ShouldNotContain("WaveWatermarks");
        }
    }

    private static List<string> TableNames(OvermindDbContext db) =>
        [.. db.Database
            .SqlQuery<string>($"SELECT name FROM sqlite_master WHERE type = 'table' AND name LIKE 'Wave%'")
            .AsEnumerable()];

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
