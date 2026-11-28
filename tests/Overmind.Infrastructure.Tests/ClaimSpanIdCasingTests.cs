using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

// The AddClaimSpansAndUsageRecords backfill wrote span ids as lowercase text while EF binds Guid
// parameters as UPPERCASE TEXT — every later ledger write on such a row matched 0 rows and blew up
// as DbUpdateConcurrencyException (issue #9). NormalizeClaimSpanIdCasing repairs existing rows.
public sealed class ClaimSpanIdCasingTests : IDisposable
{
    private const string PreClaimSpansMigration = "20261103231214_AddTelegramValidatorMappings";
    private const string AddClaimSpansMigration = "20261112122309_AddClaimSpansAndUsageRecords";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"overmind-span-casing-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_LegacyBackfilledSpans_LandUppercase()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(PreClaimSpansMigration);
            SeedClaimedFeature(db, "001-held");
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate(AddClaimSpansMigration);

            LowercaseSpanCount(db).ShouldBe(0);
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate();

            db.Database.GetAppliedMigrations().ShouldContain(AddClaimSpansMigration);
            LowercaseSpanCount(db).ShouldBe(0);
        }
    }

    [Fact]
    public void Migrate_LowercaseSpanIdFromLegacyBackfill_NormalizesIt()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate(PreClaimSpansMigration);
            SeedClaimedFeature(db, "002-lowered");
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate(AddClaimSpansMigration);
            CorruptSpanIdsToLowercase(db);
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate();

            db.FeatureClaimSpans.Count().ShouldBe(1);
            LowercaseSpanCount(db).ShouldBe(0);
        }
    }

    [Fact]
    public async Task Unclaim_LegacyLowercaseSpan_ClosesIt()
    {
        var holderSessionId = Guid.NewGuid();
        using (var db = CreateContext())
        {
            db.Database.Migrate(PreClaimSpansMigration);
            SeedClaimedFeature(db, "003-unclaim", holderSessionId);
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate(AddClaimSpansMigration);
            CorruptSpanIdsToLowercase(db);
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var projectId = db.Projects.Single().Id;
            var service = CreateService(db);

            var feature = await service.UnclaimAsync(projectId, "003-unclaim", holderSessionId);

            feature.ClaimSessionId.ShouldBeNull();
            db.FeatureClaimSpans.Single().ReleasedAt.ShouldNotBeNull();
            LowercaseSpanCount(db).ShouldBe(0);
        }
    }

    [Fact]
    public async Task Claim_TakeoverOverLegacyLowercaseSpan_OpensNewSpan()
    {
        var holderSessionId = Guid.NewGuid();
        var newcomerSessionId = Guid.NewGuid();
        using (var db = CreateContext())
        {
            db.Database.Migrate(PreClaimSpansMigration);
            SeedClaimedFeature(db, "004-takeover", holderSessionId);
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate(AddClaimSpansMigration);
            CorruptSpanIdsToLowercase(db);
        }

        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var projectId = db.Projects.Single().Id;
            var service = CreateService(db);

            var feature = await service.ClaimAsync(
                projectId, "004-takeover", "billy", "qwen", newcomerSessionId);

            feature.ClaimSessionId.ShouldBe(newcomerSessionId);
            var spans = db.FeatureClaimSpans.OrderBy(s => s.ClaimedAt).ToList();
            spans.Count.ShouldBe(2);
            spans[0].ReleasedAt.ShouldNotBeNull();
            spans[1].ReleasedAt.ShouldBeNull();
            spans[1].SessionId.ShouldBe(newcomerSessionId);
            LowercaseSpanCount(db).ShouldBe(0);
        }
    }

    private static FeatureClaimService CreateService(OvermindDbContext db)
    {
        var materializer = Substitute.For<IPlanningMaterializer>();
        materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));
        return new FeatureClaimService(db, TestPlanningDispatcher.Create(db, materializer));
    }

    private static void SeedClaimedFeature(OvermindDbContext db, string slug, Guid? sessionId = null)
    {
        var projectId = Guid.NewGuid();
        using var command = (SqliteCommand)db.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            "INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt) "
            + $"VALUES ('{projectId.ToString().ToUpperInvariant()}', 'span-casing', '/tmp/span-casing', '', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}')";
        db.Database.OpenConnection();
        command.ExecuteNonQuery();

        var claimed = PlanningFeature.Create(projectId, slug, slug, "body");
        claimed.Claim("dan", "qwen", sessionId ?? Guid.NewGuid(), DateTime.UtcNow.AddHours(-2));
        db.PlanningFeatures.Add(claimed);
        db.SaveChanges();
    }

    private static void CorruptSpanIdsToLowercase(OvermindDbContext db)
    {
        using var command = (SqliteCommand)db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "UPDATE FeatureClaimSpans SET Id = lower(Id)";
        db.Database.OpenConnection();
        command.ExecuteNonQuery();
    }

    private static int LowercaseSpanCount(OvermindDbContext db)
    {
        using var command = (SqliteCommand)db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM FeatureClaimSpans WHERE Id <> upper(Id)";
        db.Database.OpenConnection();
        return Convert.ToInt32(command.ExecuteScalar());
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
