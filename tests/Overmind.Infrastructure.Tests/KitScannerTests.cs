using Overmind.Core;
using Overmind.Infrastructure.Kits;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class KitScannerTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"overmind-kit-scan-{Guid.NewGuid():N}");

    public KitScannerTests()
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

    private KitScanner CreateScanner() => new(_context, PlatformDirectoryMap.Load());

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
        string name, string description = "Does a thing.", string? extraField = null) =>
        $"---\nname: {name}\ndescription: {description}\n{extraField}\n---\n\nBody.\n";

    private string WriteSkill(string skillDirName, string? content = null, params string[] parentDirs)
    {
        var dir = MakeDir([.. parentDirs, skillDirName]);
        WriteFile(Path.Combine(dir, "SKILL.md"), content ?? SkillYaml(skillDirName));
        return dir;
    }

    [Fact]
    public async Task Scan_EmptyGateAndGuardFiles_ProduceEmptyFileIssueAndNoEntry()
    {
        WriteSkill("deploy");
        WriteFile(Path.Combine(_root, "gates", "empty-gate.md"), string.Empty);
        WriteFile(Path.Combine(_root, "gates", "review.md"), "# Review Gate\n\nAlways review.\n");
        WriteFile(Path.Combine(_root, "guards", "blank-guard.md"), "   \n\t\n");

        var result = await CreateScanner().ScanAsync(_root);

        var candidate = result.Candidates.ShouldHaveSingleItem();
        candidate.Gates.ShouldHaveSingleItem().RelativePath.ShouldBe(
            $"gates{Path.DirectorySeparatorChar}review.md");
        candidate.Guards.ShouldBeEmpty();

        var emptyIssues = candidate.Issues.Where(i => i.Code == KitScanIssueCode.EmptyFile).ToList();
        emptyIssues.Select(i => i.RelativePath).ShouldBe(
            [
                $"gates{Path.DirectorySeparatorChar}empty-gate.md",
                $"guards{Path.DirectorySeparatorChar}blank-guard.md"
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Scan_SingleKitAtRoot_ReturnsOneCandidate()
    {
        WriteSkill("deploy");
        WriteFile(Path.Combine(_root, "deploy", "scripts", "run.sh"), "#!/bin/sh\n");

        var result = await CreateScanner().ScanAsync(_root);

        result.Truncated.ShouldBeFalse();
        var candidate = result.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe(new DirectoryInfo(_root).Name);
        candidate.Path.ShouldBe(Path.GetFullPath(_root));
        candidate.RelativePath.ShouldBe(string.Empty);
        candidate.NameCollision.ShouldBeFalse();
        candidate.ReImportMatchKitId.ShouldBeNull();

        var skill = candidate.Skills.ShouldHaveSingleItem();
        skill.Name.ShouldBe("deploy");
        skill.Platform.ShouldBeNull();
        skill.FileCount.ShouldBe(2);
        candidate.Issues.ShouldBeEmpty();
    }

    [Fact]
    public async Task Scan_ContainerFolder_ReturnsCandidatePerTopLevelKit()
    {
        WriteSkill("alpha", parentDirs: ["kit-a"]);
        WriteSkill("beta", parentDirs: ["kit-b"]);
        WriteFile(Path.Combine(_root, "README.md"), "container\n");

        var result = await CreateScanner().ScanAsync(_root);

        result.Candidates.Count.ShouldBe(2);
        result.Candidates.Select(c => c.Name).ShouldBe(["kit-a", "kit-b"]);
        result.Candidates.Select(c => c.RelativePath).ShouldBe(["kit-a", "kit-b"]);
        result.Candidates[0].Skills.ShouldHaveSingleItem().Name.ShouldBe("alpha");
        result.Candidates[1].Skills.ShouldHaveSingleItem().Name.ShouldBe("beta");
    }

    [Fact]
    public async Task Scan_FolderRoots_QualifyMarkdownProseButNotScripts()
    {
        WriteSkill("deploy", parentDirs: ["dotnet-crew"]);
        WriteFile(Path.Combine(_root, "sociality", "SOCIAL_DICTIONARY.md"), "# Tone rules.\n");
        WriteFile(Path.Combine(_root, "sociality", "GLOSSARY.MD"), "# Terms.\n");
        WriteFile(Path.Combine(_root, "build", "pack.sh"), "#!/bin/sh\n");
        WriteFile(Path.Combine(_root, "build", "manifest.json"), "{}\n");

        var result = await CreateScanner().ScanAsync(_root);

        result.Candidates.Select(c => c.RelativePath).ShouldBe(["dotnet-crew", "sociality"]);
        var prose = result.Candidates.Single(c => c.RelativePath == "sociality");
        prose.Skills.ShouldBeEmpty();
        result.Candidates.Any(c => c.RelativePath == "build").ShouldBeFalse();
    }

    [Fact]
    public async Task Scan_PlatformMarkedAndAgnosticSkills_DetectsPlatforms()
    {
        WriteSkill("deploy", parentDirs: [".claude", "skills"]);
        WriteSkill("lint");

        var result = await CreateScanner().ScanAsync(_root);

        var candidate = result.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe(new DirectoryInfo(_root).Name);
        candidate.Skills.Count.ShouldBe(2);

        var platformSkill = candidate.Skills.Single(s => s.Name == "deploy");
        platformSkill.Platform.ShouldBe(AgentPlatform.Claude);
        platformSkill.RelativePath.ShouldBe(
            $".claude{Path.DirectorySeparatorChar}skills{Path.DirectorySeparatorChar}deploy");

        var agnosticSkill = candidate.Skills.Single(s => s.Name == "lint");
        agnosticSkill.Platform.ShouldBeNull();
        agnosticSkill.RelativePath.ShouldBe("lint");
    }

    [Fact]
    public async Task Scan_PlatformDirInsideKitSubFolder_DetectsPlatformVariant()
    {
        WriteSkill("deploy", parentDirs: ["kit-a", ".claude", "skills"]);
        WriteSkill("lint", parentDirs: ["kit-a"]);

        var result = await CreateScanner().ScanAsync(_root);

        var candidate = result.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("kit-a");
        candidate.Skills.Count.ShouldBe(2);

        var platformSkill = candidate.Skills.Single(s => s.Name == "deploy");
        platformSkill.Platform.ShouldBe(AgentPlatform.Claude);
        platformSkill.RelativePath.ShouldBe(
            $"kit-a{Path.DirectorySeparatorChar}.claude{Path.DirectorySeparatorChar}skills{Path.DirectorySeparatorChar}deploy");
        candidate.Skills.Single(s => s.Name == "lint").Platform.ShouldBeNull();
    }

    [Fact]
    public async Task Scan_GatesAndGuards_ReturnsTitlesFromH1WithFilenameFallback()
    {
        WriteSkill("deploy");
        WriteFile(
            Path.Combine(_root, "gates", "review.md"),
            "# Review Gate\n\nAlways review.\n");
        WriteFile(
            Path.Combine(_root, "guards", "no-delete.md"),
            "Do not delete entries.\n");

        var result = await CreateScanner().ScanAsync(_root);

        var candidate = result.Candidates.ShouldHaveSingleItem();
        candidate.Gates.ShouldHaveSingleItem().Title.ShouldBe("Review Gate");
        var guard = candidate.Guards.ShouldHaveSingleItem();
        guard.Title.ShouldBe("no-delete");
        guard.RelativePath.ShouldBe($"guards{Path.DirectorySeparatorChar}no-delete.md");
    }

    [Fact]
    public async Task Scan_BrokenSkillFiles_ProduceIssuesWithoutAborting()
    {
        WriteSkill("ok");
        WriteSkill("wrong-name", SkillYaml("something-else"));
        WriteSkill("no-frontmatter", "just a body, no frontmatter at all\n");
        WriteSkill("bad-type", SkillYaml("bad-type", extraField: "type: flow"));
        WriteFile(Path.Combine(MakeDir("empty-skill"), "SKILL.md"), string.Empty);
        WriteSkill("long-desc", SkillYaml("long-desc", description: new string('x', 1100)));

        var result = await CreateScanner().ScanAsync(_root);

        var candidate = result.Candidates.ShouldHaveSingleItem();
        candidate.Skills.Select(s => s.Name).ShouldContain("ok");
        candidate.Skills.Count.ShouldBe(6);

        var codesBySkill = candidate.Issues
            .GroupBy(i => Path.GetDirectoryName(i.RelativePath))
            .ToDictionary(g => g.Key!, g => g.Select(i => i.Code).ToList());

        codesBySkill["wrong-name"].ShouldBe([KitScanIssueCode.InvalidName]);
        codesBySkill["no-frontmatter"].ShouldBe([KitScanIssueCode.InvalidFrontmatter]);
        codesBySkill["bad-type"].ShouldBe([KitScanIssueCode.InvalidType]);
        codesBySkill["empty-skill"].ShouldBe([KitScanIssueCode.EmptyFile]);
        codesBySkill["long-desc"].ShouldBe([KitScanIssueCode.InvalidDescription]);
        codesBySkill.ShouldNotContainKey("ok");
    }

    [Fact]
    public async Task Scan_PlatformNativeFields_FlaggedOnlyOnAgnosticSkills()
    {
        WriteSkill(
            "deploy",
            SkillYaml("deploy", extraField: "when_to_use: on release"),
            ".claude", "skills");
        WriteSkill("lint", SkillYaml("lint", extraField: "when_to_use: always"));

        var result = await CreateScanner().ScanAsync(_root);

        var candidate = result.Candidates.ShouldHaveSingleItem();
        var deployIssues = candidate.Issues
            .Where(i => i.RelativePath.Contains("deploy"))
            .ToList();
        deployIssues.ShouldBeEmpty();

        var lintIssues = candidate.Issues
            .Where(i => i.RelativePath.Contains("lint"))
            .ToList();
        lintIssues.ShouldHaveSingleItem().Code.ShouldBe(KitScanIssueCode.PlatformNativeField);
    }

    [Fact]
    public async Task Scan_IgnoreList_SkipsHiddenAndDependencyDirectories()
    {
        WriteSkill("visible");
        WriteSkill("hidden-skill", parentDirs: [".hidden"]);
        WriteSkill("pkg-skill", parentDirs: ["node_modules", "pkg"]);
        WriteSkill("git-skill", parentDirs: [".git"]);
        WriteSkill("bin-skill", parentDirs: ["bin"]);

        var result = await CreateScanner().ScanAsync(_root);

        var candidate = result.Candidates.ShouldHaveSingleItem();
        candidate.Skills.ShouldHaveSingleItem().Name.ShouldBe("visible");
    }

    [Fact]
    public async Task Scan_SymbolicLinkDirectory_IsNotFollowed()
    {
        WriteSkill("visible");
        var outside = Path.Combine(Path.GetTempPath(), $"overmind-kit-out-{Guid.NewGuid():N}");
        try
        {
            WriteFile(Path.Combine(outside, "linked-skill", "SKILL.md"), SkillYaml("linked-skill"));
            Directory.CreateDirectory(Path.Combine(_root, "linked"));
            try
            {
                Directory.CreateSymbolicLink(
                    Path.Combine(_root, "linked", "inner"), Path.Combine(outside, "linked-skill"));
            }
            catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
            {
                return;
            }

            var result = await CreateScanner().ScanAsync(_root);

            var candidate = result.Candidates.ShouldHaveSingleItem();
            candidate.Skills.ShouldHaveSingleItem().Name.ShouldBe("visible");
        }
        finally
        {
            if (Directory.Exists(outside))
            {
                Directory.Delete(outside, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Scan_SymbolicLinkSkillFile_IsNotReadAndSurfacesIssue()
    {
        WriteSkill("visible");
        var outside = Path.Combine(Path.GetTempPath(), $"overmind-kit-out-{Guid.NewGuid():N}");
        try
        {
            var outsideSkill = Path.Combine(outside, "secret.md");
            WriteFile(outsideSkill, SkillYaml("evil"));
            var linkPath = Path.Combine(MakeDir("evil"), "SKILL.md");
            try
            {
                File.CreateSymbolicLink(linkPath, outsideSkill);
            }
            catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
            {
                return;
            }

            var result = await CreateScanner().ScanAsync(_root);

            var candidate = result.Candidates.ShouldHaveSingleItem();
            candidate.Skills.ShouldHaveSingleItem().Name.ShouldBe("visible");
            var issue = candidate.Issues.ShouldHaveSingleItem();
            issue.Code.ShouldBe(KitScanIssueCode.UnreadableFile);
            issue.Message.ShouldBe("Symbolic links are not supported.");
            issue.RelativePath.ShouldBe($"evil{Path.DirectorySeparatorChar}SKILL.md");
        }
        finally
        {
            if (Directory.Exists(outside))
            {
                Directory.Delete(outside, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Scan_DirectoryCapExceeded_SetsTruncatedFlag()
    {
        for (var i = 0; i < 25; i++)
        {
            for (var j = 0; j < 21; j++)
            {
                MakeDir($"w{i}", $"c{j}");
            }
        }

        var result = await CreateScanner().ScanAsync(_root);

        result.Truncated.ShouldBeTrue();
    }

    [Fact]
    public async Task Scan_NameCollision_FlagsExistingKit()
    {
        var kit = SkillKit.Create("dup", 1, "existing");
        _context.SkillKits.Add(kit);
        await _context.SaveChangesAsync();

        WriteSkill("alpha", parentDirs: ["dup"]);

        var result = await CreateScanner().ScanAsync(_root);

        var candidate = result.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("dup");
        candidate.NameCollision.ShouldBeTrue();
        candidate.NameCollisionKitId.ShouldBe(kit.Id);
        candidate.ReImportMatchKitId.ShouldBeNull();
    }

    [Fact]
    public async Task Scan_ReImportMatch_FlagsKitBySourcePath()
    {
        var kit = SkillKit.Create("other", 1, "existing");
        kit.MarkImported(Path.Combine(_root, "kit-a"));
        _context.SkillKits.Add(kit);
        await _context.SaveChangesAsync();

        WriteSkill("alpha", parentDirs: ["kit-a"]);

        var result = await CreateScanner().ScanAsync(_root);

        var candidate = result.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("kit-a");
        candidate.NameCollision.ShouldBeFalse();
        candidate.ReImportMatchKitId.ShouldBe(kit.Id);
    }

    [Fact]
    public async Task Scan_MissingPath_ThrowsPathMissing()
    {
        var missing = Path.Combine(_root, "nope");

        var ex = await Should.ThrowAsync<KitImportException>(() => CreateScanner().ScanAsync(missing));

        ex.Kind.ShouldBe(KitImportFailureKind.PathMissing);
    }

    [Fact]
    public async Task Scan_FilePath_ThrowsNotADirectory()
    {
        var file = Path.Combine(_root, "file.txt");
        await File.WriteAllTextAsync(file, "x");

        var ex = await Should.ThrowAsync<KitImportException>(() => CreateScanner().ScanAsync(file));

        ex.Kind.ShouldBe(KitImportFailureKind.NotADirectory);
    }
}
