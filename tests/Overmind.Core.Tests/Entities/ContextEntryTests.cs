using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public class ContextEntryTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();

    [Theory]
    [InlineData(EntryType.Note)]
    [InlineData(EntryType.Decision)]
    [InlineData(EntryType.Gate)]
    [InlineData(EntryType.Guard)]
    public void Create_SetsTypeAndTimestamps(EntryType type)
    {
        var before = DateTime.UtcNow;

        var entry = ContextEntry.Create(ProjectId, type, "Title", "content");

        entry.Id.ShouldNotBe(Guid.Empty);
        entry.ProjectId.ShouldBe(ProjectId);
        entry.Type.ShouldBe(type);
        entry.Title.ShouldBe("Title");
        entry.Content.ShouldBe("content");
        entry.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
        entry.UpdatedAt.ShouldBe(entry.CreatedAt);
        entry.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void Create_SkillTypeThroughCreate_Throws()
    {
        Should.Throw<InvalidOperationException>(() =>
            ContextEntry.Create(ProjectId, EntryType.Skill, "s", "c"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_EmptyTitle_Throws(string title)
    {
        Should.Throw<ArgumentException>(() =>
            ContextEntry.Create(ProjectId, EntryType.Note, title, "content"));
    }

    [Fact]
    public void CreateSkill_WithoutFiles_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            ContextEntry.CreateSkill(ProjectId, "skill", "readme", []));
    }

    [Fact]
    public void CreateSkill_AttachesFilesAndSetsType()
    {
        var files = new[]
        {
            SkillFile.Create(AgentPlatform.Claude, "reviewer/SKILL.md", "# Reviewer"),
            SkillFile.Create(AgentPlatform.KimiCode, "reviewer/SKILL.md", "# Reviewer"),
        };

        var entry = ContextEntry.CreateSkill(ProjectId, "reviewer", "does reviews", files);

        entry.Type.ShouldBe(EntryType.Skill);
        entry.SkillFiles.Count.ShouldBe(2);
        entry.SkillFiles.ShouldAllBe(f => f.EntryId == entry.Id);
    }

    [Fact]
    public void UpdateContent_AppendsVersionSnapshotOfPreviousContent()
    {
        var entry = ContextEntry.Create(ProjectId, EntryType.Note, "n", "v1");

        var first = entry.UpdateContent("v2");

        entry.Content.ShouldBe("v2");
        entry.UpdatedAt.ShouldBeGreaterThanOrEqualTo(entry.CreatedAt);
        entry.Versions.Count.ShouldBe(1);
        first.Version.ShouldBe(1);
        first.Content.ShouldBe("v1");
        first.EntryId.ShouldBe(entry.Id);

        var second = entry.UpdateContent("v3");

        second.Version.ShouldBe(2);
        second.Content.ShouldBe("v2");
        entry.Versions.Select(v => v.Version).ShouldBe([1, 2]);
        entry.CurrentVersion.ShouldBe(2);
    }

    [Fact]
    public void Create_StartsAtVersionZero()
    {
        var entry = ContextEntry.Create(ProjectId, EntryType.Note, "n", "v1");

        entry.CurrentVersion.ShouldBe(0);
        entry.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateContent_EmptyContent_Throws()
    {
        var entry = ContextEntry.Create(ProjectId, EntryType.Note, "n", "v1");

        Should.Throw<ArgumentException>(() => entry.UpdateContent(" "));
        entry.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void AddTag_AddsEntryTagLink()
    {
        var entry = ContextEntry.Create(ProjectId, EntryType.Note, "n", "c");
        var tag = Tag.Create("backend");

        entry.AddTag(tag);

        entry.EntryTags.Count.ShouldBe(1);
        entry.EntryTags[0].TagId.ShouldBe(tag.Id);
        entry.EntryTags[0].EntryId.ShouldBe(entry.Id);
    }
}
