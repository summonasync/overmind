using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class RelocationCommitFaultTests : IDisposable
{
    private const string CommitFaultMarker = "overmind-test-commit-phase-fault";
    private const string PinMarker = "code pin moved with project root";

    private readonly TempDatabase _temp = new();
    private readonly Guid _projectId;
    private readonly string _projectName;
    private readonly string _oldRoot;
    private readonly string _newRoot;
    private readonly string _slug;
    private readonly string _oldPin;

    public RelocationCommitFaultTests()
    {
        var stamp = Guid.NewGuid().ToString("N");
        _projectName = $"commit-fault-{stamp}";
        _oldRoot = $"/var/overmind-commit-fault/{stamp}/old";
        _newRoot = $"/var/overmind-commit-fault/{stamp}/moved";
        _oldPin = $"{_oldRoot}/.lanes/030";

        using var context = _temp.CreateContext();
        var project = Project.Create(_projectName, _oldRoot);
        context.Projects.Add(project);
        _projectId = project.Id;

        var feature = PlanningFeature.Create(_projectId, "030-pinned", "030-pinned", "<goal>030</goal>\n");
        feature.SetCodePin(null, _oldPin, PlanningLogSource.Agent);
        context.PlanningFeatures.Add(feature);
        _slug = feature.Slug;

        context.SaveChanges();
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Relocate_FaultsAtCommit_LeavesPathPinsProvenanceAndReminderUnwritten()
    {
        var fault = new CommitFaultInterceptor(CommitFaultMarker);

        using (var context = _temp.CreateContext(fault))
        {
            var evidence = await CaptureFaultAsync(context);
            evidence.ShouldContain(CommitFaultMarker);
        }

        fault.CommitAttempts.ShouldBeGreaterThan(0);

        using (var after = _temp.CreateContext())
        {
            (await after.Projects.AsNoTracking().SingleAsync(p => p.Id == _projectId))
                .DiskPath.ShouldBe(_oldRoot);

            var feature = await after.PlanningFeatures.AsNoTracking().SingleAsync(f => f.Slug == _slug);
            feature.PinnedWorktreePath.ShouldBe(_oldPin);
            CountMarkers(feature.Body).ShouldBe(0);

            (await after.AgentNotifications.AnyAsync(n => n.ProjectId == _projectId)).ShouldBeFalse();
        }

        using (var retry = _temp.CreateContext())
        {
            await StageRelocationAsync(retry);
        }

        using (var healed = _temp.CreateContext())
        {
            (await healed.Projects.AsNoTracking().SingleAsync(p => p.Id == _projectId))
                .DiskPath.ShouldBe(_newRoot);

            var feature = await healed.PlanningFeatures.AsNoTracking().SingleAsync(f => f.Slug == _slug);
            feature.PinnedWorktreePath.ShouldBe($"{_newRoot}/.lanes/030");
            CountMarkers(feature.Body).ShouldBe(1);
            (await healed.AgentNotifications.CountAsync(n => n.ProjectId == _projectId)).ShouldBe(1);
        }
    }

    [Fact]
    public async Task Relocate_WithoutTheCommitFault_LandsEveryHalfOfTheWriteSet()
    {
        using var context = _temp.CreateContext();
        await StageRelocationAsync(context);

        using var after = _temp.CreateContext();
        (await after.Projects.AsNoTracking().SingleAsync(p => p.Id == _projectId))
            .DiskPath.ShouldBe(_newRoot);

        var feature = await after.PlanningFeatures.AsNoTracking().SingleAsync(f => f.Slug == _slug);
        feature.PinnedWorktreePath.ShouldBe($"{_newRoot}/.lanes/030");
        CountMarkers(feature.Body).ShouldBe(1);
        (await after.AgentNotifications.CountAsync(n => n.ProjectId == _projectId)).ShouldBe(1);
    }

    private async Task StageRelocationAsync(OvermindDbContext context)
    {
        var tracked = await context.Projects.SingleAsync(p => p.Id == _projectId);
        context.Entry(tracked).Property(p => p.DiskPath).CurrentValue = _newRoot;

        var relocations = await ProjectPinRelocator.RelocateWorktreePinsAsync(
            context, tracked, _oldRoot, _newRoot, PlanningLogSource.Ui, CancellationToken.None);

        relocations.ShouldHaveSingleItem();

        context.AgentNotifications.Add(AgentNotification.Create(
            NotificationKind.Info,
            RelocationCompanionSteps.DescribeNotification(
                tracked.Name, _oldRoot, _newRoot, relocations.Count),
            _projectId));

        await context.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<string> CaptureFaultAsync(OvermindDbContext context)
    {
        try
        {
            await StageRelocationAsync(context);
            return "the commit accepted the write set";
        }
        catch (Exception ex)
        {
            return ex.ToString();
        }
    }

    private static int CountMarkers(string body) =>
        PlanningLog.Parse(body).Count(line => line.Text.Contains(PinMarker, StringComparison.Ordinal));
}
