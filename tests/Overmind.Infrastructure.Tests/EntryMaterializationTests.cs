using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using System.Security.Cryptography;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class EntryMaterializationTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly string _sandboxDir;
    private readonly string _projectDir;
    private readonly string _dataDir;

    public EntryMaterializationTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-entry-mat-{Guid.NewGuid():N}");
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
    public async Task Sync_WritesEntryFilesWithPinnedFrontmatterAndVerbatimBody()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        Guid entryId;
        using (var seed = _db.CreateContext())
        {
            var entry = ContextEntry.Create(
                projectId, EntryType.Decision, "Use SQLite",
                "SQLite is the only metadata store\n\nRevisit if the project ever outgrows it.");
            var audit = Tag.Create("audit");
            var storage = Tag.Create("storage");
            entry.AddTag(storage);
            entry.AddTag(audit);
            seed.Tags.AddRange(audit, storage);
            seed.ContextEntries.Add(entry);
            await seed.SaveChangesAsync();
            entryId = entry.Id;
        }

        using var ctx = _db.CreateContext();
        var report = await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        var stored = await ctx.ContextEntries
            .Include(e => e.EntryTags)
            .ThenInclude(t => t.Tag)
            .SingleAsync(e => e.Id == entryId);
        var relative = $"ENTRIES/decision/use-sqlite-{entryId.ToString("N")[..8]}.md";
        var doc = await File.ReadAllTextAsync(Path.Combine(_projectDir, relative));
        doc.ShouldBe(
            "---\n" +
            $"id: {entryId}\n" +
            "type: decision\n" +
            "title: Use SQLite\n" +
            "tags: audit, storage\n" +
            $"createdAt: {stored.CreatedAt:yyyy-MM-dd'T'HH:mm:ss'Z'}\n" +
            $"updatedAt: {stored.UpdatedAt:yyyy-MM-dd'T'HH:mm:ss'Z'}\n" +
            "---\n\n" +
            "SQLite is the only metadata store\n\nRevisit if the project ever outgrows it.");

        report.Files.ShouldContain(f => f.RelativePath == relative);
        ctx.PlanningMaterializedFiles
            .Count(m => m.ProjectId == projectId && m.RelativePath == relative)
            .ShouldBe(1);
        (await CreateMaterializer(ctx).DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_ResyncWithUnchangedEntries_IsByteIdempotent()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        using (var seed = _db.CreateContext())
        {
            seed.ContextEntries.Add(
                ContextEntry.Create(projectId, EntryType.Gate, "Tests Green", "All suites pass before merge."));
            await seed.SaveChangesAsync();
        }

        using (var first = _db.CreateContext())
        {
            await CreateMaterializer(first).SyncAsync(projectId, ct: CancellationToken.None);
        }

        var before = await SnapshotTreeAsync(_projectDir);

        using (var resync = _db.CreateContext())
        {
            var report = await CreateMaterializer(resync).SyncAsync(projectId, ct: CancellationToken.None);

            report.Atomic.ShouldBeTrue();
            report.Refused.ShouldBeEmpty();
            report.Skipped.ShouldContain(s =>
                s.RelativePath.StartsWith("ENTRIES/gate/", StringComparison.Ordinal) &&
                s.Reason.Contains("already matches", StringComparison.Ordinal));
        }

        (await SnapshotTreeAsync(_projectDir)).ShouldBe(before);
    }

    [Fact]
    public async Task Sync_DeletedEntry_RemovesItsFileOnTheNextPass()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        using (var seed = _db.CreateContext())
        {
            seed.ContextEntries.Add(
                ContextEntry.Create(projectId, EntryType.Note, "Scratch Pad", "Throwaway thought."));
            await seed.SaveChangesAsync();
        }

        using (var sync = _db.CreateContext())
        {
            await CreateMaterializer(sync).SyncAsync(projectId, ct: CancellationToken.None);
        }

        var entriesDir = Path.Combine(_projectDir, "ENTRIES", "note");
        Directory.Exists(entriesDir).ShouldBeTrue();
        var file = Directory.EnumerateFiles(entriesDir).ShouldHaveSingleItem();

        using (var delete = _db.CreateContext())
        {
            delete.ContextEntries.Remove(await delete.ContextEntries.SingleAsync(e => e.ProjectId == projectId));
            await delete.SaveChangesAsync();
        }

        using (var resync = _db.CreateContext())
        {
            var report = await CreateMaterializer(resync).SyncAsync(projectId, ct: CancellationToken.None);

            report.Warnings.ShouldBeEmpty();
            report.Refused.ShouldBeEmpty();
        }

        File.Exists(file).ShouldBeFalse();
        Directory.Exists(entriesDir).ShouldBeFalse();

        using (var verify = _db.CreateContext())
        {
            var remaining = await verify.PlanningMaterializedFiles
                .Where(m => m.ProjectId == projectId)
                .Select(m => m.RelativePath)
                .ToListAsync();
            remaining.Where(p => p.StartsWith("ENTRIES/", StringComparison.Ordinal)).ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Sync_TombstoneEntry_MaterializesLikeAnyEntry()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        Guid originalId;
        using (var seed = _db.CreateContext())
        {
            var original = ContextEntry.Create(
                projectId, EntryType.Decision, "Use Postgres", "Switched the metadata store.");
            seed.ContextEntries.Add(original);
            await seed.SaveChangesAsync();
            originalId = original.Id;
        }

        using (var sync = _db.CreateContext())
        {
            await CreateMaterializer(sync).SyncAsync(projectId, ct: CancellationToken.None);
        }

        Guid tombstoneId;
        using (var delete = _db.CreateContext())
        {
            var original = await delete.ContextEntries.SingleAsync(e => e.Id == originalId);
            var tombstone = ContextEntry.CreateTombstone(original, PlanningLogSource.Agent, "van", "redundant");
            delete.ContextEntries.Add(tombstone);
            delete.ContextEntries.Remove(original);
            await delete.SaveChangesAsync();
            tombstoneId = tombstone.Id;
        }

        using (var resync = _db.CreateContext())
        {
            var report = await CreateMaterializer(resync).SyncAsync(projectId, ct: CancellationToken.None);

            report.Warnings.ShouldBeEmpty();
            report.Refused.ShouldBeEmpty();
        }

        File.Exists(Path.Combine(_projectDir, $"ENTRIES/decision/use-postgres-{originalId.ToString("N")[..8]}.md"))
            .ShouldBeFalse();

        var tombstoneDoc = await File.ReadAllTextAsync(
            Path.Combine(_projectDir, $"ENTRIES/tombstone/use-postgres-deleted-{tombstoneId.ToString("N")[..8]}.md"));
        tombstoneDoc.ShouldContain("type: tombstone\n");
        tombstoneDoc.ShouldContain("title: Use Postgres (deleted)\n");
        tombstoneDoc.ShouldContain("Deleted ");
        tombstoneDoc.ShouldContain(" UTC via agent door by van.\n");
        tombstoneDoc.ShouldContain("Original type: decision\n");
        tombstoneDoc.ShouldContain("Final content:\n");
        tombstoneDoc.ShouldContain("```\nSwitched the metadata store.\n```");

        using (var verify = _db.CreateContext())
        {
            var remaining = await verify.PlanningMaterializedFiles
                .Where(m => m.ProjectId == projectId)
                .Select(m => m.RelativePath)
                .ToListAsync();
            remaining
                .Where(p => p.StartsWith("ENTRIES/", StringComparison.Ordinal))
                .ShouldHaveSingleItem()
                .ShouldBe($"ENTRIES/tombstone/use-postgres-deleted-{tombstoneId.ToString("N")[..8]}.md");
        }
    }

    [Fact]
    public async Task Sync_StrategyOff_SkipsEntriesEntirely()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.Off);
        using (var seed = _db.CreateContext())
        {
            seed.ContextEntries.Add(
                ContextEntry.Create(projectId, EntryType.Guard, "No Force Push", "Nobody rewrites main."));
            await seed.SaveChangesAsync();
        }

        using var ctx = _db.CreateContext();
        var report = await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        report.Files.ShouldBeEmpty();
        Directory.Exists(Path.Combine(_projectDir, "ENTRIES")).ShouldBeFalse();
        ctx.PlanningMaterializedFiles.Count(m => m.ProjectId == projectId).ShouldBe(0);
    }

    private async Task<Guid> SeedProjectAsync(PlanningMaterialization strategy)
    {
        using var ctx = _db.CreateContext();
        var project = Project.Create("demo", _projectDir);
        project.SetPlanningWorkflow(strategy, HumanValidationOverride.Unset);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();
        return project.Id;
    }

    private PlanningMaterializer CreateMaterializer(OvermindDbContext ctx) =>
        new(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)), _dataDir);

    private static async Task<Dictionary<string, string>> SnapshotTreeAsync(string root)
    {
        var snapshot = new Dictionary<string, string>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var bytes = await File.ReadAllBytesAsync(file);
            snapshot[file[root.Length..].Replace('\\', '/')] = Convert.ToHexString(SHA256.HashData(bytes));
        }

        return snapshot;
    }
}
