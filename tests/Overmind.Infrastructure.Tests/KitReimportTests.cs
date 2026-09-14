using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Kits;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class KitReimportTests : IDisposable
{
    private static readonly DateTime Backdate = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"overmind-kit-reimport-{Guid.NewGuid():N}");

    public KitReimportTests()
    {
        _context = _db.CreateContext();
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private KitImporter CreateImporter()
    {
        var platformMap = PlatformDirectoryMap.Load();
        return new KitImporter(_context, new KitScanner(_context, platformMap), platformMap);
    }

    private string MakeDir(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string SkillYaml(
        string name, string description = "Does a thing.") =>
        $"---\nname: {name}\ndescription: {description}\n---\n\nBody.\n";

    private string WriteSkill(string skillDirName, string? content = null, params string[] parentDirs)
    {
        var dir = MakeDir([.. parentDirs, skillDirName]);
        WriteFile(Path.Combine(dir, "SKILL.md"), content ?? SkillYaml(skillDirName));
        return dir;
    }

    private async Task<Guid> ImportKitAsync(string candidateName)
    {
        var report = await CreateImporter().CommitAsync(_root, [new KitImportSelection(candidateName, candidateName)]);
        var result = report.Kits.ShouldHaveSingleItem();
        result.Success.ShouldBeTrue();
        return result.KitId!.Value;
    }

    private static async Task<SkillKit> LoadKitAsync(OvermindDbContext context, Guid kitId)
    {
        return await context.SkillKits
            .Include(k => k.SkillKitEntries)
            .ThenInclude(e => e.Entry)
            .ThenInclude(e => e.SkillFiles)
            .Include(k => k.SkillKitEntries)
            .ThenInclude(e => e.Entry)
            .ThenInclude(e => e.Versions)
            .FirstAsync(k => k.Id == kitId);
    }

    [Fact]
    public async Task Reimport_UpstreamChangeOnly_UpdatesSkillSnapshotsVersionAndHash()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        WriteFile(
            Path.Combine(_root, "kit-a", "gates", "review.md"),
            "# Review Gate\n\nAlways review.\n");
        var kitId = await ImportKitAsync("kit-a");

        var updatedYaml = SkillYaml("alpha", "Does a better thing.");
        WriteFile(Path.Combine(_root, "kit-a", "alpha", "SKILL.md"), updatedYaml);

        var report = await CreateImporter().ReimportAsync(kitId);

        report.KitId.ShouldBe(kitId);
        report.ScannedPath.ShouldBe(Path.GetFullPath(Path.Combine(_root, "kit-a")));
        report.UpdatedCount.ShouldBe(1);
        report.KeptLocalCount.ShouldBe(1);
        report.AddedCount.ShouldBe(0);
        report.ConflictedCount.ShouldBe(0);
        report.RemovedCount.ShouldBe(0);

        var updated = report.Entries.Single(e => e.Outcome == KitReimportOutcome.Updated);
        updated.EntryType.ShouldBe(EntryType.Skill);
        updated.Title.ShouldBe("alpha");
        updated.RelativePath.ShouldBe("alpha");
        updated.ImportedContentHash.ShouldBe(ContentHash.Sha256(updatedYaml));

        using var verify = _db.CreateContext();
        var kit = await LoadKitAsync(verify, kitId);
        var link = kit.SkillKitEntries.Single(l => l.Entry.Title == "alpha");
        link.Entry.Content.ShouldBe(updatedYaml);
        link.Entry.CurrentVersion.ShouldBe(1);
        var snapshot = link.Entry.Versions.ShouldHaveSingleItem();
        snapshot.Version.ShouldBe(1);
        snapshot.Content.ShouldBe(SkillYaml("alpha"));
        link.ImportedContentHash.ShouldBe(ContentHash.Sha256(updatedYaml));

        var gate = kit.SkillKitEntries.Single(l => l.Entry.Type == EntryType.Gate);
        gate.Entry.Content.ShouldBe("# Review Gate\n\nAlways review.\n");
        gate.Entry.Versions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reimport_LocalEditOnly_KeepsLocalContentVersionAndHash()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        var kitId = await ImportKitAsync("kit-a");

        var kit = await LoadKitAsync(_context, kitId);
        kit.SkillKitEntries.ShouldHaveSingleItem().Entry.UpdateContent("local rewrite\n");
        await _context.SaveChangesAsync();

        var report = await CreateImporter().ReimportAsync(kitId);

        report.KeptLocalCount.ShouldBe(1);
        report.UpdatedCount.ShouldBe(0);
        report.ConflictedCount.ShouldBe(0);
        var kept = report.Entries.ShouldHaveSingleItem();
        kept.Outcome.ShouldBe(KitReimportOutcome.KeptLocal);
        kept.ImportedContentHash.ShouldBe(ContentHash.Sha256(SkillYaml("alpha")));

        using var verify = _db.CreateContext();
        var reloaded = await LoadKitAsync(verify, kitId);
        var link = reloaded.SkillKitEntries.ShouldHaveSingleItem();
        link.Entry.Content.ShouldBe("local rewrite\n");
        link.Entry.CurrentVersion.ShouldBe(1);
        link.Entry.Versions.ShouldHaveSingleItem();
        link.ImportedContentHash.ShouldBe(ContentHash.Sha256(SkillYaml("alpha")));
    }

    [Fact]
    public async Task Reimport_BothChanged_RefusesConflictAndOldHashReReports()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        WriteFile(
            Path.Combine(_root, "kit-a", "gates", "review.md"),
            "# Review Gate\n\nAlways review.\n");
        var kitId = await ImportKitAsync("kit-a");
        var originalSkillHash = ContentHash.Sha256(SkillYaml("alpha"));
        var originalGateHash = ContentHash.Sha256("# Review Gate\n\nAlways review.\n");

        var kit = await LoadKitAsync(_context, kitId);
        kit.SkillKitEntries.Single(l => l.Entry.Title == "alpha").Entry.UpdateContent("local skill edit\n");
        kit.SkillKitEntries.Single(l => l.Entry.Type == EntryType.Gate).Entry.UpdateContent("local gate edit\n");
        await _context.SaveChangesAsync();

        WriteFile(Path.Combine(_root, "kit-a", "alpha", "SKILL.md"), SkillYaml("alpha", "Upstream rewrite."));
        WriteFile(
            Path.Combine(_root, "kit-a", "gates", "review.md"),
            "# Review Gate\n\nUpstream rewrite.\n");

        var report = await CreateImporter().ReimportAsync(kitId);

        report.ConflictedCount.ShouldBe(2);
        report.UpdatedCount.ShouldBe(0);
        report.Entries.ShouldAllBe(e => e.ImportedContentHash != null);

        using (var verify = _db.CreateContext())
        {
            var reloaded = await LoadKitAsync(verify, kitId);
            var skillLink = reloaded.SkillKitEntries.Single(l => l.Entry.Title == "alpha");
            skillLink.Entry.Content.ShouldBe("local skill edit\n");
            skillLink.Entry.Versions.ShouldHaveSingleItem();
            skillLink.ImportedContentHash.ShouldBe(originalSkillHash);

            var gateLink = reloaded.SkillKitEntries.Single(l => l.Entry.Type == EntryType.Gate);
            gateLink.Entry.Content.ShouldBe("local gate edit\n");
            gateLink.ImportedContentHash.ShouldBe(originalGateHash);
        }

        var again = await CreateImporter().ReimportAsync(kitId);

        again.ConflictedCount.ShouldBe(2);
        again.Entries
            .Where(e => e.Title == "alpha" || e.EntryType == EntryType.Gate)
            .ShouldAllBe(e => e.ImportedContentHash != null);
    }

    [Fact]
    public async Task Reimport_NoChanges_KeepsAllEntriesLocal()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        WriteFile(
            Path.Combine(_root, "kit-a", "guards", "no-delete.md"),
            "Do not delete entries.\n");
        var kitId = await ImportKitAsync("kit-a");

        var report = await CreateImporter().ReimportAsync(kitId);

        report.KeptLocalCount.ShouldBe(2);
        report.UpdatedCount.ShouldBe(0);
        report.AddedCount.ShouldBe(0);
        report.ConflictedCount.ShouldBe(0);
        report.RemovedCount.ShouldBe(0);

        using var verify = _db.CreateContext();
        var reloaded = await LoadKitAsync(verify, kitId);
        reloaded.SkillKitEntries.ShouldAllBe(l => !l.Entry.Versions.Any());
        reloaded.SkillKitEntries.ShouldAllBe(l => l.Entry.CurrentVersion == 0);
    }

    [Fact]
    public async Task Reimport_NewFiles_AddEntriesWithHashes()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        var kitId = await ImportKitAsync("kit-a");

        WriteSkill("beta", parentDirs: ["kit-a"]);
        WriteFile(
            Path.Combine(_root, "kit-a", "guards", "no-delete.md"),
            "Do not delete entries.\n");

        var report = await CreateImporter().ReimportAsync(kitId);

        report.AddedCount.ShouldBe(2);
        report.KeptLocalCount.ShouldBe(1);

        var beta = report.Entries.Single(e => e.Title == "beta");
        beta.Outcome.ShouldBe(KitReimportOutcome.Added);
        beta.EntryId.ShouldNotBeNull();
        beta.ImportedContentHash.ShouldBe(ContentHash.Sha256(SkillYaml("beta")));

        var guard = report.Entries.Single(e => e.EntryType == EntryType.Guard);
        guard.Outcome.ShouldBe(KitReimportOutcome.Added);
        guard.Title.ShouldBe("no-delete");
        guard.ImportedContentHash.ShouldBe(ContentHash.Sha256("Do not delete entries.\n"));

        using var verify = _db.CreateContext();
        var reloaded = await LoadKitAsync(verify, kitId);
        reloaded.SkillKitEntries.Count.ShouldBe(3);
        var betaLink = reloaded.SkillKitEntries.Single(l => l.Entry.Title == "beta");
        betaLink.ImportedContentHash.ShouldBe(ContentHash.Sha256(SkillYaml("beta")));
        betaLink.Entry.SkillFiles.ShouldHaveSingleItem().RelativePath.ShouldBe("SKILL.md");
    }

    [Fact]
    public async Task Reimport_RemovedFiles_DeletesKitOwnedEntries()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        WriteFile(
            Path.Combine(_root, "kit-a", "gates", "review.md"),
            "# Review Gate\n\nAlways review.\n");
        WriteFile(
            Path.Combine(_root, "kit-a", "guards", "no-delete.md"),
            "Do not delete entries.\n");
        var kitId = await ImportKitAsync("kit-a");

        Directory.Delete(Path.Combine(_root, "kit-a", "alpha"), recursive: true);
        File.Delete(Path.Combine(_root, "kit-a", "gates", "review.md"));

        var report = await CreateImporter().ReimportAsync(kitId);

        report.RemovedCount.ShouldBe(2);
        report.KeptLocalCount.ShouldBe(1);
        var removed = report.Entries.Where(e => e.Outcome == KitReimportOutcome.Removed).ToList();
        removed.ShouldContain(e => e.Title == "alpha");
        removed.ShouldContain(e => e.EntryType == EntryType.Gate);
        removed.ShouldAllBe(e => e.EntryId != null);

        using var verify = _db.CreateContext();
        (await verify.ContextEntries.CountAsync()).ShouldBe(1);
        (await verify.SkillKitEntries.CountAsync()).ShouldBe(1);
        var reloaded = await LoadKitAsync(verify, kitId);
        reloaded.SkillKitEntries.ShouldHaveSingleItem().Entry.Type.ShouldBe(EntryType.Guard);
    }

    [Fact]
    public async Task Reimport_RemovedEntryLinkedElsewhere_RemovesLinkKeepsEntryRow()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        var kitId = await ImportKitAsync("kit-a");

        var alphaEntryId = (await LoadKitAsync(_context, kitId)).SkillKitEntries.ShouldHaveSingleItem().Entry.Id;
        var other = SkillKit.Create("manual-pack", 1, "other");
        other.AddEntry(alphaEntryId);
        _context.SkillKits.Add(other);
        await _context.SaveChangesAsync();

        Directory.Delete(Path.Combine(_root, "kit-a", "alpha"), recursive: true);

        var report = await CreateImporter().ReimportAsync(kitId);

        report.RemovedCount.ShouldBe(1);

        using var verify = _db.CreateContext();
        (await verify.ContextEntries.CountAsync()).ShouldBe(1);
        (await verify.SkillKitEntries.CountAsync()).ShouldBe(1);

        var reloadedOther = await verify.SkillKits
            .Include(k => k.SkillKitEntries)
            .FirstAsync(k => k.Id == other.Id);
        reloadedOther.SkillKitEntries.ShouldHaveSingleItem().EntryId.ShouldBe(alphaEntryId);

        var reloaded = await verify.SkillKits.FirstAsync(k => k.Id == kitId);
        reloaded.SkillKitEntries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reimport_GateFileChanged_UpdatesGateContent()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        WriteFile(
            Path.Combine(_root, "kit-a", "gates", "review.md"),
            "# Review Gate\n\nAlways review.\n");
        var kitId = await ImportKitAsync("kit-a");

        var updatedGate = "# Review Gate\n\nAlways review carefully.\n";
        WriteFile(Path.Combine(_root, "kit-a", "gates", "review.md"), updatedGate);

        var report = await CreateImporter().ReimportAsync(kitId);

        report.UpdatedCount.ShouldBe(1);
        report.KeptLocalCount.ShouldBe(1);
        var updated = report.Entries.Single(e => e.Outcome == KitReimportOutcome.Updated);
        updated.EntryType.ShouldBe(EntryType.Gate);
        updated.RelativePath.ShouldBe("gates/review.md");
        updated.ImportedContentHash.ShouldBe(ContentHash.Sha256(updatedGate));

        using var verify = _db.CreateContext();
        var reloaded = await LoadKitAsync(verify, kitId);
        var gateLink = reloaded.SkillKitEntries.Single(l => l.Entry.Type == EntryType.Gate);
        gateLink.Entry.Content.ShouldBe(updatedGate);
        gateLink.Entry.CurrentVersion.ShouldBe(1);
        gateLink.ImportedContentHash.ShouldBe(ContentHash.Sha256(updatedGate));
    }

    [Fact]
    public async Task Reimport_UpdatedSkill_ResyncsExtraFiles()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        WriteFile(
            Path.Combine(_root, "kit-a", "alpha", "scripts", "run.sh"),
            "#!/bin/sh\necho hi\n");
        var kitId = await ImportKitAsync("kit-a");

        var updatedYaml = SkillYaml("alpha", "Does a better thing.");
        WriteFile(Path.Combine(_root, "kit-a", "alpha", "SKILL.md"), updatedYaml);
        File.Delete(Path.Combine(_root, "kit-a", "alpha", "scripts", "run.sh"));
        WriteFile(
            Path.Combine(_root, "kit-a", "alpha", "notes", "tips.md"),
            "Tips.\n");

        var report = await CreateImporter().ReimportAsync(kitId);

        report.UpdatedCount.ShouldBe(1);

        using var verify = _db.CreateContext();
        var reloaded = await LoadKitAsync(verify, kitId);
        var link = reloaded.SkillKitEntries.ShouldHaveSingleItem();
        link.Entry.Content.ShouldBe(updatedYaml);

        var paths = link.Entry.SkillFiles.Select(f => f.RelativePath).OrderBy(p => p, StringComparer.Ordinal).ToList();
        paths.ShouldBe(["SKILL.md", Path.Combine("notes", "tips.md")]);
        link.Entry.SkillFiles.ShouldAllBe(f => f.Platform == null);
    }

    [Fact]
    public async Task Reimport_PlatformSkillChanged_UpdatesAndKeepsPlatform()
    {
        WriteSkill("deploy", parentDirs: ["kit-a", ".claude", "skills"]);
        var kitId = await ImportKitAsync("kit-a");

        var updatedYaml = SkillYaml("deploy", "Deploys better.");
        WriteFile(
            Path.Combine(_root, "kit-a", ".claude", "skills", "deploy", "SKILL.md"),
            updatedYaml);

        var report = await CreateImporter().ReimportAsync(kitId);

        report.UpdatedCount.ShouldBe(1);

        using var verify = _db.CreateContext();
        var reloaded = await LoadKitAsync(verify, kitId);
        var link = reloaded.SkillKitEntries.ShouldHaveSingleItem();
        link.Entry.Content.ShouldBe(updatedYaml);
        link.Entry.SkillFiles.ShouldHaveSingleItem().Platform.ShouldBe(AgentPlatform.Claude);
        link.ImportedContentHash.ShouldBe(ContentHash.Sha256(updatedYaml));
    }

    [Fact]
    public async Task Reimport_PlatformSkillUnchanged_StaysKeptLocal()
    {
        WriteSkill("deploy", parentDirs: ["kit-a", ".claude", "skills"]);
        var kitId = await ImportKitAsync("kit-a");

        var report = await CreateImporter().ReimportAsync(kitId);

        report.KeptLocalCount.ShouldBe(1);

        using var verify = _db.CreateContext();
        var reloaded = await LoadKitAsync(verify, kitId);
        var link = reloaded.SkillKitEntries.ShouldHaveSingleItem();
        link.Entry.Versions.ShouldBeEmpty();
        link.ImportedContentHash.ShouldBe(ContentHash.Sha256(SkillYaml("deploy")));
    }

    [Fact]
    public async Task Reimport_EmptyUpstreamSkill_RefusedAsConflictKeepsLocal()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        var kitId = await ImportKitAsync("kit-a");

        WriteFile(Path.Combine(_root, "kit-a", "alpha", "SKILL.md"), string.Empty);

        var report = await CreateImporter().ReimportAsync(kitId);

        report.ConflictedCount.ShouldBe(1);
        report.UpdatedCount.ShouldBe(0);
        var conflict = report.Entries.ShouldHaveSingleItem();
        conflict.Outcome.ShouldBe(KitReimportOutcome.Conflicted);
        conflict.ImportedContentHash.ShouldBe(ContentHash.Sha256(SkillYaml("alpha")));

        using var verify = _db.CreateContext();
        var reloaded = await LoadKitAsync(verify, kitId);
        var link = reloaded.SkillKitEntries.ShouldHaveSingleItem();
        link.Entry.Content.ShouldBe(SkillYaml("alpha"));
        link.Entry.Versions.ShouldBeEmpty();
        link.ImportedContentHash.ShouldBe(ContentHash.Sha256(SkillYaml("alpha")));
    }

    [Fact]
    public async Task Reimport_BumpsImportedAtUtc()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        var kitId = await ImportKitAsync("kit-a");

        var before = DateTime.UtcNow;
        await _context.Database.ExecuteSqlAsync(
            $"UPDATE \"SkillKits\" SET \"ImportedAtUtc\" = {Backdate} WHERE \"Id\" = {kitId}");

        await CreateImporter().ReimportAsync(kitId);

        using var verify = _db.CreateContext();
        var reloaded = await verify.SkillKits.FirstAsync(k => k.Id == kitId);
        reloaded.ImportedAtUtc.ShouldNotBeNull();
        reloaded.ImportedAtUtc!.Value.ShouldBeGreaterThan(Backdate);
        reloaded.ImportedAtUtc!.Value.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public async Task Reimport_ManualKit_ThrowsNoSource()
    {
        var manual = SkillKit.Create("manual-pack", 1, "d");
        _context.SkillKits.Add(manual);
        await _context.SaveChangesAsync();

        var ex = await Should.ThrowAsync<KitImportException>(() => CreateImporter().ReimportAsync(manual.Id));

        ex.Kind.ShouldBe(KitImportFailureKind.NoSource);
    }

    [Fact]
    public async Task Reimport_MissingSourceDirectory_ThrowsSourceMissing()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        var kitId = await ImportKitAsync("kit-a");

        Directory.Delete(Path.Combine(_root, "kit-a"), recursive: true);

        var ex = await Should.ThrowAsync<KitImportException>(() => CreateImporter().ReimportAsync(kitId));

        ex.Kind.ShouldBe(KitImportFailureKind.SourceMissing);
        ex.Message.ShouldContain(Path.GetFullPath(Path.Combine(_root, "kit-a")));
    }

    [Fact]
    public async Task Reimport_UnknownKit_ThrowsKitNotFound()
    {
        var ex = await Should.ThrowAsync<KitImportException>(() => CreateImporter().ReimportAsync(Guid.NewGuid()));

        ex.Kind.ShouldBe(KitImportFailureKind.KitNotFound);
    }
}
