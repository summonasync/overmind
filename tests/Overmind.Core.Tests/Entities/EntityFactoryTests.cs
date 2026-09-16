using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public class ProjectTests
{
    [Fact]
    public void Create_SetsFieldsAndUtcTimestamp()
    {
        var before = DateTime.UtcNow;

        var project = Project.Create("Overmind", "/disk/path", "context keeper");

        project.Id.ShouldNotBe(Guid.Empty);
        project.Name.ShouldBe("Overmind");
        project.DiskPath.ShouldBe("/disk/path");
        project.Description.ShouldBe("context keeper");
        project.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
        project.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_EmptyName_Throws(string name)
    {
        Should.Throw<ArgumentException>(() => Project.Create(name, "/p"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_EmptyDiskPath_Throws(string diskPath)
    {
        Should.Throw<ArgumentException>(() => Project.Create("n", diskPath));
    }
}

public class SkillFileTests
{
    [Fact]
    public void Create_SetsFields()
    {
        var file = SkillFile.Create(AgentPlatform.Codex, "reviewer/SKILL.md", "# Skill");

        file.Id.ShouldNotBe(Guid.Empty);
        file.Platform.ShouldBe(AgentPlatform.Codex);
        file.RelativePath.ShouldBe("reviewer/SKILL.md");
        file.Content.ShouldBe("# Skill");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_EmptyRelativePath_Throws(string? relativePath)
    {
        Should.Throw<ArgumentException>(() => SkillFile.Create(AgentPlatform.Claude, relativePath!, "c"));
    }
}

public class TagTests
{
    [Fact]
    public void Create_TrimsName()
    {
        var tag = Tag.Create("  backend  ");

        tag.Name.ShouldBe("backend");
        tag.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_EmptyName_Throws(string name)
    {
        Should.Throw<ArgumentException>(() => Tag.Create(name));
    }
}

public class SkillKitTests
{
    [Fact]
    public void Create_SetsFields()
    {
        var kit = SkillKit.Create("review-pack", 1, "review skills");

        kit.Id.ShouldNotBe(Guid.Empty);
        kit.Name.ShouldBe("review-pack");
        kit.Version.ShouldBe(1);
        kit.Description.ShouldBe("review skills");
    }

    [Fact]
    public void AddEntry_AddsJoinRow()
    {
        var kit = SkillKit.Create("k", 1, "d");
        var entryId = Guid.NewGuid();

        kit.AddEntry(entryId);

        kit.SkillKitEntries.Count.ShouldBe(1);
        kit.SkillKitEntries[0].KitId.ShouldBe(kit.Id);
        kit.SkillKitEntries[0].EntryId.ShouldBe(entryId);
    }

    [Fact]
    public void AddEntry_Duplicate_Throws()
    {
        var kit = SkillKit.Create("k", 1, "d");
        var entryId = Guid.NewGuid();
        kit.AddEntry(entryId);

        Should.Throw<InvalidOperationException>(() => kit.AddEntry(entryId));
    }

    [Fact]
    public void Create_DefaultsToManualSource()
    {
        var kit = SkillKit.Create("k", 1, "d");

        kit.SourceKind.ShouldBe(KitSourceKind.Manual);
        kit.SourcePath.ShouldBeNull();
        kit.ImportedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void MarkImported_SetsSourceAndStampsUtc()
    {
        var before = DateTime.UtcNow;
        var kit = SkillKit.Create("k", 1, "d");

        kit.MarkImported("  /kits/review-pack  ");

        kit.SourceKind.ShouldBe(KitSourceKind.Imported);
        kit.SourcePath.ShouldBe("/kits/review-pack");
        kit.ImportedAtUtc.ShouldNotBeNull();
        kit.ImportedAtUtc!.Value.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public void MarkImported_OnAlreadyImported_ReStampsPathAndDate()
    {
        var kit = SkillKit.Create("k", 1, "d");
        kit.MarkImported("/kits/first");
        var firstStamp = kit.ImportedAtUtc;

        var before = DateTime.UtcNow;
        kit.MarkImported("/kits/second");

        kit.SourceKind.ShouldBe(KitSourceKind.Imported);
        kit.SourcePath.ShouldBe("/kits/second");
        kit.ImportedAtUtc!.Value.ShouldBeGreaterThanOrEqualTo(firstStamp!.Value);
        kit.ImportedAtUtc!.Value.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void MarkImported_EmptyPath_Throws(string? sourcePath)
    {
        var kit = SkillKit.Create("k", 1, "d");

        Should.Throw<ArgumentException>(() => kit.MarkImported(sourcePath!));
    }

    [Fact]
    public void MarkImported_EmptyPath_LeavesSourceUntouched()
    {
        var kit = SkillKit.Create("k", 1, "d");

        Should.Throw<ArgumentException>(() => kit.MarkImported(""));

        kit.SourceKind.ShouldBe(KitSourceKind.Manual);
        kit.SourcePath.ShouldBeNull();
        kit.ImportedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void MarkBundled_SetsKindAndPath_LeavesImportedAtNull()
    {
        var kit = SkillKit.Create("k", 1, "d");

        kit.MarkBundled("  /bundled/review-pack  ");

        kit.SourceKind.ShouldBe(KitSourceKind.Bundled);
        kit.SourcePath.ShouldBe("/bundled/review-pack");
        kit.ImportedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void MarkBundled_OnPreviouslyImportedKit_ClearsImportStamp()
    {
        var kit = SkillKit.Create("k", 1, "d");
        kit.MarkImported("/kits/first");

        kit.MarkBundled("/bundled/review-pack");

        kit.SourceKind.ShouldBe(KitSourceKind.Bundled);
        kit.SourcePath.ShouldBe("/bundled/review-pack");
        kit.ImportedAtUtc.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void MarkBundled_EmptyPath_Throws(string? sourcePath)
    {
        var kit = SkillKit.Create("k", 1, "d");

        Should.Throw<ArgumentException>(() => kit.MarkBundled(sourcePath!));
    }

    [Fact]
    public void ClearSource_AfterImport_ResetsEverything()
    {
        var kit = SkillKit.Create("k", 1, "d");
        kit.MarkImported("/kits/review-pack");

        kit.ClearSource();

        kit.SourceKind.ShouldBe(KitSourceKind.Manual);
        kit.SourcePath.ShouldBeNull();
        kit.ImportedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void ClearSource_OnManualKit_StaysManualWithNulls()
    {
        var kit = SkillKit.Create("k", 1, "d");

        kit.ClearSource();

        kit.SourceKind.ShouldBe(KitSourceKind.Manual);
        kit.SourcePath.ShouldBeNull();
        kit.ImportedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void ClearSource_OnBundledKit_ThrowsTypedFailureAndKeepsOrigin()
    {
        var kit = SkillKit.Create("k", 1, "d");
        kit.MarkBundled("/bundled/review-pack");

        var ex = Should.Throw<KitImportException>(() => kit.ClearSource());

        ex.Kind.ShouldBe(KitImportFailureKind.BundledSourceProtected);
        ex.Message.ShouldContain(kit.Id.ToString());
        kit.SourceKind.ShouldBe(KitSourceKind.Bundled);
        kit.SourcePath.ShouldBe("/bundled/review-pack");
        kit.ImportedAtUtc.ShouldBeNull();
    }
}

public class ProjectKitTests
{
    [Fact]
    public void Create_DefaultsToActive()
    {
        var projectId = Guid.NewGuid();
        var kitId = Guid.NewGuid();

        var link = ProjectKit.Create(projectId, kitId);

        link.ProjectId.ShouldBe(projectId);
        link.KitId.ShouldBe(kitId);
        link.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Deactivate_Activate_TogglesFlag()
    {
        var link = ProjectKit.Create(Guid.NewGuid(), Guid.NewGuid());

        link.Deactivate();
        link.IsActive.ShouldBeFalse();

        link.Activate();
        link.IsActive.ShouldBeTrue();
    }
}
