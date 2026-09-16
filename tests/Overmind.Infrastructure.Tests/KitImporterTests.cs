using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Kits;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class KitImporterTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"overmind-kit-import-{Guid.NewGuid():N}");

    public KitImporterTests()
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

    private async Task<SkillKit> LoadKitAsync(Guid kitId)
    {
        return await _context.SkillKits
            .Include(k => k.SkillKitEntries)
            .ThenInclude(e => e.Entry)
            .ThenInclude(e => e.SkillFiles)
            .FirstAsync(k => k.Id == kitId);
    }

    [Fact]
    public async Task Commit_TwoKits_CreatesBothFullyLinked()
    {
        WriteSkill("deploy", parentDirs: ["kit-a", ".claude", "skills"]);
        WriteFile(
            Path.Combine(_root, "kit-a", ".claude", "skills", "deploy", "scripts", "run.sh"),
            "#!/bin/sh\necho hi\n");
        WriteSkill("lint", parentDirs: ["kit-a"]);
        WriteFile(
            Path.Combine(_root, "kit-a", "gates", "review.md"),
            "# Review Gate\n\nAlways review.\n");
        WriteFile(
            Path.Combine(_root, "kit-a", "guards", "no-delete.md"),
            "Do not delete entries.\n");
        WriteSkill("backup", parentDirs: ["kit-b"]);

        var report = await CreateImporter().CommitAsync(_root,
            [new KitImportSelection("kit-a", "kit-a"), new KitImportSelection("kit-b", "kit-b")]);

        report.Kits.Count.ShouldBe(2);
        report.Kits.ShouldAllBe(k => k.Success);
        report.Kits[0].KitId.ShouldNotBeNull();
        report.Kits[1].KitId.ShouldNotBeNull();

        var kitA = await LoadKitAsync(report.Kits[0].KitId!.Value);
        kitA.Name.ShouldBe("kit-a");
        kitA.SourceKind.ShouldBe(KitSourceKind.Imported);
        kitA.SourcePath.ShouldBe(Path.GetFullPath(Path.Combine(_root, "kit-a")));
        kitA.ImportedAtUtc.ShouldNotBeNull();
        kitA.Version.ShouldBe(1);

        var kitB = await LoadKitAsync(report.Kits[1].KitId!.Value);
        kitB.SourceKind.ShouldBe(KitSourceKind.Imported);
        kitB.SourcePath.ShouldBe(Path.GetFullPath(Path.Combine(_root, "kit-b")));

        var deploy = kitA.SkillKitEntries.Select(l => l.Entry).Single(e => e.Title == "deploy");
        deploy.Type.ShouldBe(EntryType.Skill);
        deploy.SkillFiles.Count.ShouldBe(2);
        var deploySkillMd = deploy.SkillFiles.Single(f => f.RelativePath == "SKILL.md");
        deploySkillMd.Platform.ShouldBe(AgentPlatform.Claude);
        var deployScript = deploy.SkillFiles.Single(f => f.RelativePath == Path.Combine("scripts", "run.sh"));
        deployScript.Platform.ShouldBe(AgentPlatform.Claude);

        var lint = kitA.SkillKitEntries.Select(l => l.Entry).Single(e => e.Title == "lint");
        var lintFile = lint.SkillFiles.ShouldHaveSingleItem();
        lintFile.Platform.ShouldBeNull();
        lintFile.RelativePath.ShouldBe("SKILL.md");

        var gate = kitA.SkillKitEntries.Select(l => l.Entry).Single(e => e.Type == EntryType.Gate);
        gate.Title.ShouldBe("Review Gate");
        var guard = kitA.SkillKitEntries.Select(l => l.Entry).Single(e => e.Type == EntryType.Guard);
        guard.Title.ShouldBe("no-delete");

        kitA.SkillKitEntries.Count.ShouldBe(4);
        kitB.SkillKitEntries.ShouldHaveSingleItem().Entry.Title.ShouldBe("backup");

        foreach (var link in kitA.SkillKitEntries.Concat(kitB.SkillKitEntries))
        {
            link.ImportedContentHash.ShouldBe(ContentHash.Sha256(link.Entry.Content));
        }

        var libraryProject = await _context.Projects.SingleAsync(p => p.Name == KitImporter.LibraryProjectName);
        foreach (var link in kitA.SkillKitEntries.Concat(kitB.SkillKitEntries))
        {
            link.Entry.ProjectId.ShouldBe(libraryProject.Id);
        }
    }

    [Fact]
    public async Task Commit_PersistsKitRelativePathsOnSkillLinks()
    {
        WriteSkill("boss", parentDirs: ["kit-a", "skills"]);
        WriteSkill("deploy", parentDirs: ["kit-a", ".claude", "skills"]);
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        WriteFile(
            Path.Combine(_root, "kit-a", "gates", "review.md"),
            "# Review Gate\n\nAlways review.\n");

        var report = await CreateImporter().CommitAsync(_root, [new KitImportSelection("kit-a", "kit-a")]);

        var kit = await LoadKitAsync(report.Kits[0].KitId!.Value);
        kit.SkillKitEntries.Count.ShouldBe(4);
        kit.SkillKitEntries.Single(l => l.Entry.Title == "boss").RelativePath.ShouldBe("skills/boss");
        kit.SkillKitEntries.Single(l => l.Entry.Title == "deploy").RelativePath.ShouldBe(".claude/skills/deploy");
        kit.SkillKitEntries.Single(l => l.Entry.Title == "alpha").RelativePath.ShouldBe("alpha");
        kit.SkillKitEntries.Single(l => l.Entry.Type == EntryType.Gate).RelativePath.ShouldBeNull();
    }

    [Fact]
    public async Task Commit_SameTitleOnTwoPlatforms_CreatesDistinctEntries()
    {
        WriteSkill("deploy", parentDirs: ["kit-a", ".claude", "skills"]);
        WriteSkill("deploy", parentDirs: ["kit-a", ".kimi-code", "skills"]);

        var report = await CreateImporter().CommitAsync(_root, [new KitImportSelection("kit-a", "kit-a")]);

        var kit = await LoadKitAsync(report.Kits[0].KitId!.Value);
        var links = kit.SkillKitEntries
            .OrderBy(l => l.RelativePath, StringComparer.Ordinal)
            .ToList();
        links.Count.ShouldBe(2);
        links.ShouldAllBe(l => l.Entry.Title == "deploy");
        links[0].RelativePath.ShouldBe(".claude/skills/deploy");
        links[1].RelativePath.ShouldBe(".kimi-code/skills/deploy");
        links[0].Entry.SkillFiles.ShouldHaveSingleItem().Platform.ShouldBe(AgentPlatform.Claude);
        links[1].Entry.SkillFiles.ShouldHaveSingleItem().Platform.ShouldBe(AgentPlatform.KimiCode);
        links[0].EntryId.ShouldNotBe(links[1].EntryId);
    }

    [Fact]
    public async Task Commit_EditedName_AppliesToCreatedKit()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);

        var report = await CreateImporter().CommitAsync(_root,
            [new KitImportSelection("kit-a", "My Renamed Kit")]);

        var result = report.Kits.ShouldHaveSingleItem();
        result.Success.ShouldBeTrue();
        var kit = await LoadKitAsync(result.KitId!.Value);
        kit.Name.ShouldBe("My Renamed Kit");
        kit.SourcePath.ShouldBe(Path.GetFullPath(Path.Combine(_root, "kit-a")));
    }

    [Fact]
    public async Task Commit_CrlfContent_HashMatchesLfNormalizedContent()
    {
        var crlfContent = "---\r\nname: deploy\r\ndescription: Does a thing.\r\n---\r\n\r\nBody.\r\n";
        WriteSkill("deploy", crlfContent, "kit-a");

        var report = await CreateImporter().CommitAsync(_root,
            [new KitImportSelection("kit-a", "kit-a")]);

        var link = (await LoadKitAsync(report.Kits[0].KitId!.Value)).SkillKitEntries.ShouldHaveSingleItem();
        link.Entry.Content.ShouldBe(crlfContent);
        link.ImportedContentHash.ShouldBe(ContentHash.Sha256(crlfContent));

        var lfContent = crlfContent.Replace("\r\n", "\n", StringComparison.Ordinal);
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lfContent)));
        link.ImportedContentHash.ShouldBe(expected);
    }

    [Fact]
    public async Task Commit_NameCollision_FailsOnlyThatKit()
    {
        var seeded = SkillKit.Create("KIT-B", 1, "seeded");
        _context.SkillKits.Add(seeded);
        await _context.SaveChangesAsync();

        WriteSkill("alpha", parentDirs: ["kit-a"]);
        WriteSkill("beta", parentDirs: ["kit-b"]);

        var report = await CreateImporter().CommitAsync(_root,
            [new KitImportSelection("kit-a", "kit-a"), new KitImportSelection("kit-b", "kit-b")]);

        report.Kits[0].Success.ShouldBeTrue();
        report.Kits[1].Success.ShouldBeFalse();
        report.Kits[1].FailureKind.ShouldBe(KitImportFailureKind.DuplicateName);
        report.Kits[1].KitId.ShouldBeNull();

        var kits = await _context.SkillKits.Select(k => k.Name).ToListAsync();
        kits.ShouldBe(["KIT-B", "kit-a"], ignoreOrder: true);
    }

    [Fact]
    public async Task Commit_FailingKit_LeavesNoPartialRowsAndSkipsNothingElse()
    {
        WriteSkill("broken", string.Empty, "kit-bad");
        WriteSkill("fine", parentDirs: ["kit-good"]);

        var report = await CreateImporter().CommitAsync(_root,
            [new KitImportSelection("kit-bad", "kit-bad"), new KitImportSelection("kit-good", "kit-good")]);

        report.Kits[0].Success.ShouldBeFalse();
        report.Kits[0].KitId.ShouldBeNull();
        report.Kits[0].Error.ShouldNotBeNull();
        report.Kits[1].Success.ShouldBeTrue();

        (await _context.SkillKits.CountAsync()).ShouldBe(1);
        (await _context.ContextEntries.CountAsync()).ShouldBe(1);
        (await _context.SkillKitEntries.CountAsync()).ShouldBe(1);
        (await _context.SkillFiles.CountAsync()).ShouldBe(1);

        var goodKit = await LoadKitAsync(report.Kits[1].KitId!.Value);
        goodKit.SkillKitEntries.ShouldHaveSingleItem().Entry.Title.ShouldBe("fine");
    }

    [Fact]
    public async Task Commit_ReImportMatch_FailsWithTypedFailure()
    {
        var seeded = SkillKit.Create("kit-a", 1, "existing");
        seeded.MarkImported(Path.Combine(_root, "kit-a"));
        _context.SkillKits.Add(seeded);
        await _context.SaveChangesAsync();

        WriteSkill("alpha", parentDirs: ["kit-a"]);

        var report = await CreateImporter().CommitAsync(_root,
            [new KitImportSelection("kit-a", "kit-a")]);

        var result = report.Kits.ShouldHaveSingleItem();
        result.FailureKind.ShouldBe(KitImportFailureKind.ReImportRequired);
        result.KitId.ShouldBeNull();
        (await _context.SkillKits.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Commit_UnknownCandidate_FailsWithTypedFailure()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);

        var report = await CreateImporter().CommitAsync(_root,
            [new KitImportSelection("nope", "nope")]);

        var result = report.Kits.ShouldHaveSingleItem();
        result.FailureKind.ShouldBe(KitImportFailureKind.CandidateNotFound);
        (await _context.SkillKits.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Commit_EmptyGateFile_SkipsThatGate()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        WriteFile(Path.Combine(_root, "kit-a", "gates", "empty.md"), string.Empty);
        WriteFile(Path.Combine(_root, "kit-a", "gates", "review.md"), "# Review Gate\n\nReview.\n");

        var report = await CreateImporter().CommitAsync(_root,
            [new KitImportSelection("kit-a", "kit-a")]);

        var kit = await LoadKitAsync(report.Kits[0].KitId!.Value);
        var titles = kit.SkillKitEntries.Select(l => l.Entry.Title).ToList();
        titles.ShouldBe(["alpha", "Review Gate"], ignoreOrder: true);
    }

    [Fact]
    public async Task ClearSource_ConvertsToManual_KeepsEntries_AndIsIdempotent()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        var report = await CreateImporter().CommitAsync(_root, [new KitImportSelection("kit-a", "kit-a")]);
        var kitId = report.Kits[0].KitId!.Value;

        var cleared = await CreateImporter().ClearSourceAsync(kitId);

        cleared.SourceKind.ShouldBe(KitSourceKind.Manual);
        cleared.SourcePath.ShouldBeNull();
        cleared.ImportedAtUtc.ShouldBeNull();

        var reloaded = await LoadKitAsync(kitId);
        reloaded.SkillKitEntries.ShouldHaveSingleItem().Entry.Title.ShouldBe("alpha");

        var again = await CreateImporter().ClearSourceAsync(kitId);
        again.SourceKind.ShouldBe(KitSourceKind.Manual);
    }

    [Fact]
    public async Task ClearSource_MissingKit_ThrowsKitNotFound()
    {
        var ex = await Should.ThrowAsync<KitImportException>(() =>
            CreateImporter().ClearSourceAsync(Guid.NewGuid()));

        ex.Kind.ShouldBe(KitImportFailureKind.KitNotFound);
    }
}
