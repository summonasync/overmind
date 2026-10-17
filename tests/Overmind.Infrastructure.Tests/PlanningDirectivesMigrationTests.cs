using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningDirectivesMigrationTests : IDisposable
{
    private const string AddLanesMigration = "20261017161452_AddLanes";

    private const string FtsTriggersInSchema =
        """
        SELECT COUNT(*) FROM sqlite_master
        WHERE type = 'trigger' AND name IN (
            'trg_feature_fts_insert', 'trg_feature_fts_update', 'trg_feature_fts_delete',
            'trg_artifact_fts_insert', 'trg_artifact_fts_update', 'trg_artifact_fts_delete')
        """;

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"overmind-directives-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsDirectives()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("directed", "/tmp/directed");
            db.Projects.Add(project);
            var feature = PlanningFeature.Create(project.Id, "001-directed", "Directed", "body");
            db.PlanningFeatures.Add(feature);
            db.PlanningDirectives.Add(PlanningDirective.Create(
                feature.Id, "HOLD before push", "Marshal", "wave-marshal", Guid.NewGuid()));
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            var directive = db.PlanningDirectives.Single();
            directive.Text.ShouldBe("HOLD before push");
            directive.AuthorAlias.ShouldBe("Marshal");
            directive.AuthorVendor.ShouldBe("wave-marshal");
            directive.Acked.ShouldBeFalse();
        }
    }

    [Fact]
    public void Migrate_FeatureDeleted_DirectivesCascadeAway()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("cascade", "/tmp/cascade");
            db.Projects.Add(project);
            var feature = PlanningFeature.Create(project.Id, "002-cascade", "Cascade", "body");
            db.PlanningFeatures.Add(feature);
            db.PlanningDirectives.Add(PlanningDirective.Create(
                feature.Id, "temporary", null, null, null));
            db.SaveChanges();
            db.PlanningFeatures.Remove(feature);
            db.SaveChanges();
        }

        using var verify = CreateContext();
        verify.PlanningDirectives.ShouldBeEmpty();
    }

    [Fact]
    public async Task Migrate_DownToLanes_DropsDirectivesTable_AndUpRearmsFtsTriggers()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();
            var project = db.Projects.Add(Project.Create("fts-probe", "/tmp/fts-probe")).Entity;
            var feature = PlanningFeature.Create(project.Id, "003-probe", "Fts probe", "rearmed quokka body");
            db.PlanningFeatures.Add(feature);
            db.SaveChanges();

            db.Database.Migrate(AddLanesMigration);

            TableNames(db).ShouldNotContain("PlanningDirectives");
            (await TriggerCountAsync(db)).ShouldBe(6);

            db.Database.Migrate();
            (await TriggerCountAsync(db)).ShouldBe(6);
        }

        using (var db = CreateContext())
        {
            db.PlanningFeatures.Add(PlanningFeature.Create(
                db.Projects.Single().Id, "004-rearm", "Rearm", "rearmed capybara body"));
            db.SaveChanges();
        }

        using var verify = CreateContext();
        verify.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM feature_fts WHERE feature_fts MATCH 'capybara'")
            .AsEnumerable().Single().ShouldBe(1);
    }

    private static List<string> TableNames(OvermindDbContext db) =>
        [.. db.Database
            .SqlQuery<string>($"SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'PlanningDirectives'")
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
