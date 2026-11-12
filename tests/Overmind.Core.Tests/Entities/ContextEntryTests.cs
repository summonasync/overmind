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
    [InlineData(EntryType.Idea)]
    [InlineData(EntryType.Tombstone)]
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

    [Fact]
    public void CreateTombstone_BuriesOriginalWithFencedMemory()
    {
        var original = ContextEntry.Create(
            ProjectId, EntryType.Decision, "pick zebra", "first take\n```text\nv1\n```");
        original.UpdateContent("final take\n```csharp\nvar x = 1;\n```\nend");

        var tombstone = ContextEntry.CreateTombstone(
            original, PlanningLogSource.Ui, null, "superseded by the yak decision");

        tombstone.ProjectId.ShouldBe(ProjectId);
        tombstone.Type.ShouldBe(EntryType.Tombstone);
        tombstone.Title.ShouldBe("pick zebra (deleted)");
        tombstone.Versions.ShouldBeEmpty();
        tombstone.Content.ShouldStartWith("Deleted ");
        tombstone.Content.ShouldContain("via human door.");
        tombstone.Content.ShouldContain("Reason: superseded by the yak decision");
        tombstone.Content.ShouldContain("Original type: decision");
        tombstone.Content.ShouldContain(original.Content);
        tombstone.Content.ShouldNotContain("first take");
        tombstone.Content.ShouldEndWith("end\n````");
    }

    [Fact]
    public void CreateTombstone_WithoutReason_OmitsReasonLine()
    {
        var original = ContextEntry.Create(ProjectId, EntryType.Note, "plain", "body");

        var tombstone = ContextEntry.CreateTombstone(original, PlanningLogSource.Agent, "Van (claude)");

        tombstone.Content.ShouldNotContain("Reason:");
        tombstone.Content.ShouldContain("via agent door by Van (claude).");
        tombstone.Content.ShouldContain("Original type: note");
    }

    [Fact]
    public void CreateTombstone_ReasonWithBackticks_SizesTheFencePastBothTexts()
    {
        var original = ContextEntry.Create(ProjectId, EntryType.Note, "plain", "body");
        var reason = "why ```` not — demo grave";

        var tombstone = ContextEntry.CreateTombstone(original, PlanningLogSource.Agent, "Van (claude)", reason);

        var fence = new string('`', 5);
        tombstone.Content.ShouldContain($"{fence}\nbody\n{fence}");
    }

    [Fact]
    public void CreateTombstone_HumanDoor_OmitsActor()
    {
        var original = ContextEntry.Create(ProjectId, EntryType.Gate, "deploy gate", "body");

        var tombstone = ContextEntry.CreateTombstone(original, PlanningLogSource.Ui, null);

        tombstone.Content.ShouldContain("via human door.");
        tombstone.Content.ShouldNotContain(" by ");
    }

    [Fact]
    public void CreateTombstone_AutoSource_IsRefused()
    {
        var original = ContextEntry.Create(ProjectId, EntryType.Note, "plain", "body");

        Should.Throw<ArgumentException>(() =>
            ContextEntry.CreateTombstone(original, PlanningLogSource.Auto, null));
    }

    [Fact]
    public void CreateTombstone_OnTombstone_Throws()
    {
        var original = ContextEntry.Create(ProjectId, EntryType.Note, "plain", "body");
        var tombstone = ContextEntry.CreateTombstone(original, PlanningLogSource.Agent, "Van");

        Should.Throw<InvalidOperationException>(() =>
            ContextEntry.CreateTombstone(tombstone, PlanningLogSource.Agent, "Van"));
    }

    [Fact]
    public void Lift_WithoutSuccessor_SetsLiftedStateWithProvenance()
    {
        var entry = ContextEntry.Create(ProjectId, EntryType.Decision, "pick zebra", "first take");

        entry.Lift("replaced by the yak decision", "Van (claude)", "reply 42", null);

        entry.State.ShouldBe(ContextEntry.LiftedState);
        entry.StateAt.ShouldNotBeNull();
        entry.StateAt.Value.ShouldBeGreaterThanOrEqualTo(entry.CreatedAt);
        entry.StateBy.ShouldBe("Van (claude)");
        entry.StateReason.ShouldBe("replaced by the yak decision");
        entry.StateProof.ShouldBe("reply 42");
        entry.SupersededById.ShouldBeNull();
    }

    [Fact]
    public void Lift_WithSuccessor_SetsSupersededStateAndSuccessorLink()
    {
        var entry = ContextEntry.Create(ProjectId, EntryType.Gate, "deploy gate", "body");
        var successor = Guid.NewGuid();

        entry.Lift("moved to the pipeline gate", "Dan (qwen)", null, successor);

        entry.State.ShouldBe(ContextEntry.SupersededState);
        entry.SupersededById.ShouldBe(successor);
        entry.StateReason.ShouldBe("moved to the pipeline gate");
        entry.StateProof.ShouldBeNull();
    }

    [Theory]
    [InlineData(EntryType.Skill)]
    [InlineData(EntryType.Tombstone)]
    public void Lift_SkillAndTombstoneTypes_Throw(EntryType type)
    {
        var entry = type == EntryType.Skill
            ? ContextEntry.CreateSkill(
                ProjectId, "reviewer", "reviews code",
                [SkillFile.Create(AgentPlatform.Claude, "reviewer/SKILL.md", "# Reviewer")])
            : ContextEntry.CreateTombstone(
                ContextEntry.Create(ProjectId, EntryType.Note, "plain", "body"),
                PlanningLogSource.Agent, "Van");

        Should.Throw<InvalidOperationException>(() =>
                entry.Lift("why", "Van", null, null))
            .Message.ShouldContain(type.ToString());

        entry.State.ShouldBe(ContextEntry.ActiveState);
        entry.StateAt.ShouldBeNull();
        entry.SupersededById.ShouldBeNull();
    }

    [Fact]
    public void Lift_ReLiftWithoutSuccessor_OverwritesProvenanceAndClearsSuccessor()
    {
        var entry = ContextEntry.Create(ProjectId, EntryType.Decision, "pick zebra", "first take");

        entry.Lift("moved to the pipeline gate", "Dan (qwen)", null, Guid.NewGuid());
        var supersededAt = entry.StateAt!.Value;

        entry.Lift("contradicted by the BFF rule", "Van (claude)", "reply 42", null);

        entry.State.ShouldBe(ContextEntry.LiftedState);
        entry.StateAt.Value.ShouldBeGreaterThanOrEqualTo(supersededAt);
        entry.StateBy.ShouldBe("Van (claude)");
        entry.StateReason.ShouldBe("contradicted by the BFF rule");
        entry.StateProof.ShouldBe("reply 42");
        entry.SupersededById.ShouldBeNull();
    }

    [Fact]
    public void Lift_SelfSupersede_Throws()
    {
        var entry = ContextEntry.Create(ProjectId, EntryType.Decision, "pick zebra", "first take");

        Should.Throw<InvalidOperationException>(() =>
                entry.Lift("why", "Van", null, entry.Id))
            .Message.ShouldContain("supersede");

        entry.State.ShouldBe(ContextEntry.ActiveState);
        entry.StateAt.ShouldBeNull();
    }

    [Fact]
    public void Lift_BlankReason_Throws()
    {
        var entry = ContextEntry.Create(ProjectId, EntryType.Decision, "pick zebra", "first take");

        Should.Throw<ArgumentException>(() => entry.Lift(" ", null, null, null));
        entry.State.ShouldBe(ContextEntry.ActiveState);
    }

    [Fact]
    public void AppendFollowUp_BumpsVersionAndDatesTheSection()
    {
        var entry = ContextEntry.Create(ProjectId, EntryType.Note, "n", "base take");

        var version = entry.AppendFollowUp("confirmed after review");

        entry.Content.ShouldBe(
            $"base take\n\n## Follow-up {DateTime.UtcNow:yyyy-MM-dd}\nconfirmed after review");
        entry.CurrentVersion.ShouldBe(1);
        version.Version.ShouldBe(1);
        version.Content.ShouldBe("base take");
        version.EntryId.ShouldBe(entry.Id);
    }

    [Fact]
    public void AppendFollowUp_BlankText_ThrowsWithoutVersionBump()
    {
        var entry = ContextEntry.Create(ProjectId, EntryType.Note, "n", "base take");

        Should.Throw<ArgumentException>(() => entry.AppendFollowUp(" "));

        entry.Content.ShouldBe("base take");
        entry.CurrentVersion.ShouldBe(0);
        entry.Versions.ShouldBeEmpty();
    }
}
