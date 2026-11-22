using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

// T1 / R6: the object-scope comparison must not read the object twice. Every door is executed twice over
// equivalent artifacts — once with no caller (the pre-246 shape the human doors still use) and once with
// the conversation's project id — and the SQL command counts must be equal. The baseline is asserted
// non-zero in the same test, so an equality measured over nothing cannot pass.
public sealed class PlanningArtifactCallerScopeQueryCountTests : IAsyncLifetime, IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly CommandCounter _counter = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;

    public PlanningArtifactCallerScopeQueryCountTests()
    {
        _context = _db.CreateContext(_counter);
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));
        _materializer.ClearAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));

        var project = Project.Create($"scope-queries-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;
    }

    public async Task InitializeAsync() =>
        await TestPlanningDispatcher.EnableSyncModeAsync(_context);

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Theory]
    [InlineData("planning_read_artifact")]
    [InlineData("planning_read_artifact_diff")]
    [InlineData("planning_set_status")]
    [InlineData("planning_validate")]
    [InlineData("planning_append_log")]
    [InlineData("planning_edit_log")]
    [InlineData("planning_patch_artifact")]
    [InlineData("planning_delete_artifact")]
    public async Task CallerScope_AddsNoQueryToAnyArtifactDoor(string door)
    {
        var unscoped = await MeasureAsync(door, scoped: false);
        var scoped = await MeasureAsync(door, scoped: true);

        unscoped.ShouldBeGreaterThan(0,
            $"{door}: a baseline of zero means the counter never fired, so this equality proves nothing");
        scoped.ShouldBe(unscoped,
            $"{door}: the owning project must come off the load that already runs");
    }

    private async Task<int> MeasureAsync(string door, bool scoped)
    {
        var target = await CreateFixtureAsync(door);

        _counter.Reset();
        await RunDoorAsync(door, target, scoped);

        return _counter.Count;
    }

    private async Task RunDoorAsync(string door, PlanningArtifact artifact, bool scoped)
    {
        var caller = scoped ? _projectId : (Guid?)null;
        var artifacts = CreateArtifactService();

        switch (door)
        {
            case "planning_read_artifact":
                await artifacts.ReadAsync(artifact.Id, CancellationToken.None, caller);
                break;
            case "planning_read_artifact_diff":
                await new PlanningArtifactDiffService(_context)
                    .DiffAsync(artifact.Id, 1, 2, CancellationToken.None, caller);
                break;
            case "planning_set_status":
                await artifacts.SetStatusAsync(
                    artifact.Id, PlanningArtifactStatus.InProgress, PlanningLogSource.Agent,
                    "counting", CancellationToken.None, callerProjectId: caller);
                break;
            case "planning_validate":
                await artifacts.ValidateAsync(
                    artifact.Id, "counted", PlanningLogSource.Agent, ct: CancellationToken.None,
                    callerProjectId: caller);
                break;
            case "planning_append_log":
                await artifacts.AppendLogAsync(
                    artifact.Id, "counting", CancellationToken.None, PlanningLogSource.Agent, caller);
                break;
            case "planning_edit_log":
                await artifacts.EditNewestLogAsync(
                    artifact.Id, "corrected line", PlanningLogSource.Agent, CancellationToken.None, caller);
                break;
            case "planning_patch_artifact":
                await artifacts.PatchAsync(
                    artifact.Id,
                    new PlanningArtifactPatch("goal", "<goal>\npatched\n</goal>", null, null, null, null),
                    CancellationToken.None,
                    PlanningLogSource.Agent,
                    caller);
                break;
            case "planning_delete_artifact":
                await artifacts.DeleteAsync(
                    artifact.Id, byHuman: false, "counting", PlanningLogSource.Agent,
                    CancellationToken.None, caller);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(door), door, "unmeasured door");
        }
    }

    // A fresh feature per measurement: the mutating doors consume the object they address, so the second
    // run must not inherit what the first one did to it.
    private async Task<PlanningArtifact> CreateFixtureAsync(string door)
    {
        var slug = $"{door.Replace('_', '-')}-{Guid.NewGuid():N}";
        slug.Length.ShouldBeLessThanOrEqualTo(64, "the fixture slug has to stay addressable");
        var feature = await CreateFeatureService().CreateAsync(_projectId, slug, "Query count fixture", "body");
        var artifacts = CreateArtifactService();
        var kind = door is "planning_set_status" or "planning_delete_artifact"
            ? PlanningArtifactKind.Task
            : PlanningArtifactKind.Spec;

        var artifact = await artifacts.UpsertAsync(
            feature.Id, kind, "<goal>\nv1\n</goal>", null, null, null, CancellationToken.None);

        if (door is "planning_read_artifact_diff")
        {
            artifact = await artifacts.UpsertAsync(
                feature.Id, kind, "<goal>\nv2\n</goal>\n<log>\n(none)\n</log>", null, null, null,
                CancellationToken.None);
        }

        if (door is "planning_edit_log")
        {
            await artifacts.AppendLogAsync(
                artifact.Id, "line to correct", CancellationToken.None, PlanningLogSource.Agent);
        }

        _counter.Reset();
        return artifact;
    }

    private PlanningFeatureService CreateFeatureService() =>
        new(
            _context,
            _materializer,
            new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
            new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)),
            TestPlanningDispatcher.Create(_context, _materializer),
            NullLogger<PlanningFeatureService>.Instance);

    private PlanningArtifactService CreateArtifactService() =>
        new(
            _context,
            TestPlanningDispatcher.Create(_context, _materializer),
            new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)));

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _count;

        public int Count => _count;

        public void Reset() => _count = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            _count++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            _count++;
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            _count++;
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _count++;
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            _count++;
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            _count++;
            return ValueTask.FromResult(result);
        }
    }
}
