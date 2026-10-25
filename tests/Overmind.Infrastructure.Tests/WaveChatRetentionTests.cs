using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wave;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class WaveChatRetentionTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly Guid _projectId;

    public WaveChatRetentionTests()
    {
        _context = _db.CreateContext();
        var project = Project.Create($"wave-keep-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Sweep_PurgesMessagesPastRetention_KeepsFreshOnes()
    {
        var ancient = WaveMessage.Create(_projectId, "all", "Dan", "worker", "ancient");
        var fresh = WaveMessage.Create(_projectId, "all", "Dan", "worker", "fresh");
        _context.WaveMessages.AddRange(ancient, fresh);
        await _context.SaveChangesAsync();
        await BackdateAsync(ancient.Id, DateTime.UtcNow.AddDays(-8));

        var purged = await new WaveChatRetentionSweeper(_context).SweepAsync(WaveChatRetentionSweeper.DefaultRetentionDays);

        purged.ShouldBe(1);
        _context.ChangeTracker.Clear();
        (await _context.WaveMessages.ToListAsync()).Select(m => m.Id).ShouldBe([fresh.Id]);
    }

    [Fact]
    public async Task Sweep_DoesNotTouchPlanningArtifacts()
    {
        var feature = PlanningFeature.Create(_projectId, "kept-feature", "Kept", "body");
        _context.PlanningFeatures.Add(feature);
        _context.PlanningArtifacts.Add(PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Spec, "spec body"));
        var message = WaveMessage.Create(_projectId, "all", "Dan", "worker", "ancient");
        _context.WaveMessages.Add(message);
        await _context.SaveChangesAsync();
        await BackdateAsync(message.Id, DateTime.UtcNow.AddDays(-30));

        await new WaveChatRetentionSweeper(_context).SweepAsync(7);

        _context.ChangeTracker.Clear();
        (await _context.WaveMessages.CountAsync()).ShouldBe(0);
        (await _context.PlanningFeatures.CountAsync()).ShouldBe(1);
        (await _context.PlanningArtifacts.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Sweep_KeepsWatermarks_ForTheirAliases()
    {
        var message = WaveMessage.Create(_projectId, "all", "Dan", "worker", "ancient");
        _context.WaveMessages.Add(message);
        await _context.SaveChangesAsync();
        await BackdateAsync(message.Id, DateTime.UtcNow.AddDays(-8));
        _context.WaveWatermarks.Add(WaveWatermark.Create(_projectId, "marshal", DateTime.UtcNow));
        await _context.SaveChangesAsync();

        await new WaveChatRetentionSweeper(_context).SweepAsync(7);

        _context.ChangeTracker.Clear();
        (await _context.WaveWatermarks.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public void NormalizeDays_FallsBackToDefault_OnMissingOrInvalid()
    {
        WaveChatRetentionSweeper.NormalizeDays(null).ShouldBe(7);
        WaveChatRetentionSweeper.NormalizeDays(0).ShouldBe(7);
        WaveChatRetentionSweeper.NormalizeDays(-3).ShouldBe(7);
    }

    [Fact]
    public void NormalizeDays_KeepsConfiguredPositiveValues()
    {
        WaveChatRetentionSweeper.NormalizeDays(1).ShouldBe(1);
        WaveChatRetentionSweeper.NormalizeDays(14).ShouldBe(14);
    }

    private async Task BackdateAsync(Guid messageId, DateTime createdAt)
    {
        await _context.Database.ExecuteSqlAsync(
            $"UPDATE WaveMessages SET CreatedAt = {createdAt} WHERE Id = {messageId}");
        _context.ChangeTracker.Clear();
    }
}
