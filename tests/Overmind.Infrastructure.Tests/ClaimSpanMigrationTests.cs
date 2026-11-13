using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ClaimSpanMigrationTests : IDisposable
{
    private const string PreClaimSpansMigration = "20261103231214_AddTelegramValidatorMappings";
    private const string AddClaimSpansMigration = "20261112122309_AddClaimSpansAndUsageRecords";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"overmind-span-mig-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_ToClaimSpans_BackfillsOpenClaimsAsOpenSpans()
    {
        Guid claimedFeatureId;
        using (var db = CreateContext())
        {
            db.Database.Migrate(PreClaimSpansMigration);

            var projectId = Guid.NewGuid();
            using var command = (SqliteCommand)db.Database.GetDbConnection().CreateCommand();
            command.CommandText =
                "INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) "
                + $"VALUES ('{projectId.ToString().ToUpperInvariant()}', 'span-backfill', '/tmp/span-backfill', '', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}')";
            db.Database.OpenConnection();
            command.ExecuteNonQuery();

            var claimed = PlanningFeature.Create(projectId, "001-held", "Held", "body");
            claimed.Claim("Dan", "qwen", Guid.NewGuid(), DateTime.UtcNow.AddHours(-2));
            var unclaimed = PlanningFeature.Create(projectId, "002-free", "Free", "body");
            db.PlanningFeatures.Add(claimed);
            db.PlanningFeatures.Add(unclaimed);
            db.SaveChanges();
            claimedFeatureId = claimed.Id;
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var spans = db.FeatureClaimSpans.AsNoTracking().ToList();
            spans.Count.ShouldBe(1);
            spans[0].FeatureId.ShouldBe(claimedFeatureId);
            spans[0].ProjectId.ShouldBe(db.Projects.Single().Id);
            spans[0].Alias.ShouldBe("Dan");
            spans[0].Vendor.ShouldBe("qwen");
            spans[0].ReleasedAt.ShouldBeNull();
            spans[0].ClaimedAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
            db.Database.GetAppliedMigrations().ShouldContain(AddClaimSpansMigration);
        }
    }

    [Fact]
    public void Migrate_FreshDatabase_SeedsNoSpans()
    {
        using var db = CreateContext();
        db.Database.Migrate();

        db.FeatureClaimSpans.Count().ShouldBe(0);
    }

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
