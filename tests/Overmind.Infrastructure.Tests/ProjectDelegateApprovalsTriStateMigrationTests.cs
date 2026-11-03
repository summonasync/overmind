using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ProjectDelegateApprovalsTriStateMigrationTests : IDisposable
{
    private const string AddProjectDelegateApprovalsMigration = "20261102134002_AddProjectDelegateApprovals";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"overmind-delegate-approvals-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_LegacyBooleanRows_TurnsTrueIntoOnAndFalseIntoUnset()
    {
        var enabled = Guid.NewGuid();
        var disabled = Guid.NewGuid();

        using (var db = CreateContext())
        {
            db.Database.Migrate(AddProjectDelegateApprovalsMigration);

            db.Database.ExecuteSql(
                $"""
                 INSERT INTO Projects (Id, Name, DiskPath, Description, CreatedAt, DelegateApprovals)
                 VALUES ({enabled}, 'enabled-legacy', '/tmp/enabled', '', {DateTime.UtcNow}, 1),
                        ({disabled}, 'disabled-legacy', '/tmp/disabled', '', {DateTime.UtcNow}, 0)
                 """);

            db.Database.Migrate();
        }

        using (var db = CreateContext())
        {
            db.Projects.Single(p => p.Id == enabled).DelegateApprovals.ShouldBe(DelegateApprovalsOverride.On);
            db.Projects.Single(p => p.Id == disabled).DelegateApprovals.ShouldBe(DelegateApprovalsOverride.Unset);
        }
    }

    [Fact]
    public void Migrate_FreshDatabase_RoundTripsExplicitOffOverride()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();

            var project = Project.Create("explicit-off", "/tmp/explicit-off");
            project.SetDelegateApprovals(DelegateApprovalsOverride.Off);
            db.Projects.Add(project);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            db.Projects.Single().DelegateApprovals.ShouldBe(DelegateApprovalsOverride.Off);
        }
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
