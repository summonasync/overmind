using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningMaterializerQuarantineTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly string _sandboxDir;
    private readonly string _projectDir;
    private readonly string _dataDir;

    public PlanningMaterializerQuarantineTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-quarantine-{Guid.NewGuid():N}");
        _projectDir = Path.Combine(_sandboxDir, "project");
        _dataDir = Path.Combine(_sandboxDir, "data");
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_sandboxDir))
        {
            Directory.Delete(_sandboxDir, recursive: true);
        }
    }

    [Fact]
    public async Task Sync_HandEditedTrackedFile_QuarantineSkipsFileAndMirrorsRest()
    {
        var projectId = await SeedProjectAsync();
        var edited = await SeedAndSyncEntriesAsync(projectId, "edited note", "clean note");
        var editedPath = Path.Combine(_projectDir, edited);
        await File.AppendAllTextAsync(editedPath, "\nhand note\n");

        using var ctx = _db.CreateContext();
        var report = await CreateMaterializer(ctx)
            .SyncAsync(projectId, quarantine: true, ct: CancellationToken.None);

        var quarantined = report.Quarantined.ShouldHaveSingleItem();
        quarantined.RelativePath.ShouldBe(edited);
        quarantined.Reason.ShouldBe("tracked file has local edits that are not on the board");
        report.Refused.ShouldBeEmpty();
        (await File.ReadAllTextAsync(editedPath)).ShouldEndWith("\nhand note\n");
        report.Files.ShouldNotContain(f => f.RelativePath == edited);
        report.Skipped.ShouldContain(s =>
            s.RelativePath != edited && s.Reason == "content already matches the board");
        (await ctx.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId))
            .ShouldBe(2);
    }

    [Fact]
    public async Task Sync_HandEditedTrackedFile_DefaultPassStillAbortsBeforeAnyWrite()
    {
        var projectId = await SeedProjectAsync();
        var edited = await SeedAndSyncEntriesAsync(projectId, "kept note", "other note");
        await File.AppendAllTextAsync(Path.Combine(_projectDir, edited), "\nhand note\n");

        using var seedCtx = _db.CreateContext();
        var firstSuccess = (await seedCtx.Projects.SingleAsync(p => p.Id == projectId)).LastMaterializedAt;

        using var ctx = _db.CreateContext();
        var ex = await Should.ThrowAsync<MaterializationException>(
            () => CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.Refused);
        ex.Message.ShouldContain("nothing was written");
        (await ctx.Projects.SingleAsync(p => p.Id == projectId)).LastMaterializedAt.ShouldBe(firstSuccess);
    }

    [Fact]
    public async Task Sync_QuarantinedPassCountsAsSuccessAndRunCarriesQuarantined()
    {
        var projectId = await SeedProjectAsync();
        var edited = await SeedAndSyncEntriesAsync(projectId, "held note", "fresh note");
        await File.AppendAllTextAsync(Path.Combine(_projectDir, edited), "\nhand note\n");

        using (var ctx = _db.CreateContext())
        {
            await CreateMaterializer(ctx).SyncAsync(projectId, quarantine: true, ct: CancellationToken.None);
        }

        using var assertCtx = _db.CreateContext();
        var project = await assertCtx.Projects.SingleAsync(p => p.Id == projectId);
        project.LastMaterializationError.ShouldBeNull();
        project.LastMaterializedAt.ShouldNotBeNull();

        var status = await new PlanningMaterializationStatusReader(
            assertCtx, new PlanningPolicy(assertCtx, new AppSettingsService(assertCtx)),
            CreateMaterializer(assertCtx), _dataDir).ReadAsync(projectId, CancellationToken.None);
        status.LastAttemptReason.ShouldBeNull();
        status.LastRun!.Quarantined.ShouldHaveSingleItem().RelativePath.ShouldBe(edited);
        status.StalePaths.ShouldContain(edited);
    }

    [Fact]
    public async Task Sync_StaleDriftedFile_QuarantineKeepsFileAndRow()
    {
        var projectId = await SeedProjectAsync();
        var relative = await SeedAndSyncEntriesAsync(projectId, "doomed note", "keeper note");
        var absolute = Path.Combine(_projectDir, relative);
        await File.AppendAllTextAsync(absolute, "\nhand note\n");

        using (var removeCtx = _db.CreateContext())
        {
            var entry = await removeCtx.ContextEntries.SingleAsync(e => e.Title == "doomed note");
            removeCtx.ContextEntries.Remove(entry);
            await removeCtx.SaveChangesAsync();
        }

        using var ctx = _db.CreateContext();
        var report = await CreateMaterializer(ctx)
            .SyncAsync(projectId, quarantine: true, ct: CancellationToken.None);

        report.Quarantined.ShouldHaveSingleItem().RelativePath.ShouldBe(relative);
        (await File.ReadAllTextAsync(absolute)).ShouldEndWith("\nhand note\n");
        (await ctx.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId))
            .ShouldBe(2);
    }

    [Fact]
    public async Task Sync_StaleDriftedFile_DefaultPassRefusesIt()
    {
        var projectId = await SeedProjectAsync();
        var relative = await SeedAndSyncEntriesAsync(projectId, "refused note", "staying note");
        await File.AppendAllTextAsync(Path.Combine(_projectDir, relative), "\nhand note\n");

        using (var removeCtx = _db.CreateContext())
        {
            var entry = await removeCtx.ContextEntries.SingleAsync(e => e.Title == "refused note");
            removeCtx.ContextEntries.Remove(entry);
            await removeCtx.SaveChangesAsync();
        }

        using var ctx = _db.CreateContext();
        var ex = await Should.ThrowAsync<MaterializationException>(
            () => CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.Refused);
        ex.Message.ShouldContain(relative);
    }

    private async Task<Guid> SeedProjectAsync()
    {
        using var ctx = _db.CreateContext();
        var project = Project.Create("demo", _projectDir);
        project.SetPlanningWorkflow(PlanningMaterialization.On, HumanValidationOverride.Unset);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();
        return project.Id;
    }

    private async Task<string> SeedAndSyncEntriesAsync(Guid projectId, string firstTitle, string secondTitle)
    {
        using (var seed = _db.CreateContext())
        {
            seed.ContextEntries.AddRange(
                ContextEntry.Create(projectId, EntryType.Note, firstTitle, $"{firstTitle} body."),
                ContextEntry.Create(projectId, EntryType.Note, secondTitle, $"{secondTitle} body."));
            await seed.SaveChangesAsync();
        }

        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        var firstId = await EntryIdOfAsync(projectId, firstTitle);
        return $"ENTRIES/note/{BacklogNaming.MirrorSlug(firstTitle, "entry")}-{firstId.ToString("N")[..8]}.md";
    }

    private async Task<Guid> EntryIdOfAsync(Guid projectId, string title)
    {
        using var ctx = _db.CreateContext();
        return (await ctx.ContextEntries.SingleAsync(e => e.Title == title)).Id;
    }

    private PlanningMaterializer CreateMaterializer(OvermindDbContext ctx) =>
        new(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)), _dataDir);
}
