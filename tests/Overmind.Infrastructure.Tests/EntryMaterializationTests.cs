using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Materialization;
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
    public async Task Sync_FeatureRequestEntry_LandsInKebabDirectoryAndStaysPinnedOnResync()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        Guid entryId;
        using (var seed = _db.CreateContext())
        {
            var entry = ContextEntry.Create(
                projectId, EntryType.FeatureRequest, "Vector Cache", "Origin: chat, 2026-10-13.");
            seed.ContextEntries.Add(entry);
            await seed.SaveChangesAsync();
            entryId = entry.Id;
        }

        using (var stored = _db.CreateContext())
        {
            var entry = await stored.ContextEntries.SingleAsync(e => e.Id == entryId);
            await CreateMaterializer(stored).SyncAsync(projectId, ct: CancellationToken.None);

            var relative = $"ENTRIES/feature-request/vector-cache-{entryId.ToString("N")[..8]}.md";
            var doc = await File.ReadAllTextAsync(Path.Combine(_projectDir, relative));
            doc.ShouldBe(
                "---\n" +
                $"id: {entryId}\n" +
                "type: featureRequest\n" +
                "title: Vector Cache\n" +
                $"createdAt: {entry.CreatedAt:yyyy-MM-dd'T'HH:mm:ss'Z'}\n" +
                $"updatedAt: {entry.UpdatedAt:yyyy-MM-dd'T'HH:mm:ss'Z'}\n" +
                "---\n\n" +
                "Origin: chat, 2026-10-13.");
        }

        using (var resync = _db.CreateContext())
        {
            var report = await CreateMaterializer(resync).SyncAsync(projectId, ct: CancellationToken.None);

            report.Refused.ShouldBeEmpty();
            report.Skipped.ShouldContain(s =>
                s.RelativePath.StartsWith("ENTRIES/feature-request/", StringComparison.Ordinal) &&
                s.Reason.Contains("already matches", StringComparison.Ordinal));
        }

        Directory.EnumerateFiles(Path.Combine(_projectDir, "ENTRIES", "feature-request"))
            .ShouldHaveSingleItem();
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

    [Fact]
    public async Task Sync_HandEditedTrackedEntryFile_RefusesAndWritesNothing()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        var relative = await SeedSyncedEntryAsync(projectId, EntryType.Note, "Guard Me");

        var entryPath = Path.Combine(_projectDir, relative.Replace('/', Path.DirectorySeparatorChar));
        await File.AppendAllTextAsync(entryPath, "\nhand note\n");
        var before = await SnapshotTreeAsync(_projectDir);

        using var ctx = _db.CreateContext();
        var ex = await Should.ThrowAsync<MaterializationException>(
            () => CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.Refused);
        ex.OffendingPath.ShouldBe(relative);
        ex.Message.ShouldContain(relative);
        ex.Message.ShouldContain("local edits");

        (await SnapshotTreeAsync(_projectDir)).ShouldBe(before);

        using var verify = _db.CreateContext();
        var project = await verify.Projects.SingleAsync(p => p.Id == projectId);
        project.LastMaterializationError.ShouldNotBeNull();
        project.LastMaterializationError!.ShouldContain("local edits");

        var run = MaterializationRunJson.Deserialize(project.LastMaterializationRun);
        run.ShouldNotBeNull();
        run!.Atomic.ShouldBeFalse();
        run.Written.ShouldBeEmpty();
        var refusal = run.Refused.ShouldHaveSingleItem();
        refusal.RelativePath.ShouldBe(relative);
        refusal.Reason.ShouldContain("local edits");
        run.Skipped.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_HandEditedTrackedEntryFile_ForceOverwritesAndWarns()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        var relative = await SeedSyncedEntryAsync(projectId, EntryType.Note, "Guard Me");

        var entryPath = Path.Combine(_projectDir, relative.Replace('/', Path.DirectorySeparatorChar));
        await File.AppendAllTextAsync(entryPath, "\nhand note\n");

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        var report = await materializer.SyncAsync(projectId, force: true, ct: CancellationToken.None);

        report.Refused.ShouldBeEmpty();
        report.Warnings.ShouldHaveSingleItem().ShouldBe($"Overwrote locally modified file: {entryPath}");
        var content = await File.ReadAllTextAsync(entryPath);
        content.ShouldNotContain("hand note");
        content.ShouldContain("title: Guard Me\n");
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_ForeignFileAtEntryPath_RefusesThenForceAdopts()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        var relative = await SeedEntryAsync(projectId, EntryType.Note, "Adopted Note");

        var foreign = Path.Combine(_projectDir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
        await File.WriteAllTextAsync(foreign, "user data");
        var before = await SnapshotTreeAsync(_projectDir);

        using (var ctx = _db.CreateContext())
        {
            var ex = await Should.ThrowAsync<MaterializationException>(
                () => CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None));

            ex.Kind.ShouldBe(MaterializationFailureKind.Refused);
            ex.OffendingPath.ShouldBe(relative);
            ex.Message.ShouldContain("not tracked by Overmind");
        }

        (await SnapshotTreeAsync(_projectDir)).ShouldBe(before);
        using (var verify = _db.CreateContext())
        {
            (await verify.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(0);
        }

        using var adopt = _db.CreateContext();
        var materializer = CreateMaterializer(adopt);
        var report = await materializer.SyncAsync(projectId, force: true, ct: CancellationToken.None);

        report.Warnings.ShouldHaveSingleItem().ShouldBe($"Overwrote untracked file: {foreign}");
        (await File.ReadAllTextAsync(foreign)).ShouldContain("title: Adopted Note\n");
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_LineBreaksInTitleOrTags_FlattenToFrontmatterSafeValues()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        Guid entryId;
        DateTime createdAt;
        DateTime updatedAt;
        using (var seed = _db.CreateContext())
        {
            var entry = ContextEntry.Create(projectId, EntryType.Note, "Use\r\nSQLite", "Body survives verbatim.");
            var tag = Tag.Create("multi\nline");
            entry.AddTag(tag);
            seed.Tags.Add(tag);
            seed.ContextEntries.Add(entry);
            await seed.SaveChangesAsync();
            entryId = entry.Id;
            createdAt = entry.CreatedAt;
            updatedAt = entry.UpdatedAt;
        }

        var relative = $"ENTRIES/note/use-sqlite-{entryId.ToString("N")[..8]}.md";
        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        var doc = await File.ReadAllTextAsync(Path.Combine(_projectDir, relative.Replace('/', Path.DirectorySeparatorChar)));
        doc.ShouldBe(
            "---\n" +
            $"id: {entryId}\n" +
            "type: note\n" +
            "title: Use SQLite\n" +
            "tags: multi line\n" +
            $"createdAt: {createdAt:yyyy-MM-dd'T'HH:mm:ss'Z'}\n" +
            $"updatedAt: {updatedAt:yyyy-MM-dd'T'HH:mm:ss'Z'}\n" +
            "---\n\n" +
            "Body survives verbatim.");

        using var verify = _db.CreateContext();
        var stored = await verify.ContextEntries.SingleAsync(e => e.Id == entryId);
        stored.Title.ShouldBe("Use\r\nSQLite");

        var resync = await CreateMaterializer(verify).SyncAsync(projectId, ct: CancellationToken.None);
        resync.Refused.ShouldBeEmpty();
        resync.Skipped.ShouldContain(s =>
            s.RelativePath == relative &&
            s.Reason.Contains("already matches", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sync_EntryTitleRename_ReplacesFileInOnePass()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        var entryId = await SeedEntryIdAsync(projectId, EntryType.Note, "Old Title");

        var id8 = entryId.ToString("N")[..8];
        var oldPath = Path.Combine(_projectDir, "ENTRIES", "note", $"old-title-{id8}.md");
        var newPath = Path.Combine(_projectDir, "ENTRIES", "note", $"new-title-{id8}.md");

        using (var ctx = _db.CreateContext())
        {
            await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);
        }

        File.Exists(oldPath).ShouldBeTrue();

        using (var rename = _db.CreateContext())
        {
            var entry = await rename.ContextEntries.SingleAsync(e => e.Id == entryId);
            rename.Entry(entry).Property(e => e.Title).CurrentValue = "New Title";
            await rename.SaveChangesAsync();
        }

        using var resync = _db.CreateContext();
        var report = await CreateMaterializer(resync).SyncAsync(projectId, ct: CancellationToken.None);

        report.Warnings.ShouldBeEmpty();
        report.Refused.ShouldBeEmpty();
        File.Exists(oldPath).ShouldBeFalse();
        File.Exists(newPath).ShouldBeTrue();
        (await File.ReadAllTextAsync(newPath)).ShouldContain("title: New Title\n");
        Directory.EnumerateFiles(Path.Combine(_projectDir, "ENTRIES", "note"))
            .ShouldHaveSingleItem()
            .ShouldBe(newPath);

        var rows = await resync.PlanningMaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .Select(m => m.RelativePath)
            .ToListAsync();
        rows.Where(p => p.StartsWith("ENTRIES/", StringComparison.Ordinal))
            .ShouldHaveSingleItem()
            .ShouldBe($"ENTRIES/note/new-title-{id8}.md");
    }

    [Fact]
    public async Task Sync_CyrillicEntryTitle_WritesBoundedAsciiFilename()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        var entryId = await SeedEntryIdAsync(projectId, EntryType.Decision, "Решение по кэшированию");

        using var ctx = _db.CreateContext();
        var report = await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        var relative = $"ENTRIES/decision/entry-75f09372-{entryId.ToString("N")[..8]}.md";
        report.Files.ShouldContain(f => f.RelativePath == relative);
        File.Exists(Path.Combine(_projectDir, relative.Replace('/', Path.DirectorySeparatorChar))).ShouldBeTrue();
        Directory.EnumerateFiles(Path.Combine(_projectDir, "ENTRIES", "decision")).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Sync_TitleUnchangedEntryWithLegacyNonAsciiName_ConvergesOnTheNextPass()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        var entryId = await SeedEntryIdAsync(projectId, EntryType.Decision, "Решение по кэшированию");

        using (var ctx = _db.CreateContext())
        {
            await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);
        }

        var id8 = entryId.ToString("N")[..8];
        var legacyRelative = $"ENTRIES/decision/решение-по-кэшированию-{id8}.md";
        File.Move(
            Path.Combine(_projectDir, "ENTRIES", "decision", $"entry-75f09372-{id8}.md"),
            Path.Combine(_projectDir, "ENTRIES", "decision", $"решение-по-кэшированию-{id8}.md"));

        using (var track = _db.CreateContext())
        {
            var row = track.PlanningMaterializedFiles.Single(m => m.ProjectId == projectId);
            track.Entry(row).Property(r => r.RelativePath).CurrentValue = legacyRelative;
            await track.SaveChangesAsync();
        }

        using var resync = _db.CreateContext();
        var report = await CreateMaterializer(resync).SyncAsync(projectId, ct: CancellationToken.None);

        report.Warnings.ShouldBeEmpty();
        report.Refused.ShouldBeEmpty();
        File.Exists(Path.Combine(_projectDir, legacyRelative.Replace('/', Path.DirectorySeparatorChar)))
            .ShouldBeFalse();
        var convergedRelative = $"ENTRIES/decision/entry-75f09372-{id8}.md";
        Directory.EnumerateFiles(Path.Combine(_projectDir, "ENTRIES", "decision"))
            .ShouldHaveSingleItem()
            .ShouldBe(Path.Combine(_projectDir, "ENTRIES", "decision", $"entry-75f09372-{id8}.md"));

        var rows = await resync.PlanningMaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .Select(m => m.RelativePath)
            .ToListAsync();
        rows.Where(p => p.StartsWith("ENTRIES/", StringComparison.Ordinal))
            .ShouldHaveSingleItem()
            .ShouldBe(convergedRelative);
    }

    [Fact]
    public async Task Sync_TitleChangeOnLegacyNonAsciiName_ConvergesToBoundedFilename()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        var entryId = await SeedEntryIdAsync(projectId, EntryType.Decision, "Решение по кэшированию");

        using (var ctx = _db.CreateContext())
        {
            await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);
        }

        var id8 = entryId.ToString("N")[..8];
        var legacyRelative = $"ENTRIES/decision/решение-по-кэшированию-{id8}.md";
        File.Move(
            Path.Combine(_projectDir, "ENTRIES", "decision", $"entry-75f09372-{id8}.md"),
            Path.Combine(_projectDir, "ENTRIES", "decision", $"решение-по-кэшированию-{id8}.md"));

        using (var track = _db.CreateContext())
        {
            var row = track.PlanningMaterializedFiles.Single(m => m.ProjectId == projectId);
            track.Entry(row).Property(r => r.RelativePath).CurrentValue = legacyRelative;
            await track.SaveChangesAsync();
        }

        using (var rename = _db.CreateContext())
        {
            var entry = await rename.ContextEntries.SingleAsync(e => e.Id == entryId);
            rename.Entry(entry).Property(e => e.Title).CurrentValue = "Cache Decision";
            await rename.SaveChangesAsync();
        }

        using var resync = _db.CreateContext();
        var report = await CreateMaterializer(resync).SyncAsync(projectId, ct: CancellationToken.None);

        report.Warnings.ShouldBeEmpty();
        report.Refused.ShouldBeEmpty();
        File.Exists(Path.Combine(_projectDir, "ENTRIES", "decision", $"решение-по-кэшированию-{id8}.md"))
            .ShouldBeFalse();
        var convergedRelative = $"ENTRIES/decision/cache-decision-{id8}.md";
        Directory.EnumerateFiles(Path.Combine(_projectDir, "ENTRIES", "decision"))
            .ShouldHaveSingleItem()
            .ShouldBe(Path.Combine(_projectDir, "ENTRIES", "decision", $"cache-decision-{id8}.md"));

        var rows = await resync.PlanningMaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .Select(m => m.RelativePath)
            .ToListAsync();
        rows.Where(p => p.StartsWith("ENTRIES/", StringComparison.Ordinal))
            .ShouldHaveSingleItem()
            .ShouldBe(convergedRelative);
    }

    [Fact]
    public async Task Sync_ArchiveStrategy_WritesEntriesUnderArchiveRoot()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.Archive);
        var entryId = await SeedEntryIdAsync(projectId, EntryType.Decision, "Archived Decision");

        var relative = $"ENTRIES/decision/archived-decision-{entryId.ToString("N")[..8]}.md";
        using var ctx = _db.CreateContext();
        var report = await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        var archiveRoot = Path.Combine(_dataDir, "projects", "demo", "backlog");
        File.Exists(Path.Combine(archiveRoot, relative.Replace('/', Path.DirectorySeparatorChar))).ShouldBeTrue();
        Directory.Exists(Path.Combine(_projectDir, "ENTRIES")).ShouldBeFalse();
        report.Strategy.ShouldBe(PlanningMaterialization.Archive);
        report.Files.ShouldContain(f => f.RelativePath == relative);

        var row = await ctx.PlanningMaterializedFiles
            .SingleAsync(m => m.ProjectId == projectId && m.RelativePath == relative);
        row.FeatureId.ShouldBeNull();
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

    private static string EntryRelativePath(EntryType type, string title, Guid entryId) =>
        $"ENTRIES/{type.ToString().ToLowerInvariant()}/{BacklogNaming.MirrorSlug(title, "entry")}-{entryId.ToString("N")[..8]}.md";

    private async Task<Guid> SeedEntryIdAsync(Guid projectId, EntryType type, string title)
    {
        using var seed = _db.CreateContext();
        var entry = ContextEntry.Create(projectId, type, title, $"{title} body.");
        seed.ContextEntries.Add(entry);
        await seed.SaveChangesAsync();
        return entry.Id;
    }

    private async Task<string> SeedEntryAsync(Guid projectId, EntryType type, string title)
    {
        var entryId = await SeedEntryIdAsync(projectId, type, title);
        return EntryRelativePath(type, title, entryId);
    }

    private async Task<string> SeedSyncedEntryAsync(Guid projectId, EntryType type, string title)
    {
        var relative = await SeedEntryAsync(projectId, type, title);
        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);
        return relative;
    }

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
