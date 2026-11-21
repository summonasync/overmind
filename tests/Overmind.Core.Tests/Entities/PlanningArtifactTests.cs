using System.Text.RegularExpressions;
using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public class PlanningArtifactTests
{
    private static readonly Guid FeatureId = Guid.NewGuid();

    private const string PatchBody = """
# Plan

<goal>
original goal text
</goal>

<scope>
- keep this line
</scope>

<acceptance-criteria>
- [ ] write the parser
  - [ ] indented criterion with trailing note
- [x] ship the docs
- plain bullet
</acceptance-criteria>

<log>
- 2026-09-01 — seeded
</log>
""";

    private const string AmbiguousAcBody = """
<acceptance-criteria>
- [ ] deploy the service
- [ ] deploy the service
</acceptance-criteria>
""";

    private const string EscapedAcBody = """
<acceptance-criteria>
- [ ] &lt;key&gt; is required
- [ ] plain item
</acceptance-criteria>
""";

    private const string ReadableAcBody = """
<acceptance-criteria>
- [ ] <key> is required
- [ ] plain item
</acceptance-criteria>
""";

    private const string MixedEscapedAcBody = """
<acceptance-criteria>
- [ ] &lt;alpha&gt; step
- [ ] <alpha> step
</acceptance-criteria>
""";

    private const string LoggedBody = """
# Task

<goal>
ship the guard
</goal>

<log>
- 2026-09-01 — seeded
- 2026-09-02 — review feedback applied
</log>
""";

    private const string ResultBody = """
# Result

<goal>
close out the feature
</goal>

shipped behind the flag; totals below.

<log>
- 2026-09-01 — seeded
</log>
""";

    private const string HeadingBody = """
# Spec

## Scope
- ship the locator
### Nested
- deeper still

## Next
later
""";

    private const string HeadingOverLogBody = """
## Log

<log>
- 2026-09-01 — seeded
</log>

## Tail
end
""";

    [Theory]
    [InlineData(PlanningArtifactKind.Spec, PlanningArtifactStatus.Draft)]
    [InlineData(PlanningArtifactKind.Plan, PlanningArtifactStatus.Draft)]
    [InlineData(PlanningArtifactKind.Result, PlanningArtifactStatus.Draft)]
    [InlineData(PlanningArtifactKind.Task, PlanningArtifactStatus.Todo)]
    public void Create_SetsInitialStatusPerKind(PlanningArtifactKind kind, PlanningArtifactStatus expectedStatus)
    {
        var artifact = PlanningArtifact.Create(FeatureId, kind, "body content");

        artifact.Status.ShouldBe(expectedStatus);
        artifact.Version.ShouldBe(1);
        artifact.Kind.ShouldBe(kind);
        artifact.FrozenAt.ShouldBeNull();
    }

    [Fact]
    public void AllowedStatuses_SpecPlanResult_OnlyDraftAndValidated()
    {
        var allowed = PlanningArtifact.AllowedStatuses(PlanningArtifactKind.Spec);

        allowed.ShouldBe([PlanningArtifactStatus.Draft, PlanningArtifactStatus.Validated]);
        PlanningArtifact.AllowedStatuses(PlanningArtifactKind.Plan).ShouldBe(allowed);
        PlanningArtifact.AllowedStatuses(PlanningArtifactKind.Result).ShouldBe(allowed);
    }

    [Fact]
    public void AllowedStatuses_Task_ExcludesValidated()
    {
        var allowed = PlanningArtifact.AllowedStatuses(PlanningArtifactKind.Task);

        allowed.ShouldContain(PlanningArtifactStatus.Todo);
        allowed.ShouldContain(PlanningArtifactStatus.InProgress);
        allowed.ShouldContain(PlanningArtifactStatus.Review);
        allowed.ShouldContain(PlanningArtifactStatus.Done);
        allowed.ShouldContain(PlanningArtifactStatus.Blocked);
        allowed.ShouldNotContain(PlanningArtifactStatus.Validated);
        allowed.ShouldNotContain(PlanningArtifactStatus.Draft);
    }

    [Fact]
    public void SetStatus_TaskCannotBecomeValidated()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body");

        Should.Throw<ArgumentOutOfRangeException>(() =>
            artifact.SetStatus(PlanningArtifactStatus.Validated));
    }

    [Fact]
    public void SetStatus_SpecCannotBecomeDone()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "body");

        Should.Throw<ArgumentOutOfRangeException>(() =>
            artifact.SetStatus(PlanningArtifactStatus.Done));
    }

    [Fact]
    public void Validate_SpecOrPlan_FreezesAndSetsFrozenAt()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "body");
        var before = DateTime.UtcNow;

        artifact.Validate();

        artifact.Status.ShouldBe(PlanningArtifactStatus.Validated);
        artifact.FrozenAt.ShouldNotBeNull();
        artifact.FrozenAt.Value.ShouldBeGreaterThanOrEqualTo(before);
        artifact.IsFrozen.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Task_Throws()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body");

        Should.Throw<InvalidOperationException>(() => artifact.Validate());
    }

    [Fact]
    public void UpdateBody_FrozenWithoutNote_Throws()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "original");
        artifact.Validate();

        Should.Throw<InvalidOperationException>(() =>
            artifact.UpdateBody("new body"));
    }

    [Fact]
    public void UpdateBody_FrozenWithNote_ResetsToDraft_ClearsFrozenAt_AppendsLog()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "original");
        artifact.Validate();
        artifact.IsFrozen.ShouldBeTrue();

        artifact.UpdateBody("new body", "reason for change");

        artifact.Status.ShouldBe(PlanningArtifactStatus.Draft);
        artifact.FrozenAt.ShouldBeNull();
        artifact.IsFrozen.ShouldBeFalse();
        artifact.Body.ShouldContain("reason for change");
        artifact.Body.ShouldContain("<log>");
        artifact.Versions.Count.ShouldBe(1);
        artifact.Versions[0].Body.ShouldBe("original");
        artifact.Version.ShouldBe(2);
    }

    [Fact]
    public void UpdateBody_NotFrozen_SnapshotsVersion()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "v1");

        artifact.UpdateBody("v2");

        artifact.Body.ShouldBe("v2");
        artifact.Version.ShouldBe(2);
        artifact.Versions.Count.ShouldBe(1);
        artifact.Versions[0].Body.ShouldBe("v1");
        artifact.Status.ShouldBe(PlanningArtifactStatus.Todo);
    }

    [Fact]
    public void SetStatus_OffValidated_WithNote_Unfreezes()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "body");
        artifact.Validate();
        artifact.IsFrozen.ShouldBeTrue();

        artifact.SetStatus(PlanningArtifactStatus.Draft, "unfreezing for rework");

        artifact.FrozenAt.ShouldBeNull();
        artifact.Status.ShouldBe(PlanningArtifactStatus.Draft);
        artifact.Body.ShouldContain("unfreezing for rework");
    }

    [Fact]
    public void SetStatus_OffValidated_WithoutNote_Throws()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "body");
        artifact.Validate();

        Should.Throw<InvalidOperationException>(() =>
            artifact.SetStatus(PlanningArtifactStatus.Draft));
    }

    [Fact]
    public void AppendLog_SnapshotsPreviousBody_AndBumpsVersion()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "v1 body");

        artifact.AppendLog("progress note");

        artifact.Version.ShouldBe(2);
        artifact.Versions.ShouldHaveSingleItem().Version.ShouldBe(1);
        artifact.Versions[0].Body.ShouldBe("v1 body");
        artifact.Body.ShouldStartWith("v1 body");
        artifact.Body.ShouldContain("progress note");
    }

    [Fact]
    public void AppendLog_WithSource_AppendsSourcedLine()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body");

        artifact.AppendLog("halfway through the wiring", PlanningLogSource.Agent);

        var lines = PlanningLog.Parse(artifact.Body);
        lines.ShouldHaveSingleItem();
        lines[0].Text.ShouldBe("halfway through the wiring");
        lines[0].Source.ShouldBe(PlanningLogSource.Agent);
    }

    [Fact]
    public void SetStatus_WithNote_AppendsToLog()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body");

        artifact.SetStatus(PlanningArtifactStatus.InProgress, "starting work");

        artifact.Body.ShouldContain("starting work");
        artifact.Body.ShouldContain("<log>");
    }

    [Fact]
    public void SetStatus_WithNoteAndSource_AppendsSourcedLine()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body");

        artifact.SetStatus(PlanningArtifactStatus.Done, "shipped in 043", PlanningLogSource.Auto);

        var lines = PlanningLog.Parse(artifact.Body);
        lines.ShouldHaveSingleItem();
        lines[0].Text.ShouldBe("shipped in 043");
        lines[0].Source.ShouldBe(PlanningLogSource.Auto);
    }

    [Fact]
    public void UpdateBody_NotFrozen_WithNote_AppendsNote()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "v1");

        artifact.UpdateBody("v2", "improved logic");

        artifact.Body.ShouldContain("improved logic");
        artifact.Body.ShouldContain("<log>");
        artifact.Status.ShouldBe(PlanningArtifactStatus.Todo);
    }

    [Fact]
    public void UpdateBody_ThreeConsecutiveCalls_VersionRowsTrackBodies()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body-v1");

        artifact.UpdateBody("body-v2");
        artifact.UpdateBody("body-v3");
        artifact.UpdateBody("body-v4");

        artifact.Version.ShouldBe(4);
        artifact.Body.ShouldBe("body-v4");
        artifact.Versions.Count.ShouldBe(3);
        artifact.Versions[0].Version.ShouldBe(1);
        artifact.Versions[0].Body.ShouldBe("body-v1");
        artifact.Versions[1].Version.ShouldBe(2);
        artifact.Versions[1].Body.ShouldBe("body-v2");
        artifact.Versions[2].Version.ShouldBe(3);
        artifact.Versions[2].Body.ShouldBe("body-v3");
    }

    [Fact]
    public void UpdateBody_ResentIdenticalBodyWithLog_IsNoOp()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, LoggedBody);
        var original = artifact.Body;

        artifact.UpdateBody(LoggedBody);

        artifact.Body.ShouldBe(original);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateBody_TailAppendedLogLine_Passes()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, LoggedBody);

        artifact.UpdateBody("""
            # Task

            <goal>
            ship the guard
            </goal>

            <log>
            - 2026-09-01 — seeded
            - 2026-09-02 — review feedback applied
            - 2026-09-03 — follow-up noted
            </log>
            """);

        PlanningLog.Parse(artifact.Body).Count.ShouldBe(3);
        artifact.Version.ShouldBe(2);
    }

    [Fact]
    public void UpdateBody_LogSectionAddedToBodyWithoutOne_Passes()
    {
        var artifact = PlanningArtifact.Create(
            FeatureId, PlanningArtifactKind.Task, "# Task\n\n<goal>\nship it\n</goal>\n");

        artifact.UpdateBody(
            "# Task\n\n<goal>\nship it\n</goal>\n\n<log>\n- 2026-09-03 — history starts\n</log>\n");

        PlanningLog.Parse(artifact.Body).ShouldHaveSingleItem().Text.ShouldBe("history starts");
    }

    [Fact]
    public void UpdateBody_ReflowedLogLineIndentation_PassesTrimNormalized()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, LoggedBody);

        artifact.UpdateBody("""
            # Task

            <goal>
            ship the guard
            </goal>

            <log>
            - 2026-09-01 — seeded
              - 2026-09-02 — review feedback applied
            </log>
            """);

        PlanningLog.Parse(artifact.Body).Count.ShouldBe(2);
    }

    [Fact]
    public void UpdateBody_RewrittenStoredLogLine_AppendsRewriteAfterStoredHistory()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, LoggedBody);

        artifact.UpdateBody("""
            # Task

            <goal>
            ship the guard
            </goal>

            <log>
            - 2026-09-01 — seeded
            - 2026-09-02 — history was polished
            </log>
            """);

        PlanningLog.Parse(artifact.Body).Select(l => l.Text).ToList()
            .ShouldBe(["seeded", "review feedback applied", "history was polished"]);
    }

    [Fact]
    public void UpdateBody_StaleBodyWithoutAppendedLines_IsByteIdenticalNoOp()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, LoggedBody);
        var original = artifact.Body;

        artifact.UpdateBody("""
            # Task

            <goal>
            ship the guard
            </goal>

            <log>
            - 2026-09-01 — seeded
            </log>
            """);

        artifact.Body.ShouldBe(original);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateBody_ReorderedLogLines_KeepsStoredHistoryAndAppendsIncomingLines()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, LoggedBody);

        artifact.UpdateBody("""
            # Task

            <goal>
            ship the guard
            </goal>

            <log>
            - 2026-09-02 — review feedback applied
            - 2026-09-01 — seeded
            </log>
            """);

        PlanningLog.Parse(artifact.Body).Select(l => l.Text).ToList()
            .ShouldBe(["seeded", "review feedback applied", "review feedback applied", "seeded"]);
    }

    [Fact]
    public void UpdateBody_LogSectionDropped_RestoresStoredLines()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, LoggedBody);

        artifact.UpdateBody("# Task\n\n<goal>\nship the guard\n</goal>\n");

        artifact.Body.ShouldContain("<log>");
        PlanningLog.Parse(artifact.Body).Select(l => l.Text).ToList()
            .ShouldBe(["seeded", "review feedback applied"]);
        artifact.Version.ShouldBe(2);
    }

    [Fact]
    public void UpdateBody_SourceMarkerStrippedFromStoredLine_PreservesMarkerAndAppendsBareLine()
    {
        var artifact = PlanningArtifact.Create(
            FeatureId,
            PlanningArtifactKind.Task,
            "# Task\n\n<log>\n- 2026-09-01 — seeded\n- 2026-09-02 [agent] — hit a wall mid-task\n</log>\n");

        artifact.UpdateBody(
            "# Task\n\n<log>\n- 2026-09-01 — seeded\n- 2026-09-02 — hit a wall mid-task\n</log>\n");

        var lines = PlanningLog.Parse(artifact.Body);
        lines.Count.ShouldBe(3);
        lines[1].Source.ShouldBe(PlanningLogSource.Agent);
        lines[2].Text.ShouldBe("hit a wall mid-task");
        lines[2].Source.ShouldBeNull();
    }

    [Fact]
    public void UpdateBody_NewLinesOnlyOnResend_AppendsAfterStoredHistory()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, LoggedBody);

        artifact.UpdateBody(PlanningLog.Append(artifact.Body, "freshly appended line"));

        PlanningLog.Parse(artifact.Body).Select(l => l.Text).ToList()
            .ShouldBe(["seeded", "review feedback applied", "freshly appended line"]);
        artifact.Version.ShouldBe(2);
    }

    [Fact]
    public void UpdateBody_StoredLinesRepeatedWithTail_StoresNoDuplicates()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, LoggedBody);

        artifact.UpdateBody("""
            # Task

            <goal>
            ship the guard
            </goal>

            <log>
            - 2026-09-01 — seeded
            - 2026-09-02 — review feedback applied
            - 2026-09-03 — shipped
            </log>
            """);

        PlanningLog.Parse(artifact.Body).Select(l => l.Text).ToList()
            .ShouldBe(["seeded", "review feedback applied", "shipped"]);
    }

    [Fact]
    public void UpdateBody_EmptyLogSectionOnResend_PreservesStoredLinesAsNoOp()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, LoggedBody);
        var original = artifact.Body;

        artifact.UpdateBody("# Task\n\n<goal>\nship the guard\n</goal>\n\n<log>\n</log>");

        artifact.Body.ShouldBe(original);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateBody_PreviousUpsertLogNote_NeverNeedsRequotingOnResend()
    {
        var artifact = PlanningArtifact.Create(
            FeatureId, PlanningArtifactKind.Spec, "# Spec\n\n<log>\n- 2026-09-01 — seeded\n</log>\n");

        artifact.UpdateBody("# Spec\n\n<log>\n- 2026-09-01 — seeded\n</log>\n", "first edit");
        artifact.UpdateBody("# Spec\n\n<log>\n- 2026-09-01 — seeded\n</log>\n", "second edit");

        PlanningLog.Parse(artifact.Body).Select(l => l.Text).ToList()
            .ShouldBe(["seeded", "first edit", "second edit"]);
    }

    [Fact]
    public void UpdateBody_TwoLogBlocks_ThrowsPointingAtMergingIntoOneBlock()
    {
        var artifact = PlanningArtifact.Create(
            FeatureId, PlanningArtifactKind.Spec, "# Spec\n\n<log>\n- 2026-09-01 — seeded\n</log>\n");

        var ex = Should.Throw<PlanningException>(() => artifact.UpdateBody(
            "# Spec\n\n<log>\n- 2026-09-01 — seeded\n</log>\n\n<log>\n- 2026-09-02 — later\n</log>\n"));

        ex.Code.ShouldBe(PlanningFailureCode.LogBlocksDuplicated);
        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Message.ShouldContain("2 <log> blocks");
        ex.Message.ShouldContain("single <log> block");
        artifact.Body.ShouldNotContain("later");
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void Create_TwoLogBlocks_ThrowsPointingAtMergingIntoOneBlock()
    {
        var ex = Should.Throw<PlanningException>(() => PlanningArtifact.Create(
            FeatureId,
            PlanningArtifactKind.Spec,
            "# Spec\n\n<log>\n- 2026-09-01 — seeded\n</log>\n\n<log>\n- 2026-09-02 — later\n</log>\n"));

        ex.Code.ShouldBe(PlanningFailureCode.LogBlocksDuplicated);
        ex.Message.ShouldContain("single <log> block");
    }

    [Fact]
    public void PatchSection_ReplacesContent_SnapshotsPreviousBody_AndBumpsVersion()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Plan, PatchBody);
        var original = artifact.Body;

        artifact.PatchSection("goal", "revised goal text");

        artifact.Body.ShouldContain("revised goal text");
        artifact.Body.ShouldNotContain("original goal text");
        artifact.Body.ShouldContain("- keep this line");
        artifact.Body.ShouldContain("[ ] write the parser");
        artifact.Version.ShouldBe(2);
        artifact.Versions.Count.ShouldBe(1);
        artifact.Versions[0].Version.ShouldBe(1);
        artifact.Versions[0].Body.ShouldBe(original);
    }

    [Fact]
    public void PatchSection_MissingSection_OnDocumentKind_InsertsAnyRequestedTag()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Plan, PatchBody);

        artifact.PatchSection("my-tag", "- keep this line");

        artifact.Body.ShouldContain("<my-tag>\n- keep this line\n</my-tag>");
        artifact.Version.ShouldBe(2);
        artifact.Versions.ShouldHaveSingleItem().Body.ShouldBe(PatchBody);
    }

    [Theory]
    [InlineData("Goal")]
    [InlineData("2fa")]
    [InlineData("has_underscore")]
    [InlineData("a.b")]
    public void PatchSection_NameOutsideTheTagGrammar_ThrowsWithoutWriting(string section)
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        var ex = Should.Throw<PlanningException>(() => artifact.PatchSection(section, "content"));

        ex.Code.ShouldBe(PlanningFailureCode.PatchSectionNameNotParseable);
        ex.Message.ShouldContain(section);
        ex.Message.ShouldContain("lowercase letters, digits and hyphens");
        artifact.Body.ShouldBe(PatchBody);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void PatchSection_UppercaseName_OnTask_ThrowsNamingTheGrammar()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, PatchBody);

        var ex = Should.Throw<PlanningException>(() => artifact.PatchSection("Goal", "content"));

        ex.Code.ShouldBe(PlanningFailureCode.PatchSectionNameNotParseable);
        artifact.Version.ShouldBe(1);
    }

    [Fact]
    public void PatchSection_UnknownSection_OnTask_ThrowsListingPresentAndVocabulary()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, PatchBody);

        var ex = Should.Throw<PlanningException>(() => artifact.PatchSection("banana", "content"));

        ex.Code.ShouldBe(PlanningFailureCode.PatchSectionNotFound);
        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Message.ShouldContain("banana");
        ex.Message.ShouldContain("goal");
        ex.Message.ShouldContain("scope");
        ex.Message.ShouldContain("acceptance-criteria");
        ex.Message.ShouldContain("log");
        ex.Message.ShouldContain("Accepted sections for tasks");
        ex.Message.ShouldContain("obstacles");
        ex.Message.ShouldNotContain("one-line");
    }

    [Fact]
    public void PatchSection_MissingDeclaredSection_OnTask_InsertsWithContent()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, PatchBody);

        artifact.PatchSection("obstacles", "- the parser rejects fenced tags");

        artifact.Body.ShouldContain("<obstacles>\n- the parser rejects fenced tags\n</obstacles>");
        artifact.Version.ShouldBe(2);
        artifact.Versions.ShouldHaveSingleItem().Body.ShouldBe(PatchBody);
    }

    [Fact]
    public void PatchSection_MissingDeclaredSection_OnTask_WithoutContent_InsertsDeclaredEmpty()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, PatchBody);

        artifact.PatchSection("obstacles", null);

        PlanningSections.Find("obstacles", artifact.Body)!.Content.ShouldBe(PlanningSections.EmptyMarker);
        artifact.Version.ShouldBe(2);
    }

    [Fact]
    public void PatchSection_ExistingSection_WithoutContent_ThrowsContentRequired()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Plan, PatchBody);

        var ex = Should.Throw<PlanningException>(() => artifact.PatchSection("goal", null));

        ex.Code.ShouldBe(PlanningFailureCode.SectionContentRequiredForReplace);
        ex.Message.ShouldContain("goal");
        artifact.Body.ShouldBe(PatchBody);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void PatchSection_MissingSectionWithInlineCandidate_ThrowsWithCanonicalizationHint()
    {
        var artifact = PlanningArtifact.Create(
            FeatureId,
            PlanningArtifactKind.Plan,
            "<goal>\nShip.\n</goal>\n\n<anti-scope>Parser stays anchored.</anti-scope>");

        var ex = Should.Throw<PlanningException>(() => artifact.PatchSection("anti-scope", "content"));

        ex.Code.ShouldBe(PlanningFailureCode.PatchSectionNotFound);
        ex.Message.ShouldContain("A one-line '<anti-scope>…</anti-scope>' section exists");
        ex.Message.ShouldContain("re-send the body once");
    }

    [Fact]
    public void PatchSection_MissingDeclaredSection_OnTask_WithInlineCandidate_DoesNotInsertADuplicate()
    {
        var artifact = PlanningArtifact.Create(
            FeatureId,
            PlanningArtifactKind.Task,
            "<goal>\nShip.\n</goal>\n\n<obstacles>None yet.</obstacles>");

        var ex = Should.Throw<PlanningException>(() => artifact.PatchSection("obstacles", "the real list"));

        ex.Code.ShouldBe(PlanningFailureCode.PatchSectionNotFound);
        artifact.Body.ShouldNotContain("<obstacles>\n");
        artifact.Version.ShouldBe(1);
    }

    [Fact]
    public void TickAc_InlineAcceptanceCriteria_ThrowsWithCanonicalizationHint()
    {
        var artifact = PlanningArtifact.Create(
            FeatureId,
            PlanningArtifactKind.Task,
            "<goal>\nShip.\n</goal>\n\n<acceptance-criteria>- [ ] ship it</acceptance-criteria>");

        var ex = Should.Throw<PlanningException>(() => artifact.TickAc(1, null));

        ex.Code.ShouldBe(PlanningFailureCode.PatchSectionNotFound);
        ex.Message.ShouldContain("A one-line '<acceptance-criteria>…</acceptance-criteria>' section exists");
    }

    [Theory]
    [InlineData("log")]
    [InlineData("Log")]
    [InlineData("LOG")]
    public void PatchSection_LogSection_ThrowsRedirectingToAppendLog_AndLeavesArtifactUntouched(string section)
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Plan, PatchBody);
        var original = artifact.Body;

        var ex = Should.Throw<PlanningException>(() => artifact.PatchSection(section, "rewritten history"));

        ex.Code.ShouldBe(PlanningFailureCode.PatchLogSectionNotReplaceable);
        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Message.ShouldContain("append-only");
        ex.Message.ShouldContain("planning_append_log");
        ex.Message.ShouldContain("planning_upsert_artifact");
        artifact.Body.ShouldBe(original);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void PatchSection_LogSection_OnFrozenArtifact_ThrowsTheSameRedirect()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);
        artifact.Validate();
        var frozenBody = artifact.Body;

        var ex = Should.Throw<PlanningException>(() =>
            artifact.PatchSection("log", "rewritten history", "note that must not matter"));

        ex.Code.ShouldBe(PlanningFailureCode.PatchLogSectionNotReplaceable);
        artifact.Status.ShouldBe(PlanningArtifactStatus.Validated);
        artifact.Body.ShouldBe(frozenBody);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void PatchSection_FrozenWithoutNote_Throws_AndLeavesArtifactUntouched()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);
        artifact.Validate();
        var frozenBody = artifact.Body;

        Should.Throw<InvalidOperationException>(() => artifact.PatchSection("goal", "no note here"));

        artifact.Status.ShouldBe(PlanningArtifactStatus.Validated);
        artifact.Body.ShouldBe(frozenBody);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void PatchSection_FrozenWithNote_DemotesToDraft_SnapshotsFrozenBody_AppendsLog()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);
        artifact.Validate();
        var frozenBody = artifact.Body;

        artifact.PatchSection("goal", "rework after review", "spec reopened by the human door");

        artifact.Status.ShouldBe(PlanningArtifactStatus.Draft);
        artifact.FrozenAt.ShouldBeNull();
        artifact.Version.ShouldBe(2);
        artifact.Versions.ShouldHaveSingleItem().Body.ShouldBe(frozenBody);
        artifact.Body.ShouldContain("rework after review");
        artifact.Body.ShouldContain("spec reopened by the human door");
    }

    [Fact]
    public void PatchSection_FrozenWithNote_RecordsDemotionEchoAndAutoLine()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);
        artifact.Validate();

        artifact.PatchSection("goal", "rework after review", "spec reopened by the human door");

        artifact.StatusChanged.ShouldBe(PlanningArtifact.DemotedStatusEcho);
        var lines = PlanningLog.Parse(artifact.Body);
        lines.Last().Source.ShouldBe(PlanningLogSource.Auto);
        lines.Last().Text.ShouldBe(PlanningArtifact.DemotionLogNote);
    }

    [Fact]
    public void PatchSection_NotFrozen_StatusChangedStaysNull()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Plan, PatchBody);

        artifact.PatchSection("goal", "fresh goal", "mid-session correction");

        artifact.StatusChanged.ShouldBeNull();
        PlanningLog.Parse(artifact.Body).Count.ShouldBe(2);
        PlanningLog.Parse(artifact.Body).Last().Text.ShouldBe("mid-session correction");
    }

    [Fact]
    public void PatchSection_NotFrozen_WithNote_AppendsLogLine()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Plan, PatchBody);

        artifact.PatchSection("goal", "fresh goal", "mid-session correction");

        artifact.Version.ShouldBe(2);
        artifact.Body.ShouldContain("fresh goal");
        artifact.Body.ShouldContain("mid-session correction");
        artifact.Status.ShouldBe(PlanningArtifactStatus.Draft);
    }

    [Fact]
    public void PatchSection_CrlfBody_ReplacesBytePredictably()
    {
        var body = "# Spec\r\n\r\n<goal>\r\noriginal\r\n</goal>\r\n\r\n<scope>\r\n- unchanged\r\n</scope>\r\n";
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, body);

        artifact.PatchSection("goal", "patched");

        artifact.Body.ShouldBe("# Spec\r\n\r\n<goal>\r\npatched\r\n</goal>\r\n\r\n<scope>\r\n- unchanged\r\n</scope>\r\n");
    }

    [Fact]
    public void PatchSection_EmptyContent_ClearsSection()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Plan, PatchBody);

        artifact.PatchSection("scope", string.Empty);

        PlanningSections.Find("scope", artifact.Body)!.Content.ShouldBe(string.Empty);
        artifact.Body.ShouldContain("<scope>\n</scope>");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void PatchSection_BlankSectionName_Throws(string section)
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Plan, PatchBody);

        Should.Throw<ArgumentException>(() => artifact.PatchSection(section, "content"));
    }

    [Fact]
    public void PatchHeadingSection_PrefixedAndBareAddresses_ResolveIdentically()
    {
        var prefixed = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, HeadingBody);
        var bare = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, HeadingBody);

        prefixed.PatchHeadingSection("## Scope", "replaced", remove: false, reason: null);
        bare.PatchHeadingSection("Scope", "replaced", remove: false, reason: null);

        prefixed.Body.ShouldBe("# Spec\n\n## Scope\nreplaced\n## Next\nlater");
        bare.Body.ShouldBe(prefixed.Body);
        prefixed.Version.ShouldBe(2);
        prefixed.Versions.ShouldHaveSingleItem().Body.ShouldBe(HeadingBody);
    }

    [Fact]
    public void PatchHeadingSection_KeepsTheHeadingLineByteIdentical_OnReplace()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, HeadingBody);

        artifact.PatchHeadingSection("Scope", "replaced", remove: false, reason: null);

        artifact.Body.ShouldStartWith("# Spec\n\n## Scope\nreplaced\n");
        artifact.Body.ShouldNotContain("### Deep");
    }

    [Fact]
    public void PatchHeadingSection_AbsentHeading_WithContent_InsertsAtTheBodyEnd()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, HeadingBody);

        artifact.PatchHeadingSection("## Added", "fresh content", remove: false, reason: null);

        artifact.Body.ShouldEndWith("## Added\nfresh content\n");
        artifact.Version.ShouldBe(2);
    }

    [Fact]
    public void PatchHeadingSection_AbsentHeading_WithoutContent_InsertsDeclaredEmpty()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, HeadingBody);

        artifact.PatchHeadingSection("Added", null, remove: false, reason: null);

        artifact.Body.ShouldEndWith("## Added\n" + PlanningSections.EmptyMarker + "\n");
    }

    [Fact]
    public void PatchHeadingSection_ExistingHeading_WithoutContent_ThrowsContentRequired()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, HeadingBody);

        var ex = Should.Throw<PlanningException>(
            () => artifact.PatchHeadingSection("Scope", null, remove: false, reason: null));

        ex.Code.ShouldBe(PlanningFailureCode.SectionContentRequiredForReplace);
        artifact.Body.ShouldBe(HeadingBody);
        artifact.Version.ShouldBe(1);
    }

    [Fact]
    public void PatchHeadingSection_DuplicateHeading_ThrowsListingMatches_AndWritesNothing()
    {
        const string body = "## Scope\na\nmiddle\n## Scope\nb\n## Tail\nend";
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, body);

        var ex = Should.Throw<PlanningException>(
            () => artifact.PatchHeadingSection("## Scope", "c", remove: false, reason: null));

        ex.Code.ShouldBe(PlanningFailureCode.PatchHeadingAmbiguous);
        ex.Message.ShouldContain("2 H2 headings");
        ex.Message.ShouldContain("'## Scope', '## Scope'");

        // 266/T3 row 4 — the frequency leader's wording upgrade: the count alone sent people hunting
        // for duplicate headings that were never there. The rule the throw actually enforces is that
        // content is the block under the heading and the heading line itself is byte-stable.
        ex.Message.ShouldContain("the heading line itself stays byte-stable");
        ex.Message.ShouldContain("resend with that line stripped from the content");
        ex.Message.ShouldContain("merge or rename the duplicates");

        artifact.Body.ShouldBe(body);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void PatchHeadingSection_OnTask_ThrowsNamingTheXmlVocabulary()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, PatchBody);

        var ex = Should.Throw<PlanningException>(
            () => artifact.PatchHeadingSection("## Goal", "content", remove: false, reason: null));

        ex.Code.ShouldBe(PlanningFailureCode.PatchSectionNotFound);
        ex.Message.ShouldContain("Heading patches apply");
        ex.Message.ShouldContain(string.Join(", ", PlanningTaskSections.Declared));
        artifact.Version.ShouldBe(1);
    }

    [Fact]
    public void PatchHeadingSection_MissingHeading_OnRemove_ThrowsListingPresentHeadings()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, HeadingBody);

        var ex = Should.Throw<PlanningException>(
            () => artifact.PatchHeadingSection("Absent", null, remove: true, reason: "gone"));

        ex.Code.ShouldBe(PlanningFailureCode.PatchHeadingNotFound);
        ex.Message.ShouldContain("'## Scope'");
        artifact.Body.ShouldBe(HeadingBody);
    }

    [Fact]
    public void PatchHeadingSection_Remove_WritesTheTombstoneMarkerOverTheWholeBlock()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, HeadingBody);

        artifact.PatchHeadingSection("Scope", null, remove: true, reason: "scope cut in review");

        Regex.IsMatch(
                artifact.Body,
                @"^# Spec\n\n<removed \d{4}-\d{2}-\d{2} — scope cut in review>\n## Next\nlater$")
            .ShouldBeTrue();
        artifact.Version.ShouldBe(2);
        artifact.Versions.ShouldHaveSingleItem().Body.ShouldBe(HeadingBody);
    }

    [Fact]
    public void PatchHeadingSection_RemoveWithoutReason_Throws_AndLeavesArtifactUntouched()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, HeadingBody);

        var ex = Should.Throw<PlanningException>(
            () => artifact.PatchHeadingSection("Scope", null, remove: true, reason: null));

        ex.Code.ShouldBe(PlanningFailureCode.PatchRemoveReasonRequired);
        artifact.Body.ShouldBe(HeadingBody);
        artifact.Version.ShouldBe(1);
    }

    [Fact]
    public void PatchHeadingSection_BlockCarryingTheLogSection_Throws()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, HeadingOverLogBody);

        var replace = Should.Throw<PlanningException>(
            () => artifact.PatchHeadingSection("Log", "rewritten", remove: false, reason: null));
        var tombstone = Should.Throw<PlanningException>(
            () => artifact.PatchHeadingSection("Log", null, remove: true, reason: "gone"));

        replace.Code.ShouldBe(PlanningFailureCode.PatchLogSectionNotReplaceable);
        tombstone.Code.ShouldBe(PlanningFailureCode.PatchLogSectionNotReplaceable);
        artifact.Body.ShouldBe(HeadingOverLogBody);
    }

    [Fact]
    public void PatchHeadingSection_FrozenWithoutNote_Throws_AndLeavesArtifactUntouched()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, HeadingBody);
        artifact.Validate();
        var frozenBody = artifact.Body;

        Should.Throw<InvalidOperationException>(
            () => artifact.PatchHeadingSection("Scope", "no note here", remove: false, reason: null));

        artifact.Status.ShouldBe(PlanningArtifactStatus.Validated);
        artifact.Body.ShouldBe(frozenBody);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void PatchHeadingSection_FrozenWithNote_DemotesToDraft_AndAppendsTheAutoLine()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, HeadingBody);
        artifact.Validate();

        artifact.PatchHeadingSection("Scope", "rework after review", remove: false, reason: null, "spec reopened");

        artifact.Status.ShouldBe(PlanningArtifactStatus.Draft);
        artifact.FrozenAt.ShouldBeNull();
        artifact.StatusChanged.ShouldBe(PlanningArtifact.DemotedStatusEcho);
        artifact.Version.ShouldBe(2);
        artifact.Versions.ShouldHaveSingleItem().Body.ShouldBe(HeadingBody);
        var lines = PlanningLog.Parse(artifact.Body);
        lines.ShouldContain(line => line.Text == "spec reopened");
        lines.Last().Text.ShouldBe(PlanningArtifact.DemotionLogNote);
    }

    [Fact]
    public void PatchHeadingSection_IdenticalContent_DoesNotBumpTheContentAnchor()
    {
        const string body = "## Scope\nline one\nline two\n## Next\nlater";
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, body);

        artifact.PatchHeadingSection("Scope", "line one\nline two", remove: false, reason: null);
        var anchorAfterFirst = artifact.ContentUpdatedAt;

        artifact.PatchHeadingSection("Scope", "line one\nline two", remove: false, reason: null);

        artifact.Version.ShouldBe(3);
        artifact.ContentUpdatedAt.ShouldBe(anchorAfterFirst);
        artifact.StatusChanged.ShouldBeNull();
    }

    [Fact]
    public void PatchHeadingSection_LfContent_OnCrlfBody_IsANoOpWithoutBumpingTheContentAnchor()
    {
        const string body = "## Scope\r\nline one\r\nline two\r\n## Next\r\nlater";
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, body);

        artifact.PatchHeadingSection("Scope", "line one\nline two", remove: false, reason: null);

        artifact.Body.ShouldBe(body);
        artifact.Version.ShouldBe(2);
        artifact.ContentUpdatedAt.ShouldBe(artifact.CreatedAt);
        artifact.StatusChanged.ShouldBeNull();
    }

    [Fact]
    public void PatchHeadingSection_BlankSectionName_Throws()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, HeadingBody);

        Should.Throw<ArgumentException>(
            () => artifact.PatchHeadingSection("  ", "content", remove: false, reason: null));
    }

    [Fact]
    public void AmendResult_AppendsDatedBlock_LeavesExistingTextUntouched()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Result, ResultBody);

        artifact.AmendResult("total corrected to 42h", "human pre-approved the correction");

        var prose = ResultBody[..ResultBody.IndexOf("<log>", StringComparison.Ordinal)];
        artifact.Body.ShouldStartWith(prose);
        artifact.Body.ShouldContain($"## {PlanningArtifact.AmendmentMarker} ");
        artifact.Body.TrimEnd().ShouldEndWith("total corrected to 42h");
        var log = PlanningLog.Parse(artifact.Body);
        log.First().Text.ShouldBe("seeded");
        log.ShouldContain(line => line.Text == "human pre-approved the correction");
        artifact.Version.ShouldBe(2);
        artifact.Versions.ShouldHaveSingleItem().Body.ShouldBe(ResultBody);
    }

    [Fact]
    public void AmendResult_TrailingNewlineBody_AppendsWithExactlyOneBlankLine()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Result, ResultBody + "\n");

        artifact.AmendResult("corrected", "note");

        artifact.Body.ShouldContain("</log>\n\n## <amendment> ");
    }

    [Fact]
    public void AmendResult_ValidatedResult_StaysValidated_RecordsApprovalLogLine()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Result, ResultBody);
        artifact.Validate("human tapped the result");
        var frozenAt = artifact.FrozenAt;
        var validatedBody = artifact.Body;

        artifact.AmendResult("corrected the total to 42h", "human pre-approved via notification reply");

        artifact.Status.ShouldBe(PlanningArtifactStatus.Validated);
        artifact.FrozenAt.ShouldBe(frozenAt);
        artifact.StatusChanged.ShouldBeNull();
        artifact.FeatureStatusChanged.ShouldBeNull();
        artifact.ValidationProofVoided.ShouldBeNull();
        artifact.Version.ShouldBe(2);
        artifact.Versions.ShouldHaveSingleItem().Body.ShouldBe(validatedBody);
        PlanningLog.Parse(artifact.Body).Last().Text.ShouldBe("human pre-approved via notification reply");
    }

    [Fact]
    public void AmendResult_DraftResult_WorksTheSame()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Result, ResultBody);

        artifact.AmendResult("draft-stage addendum", "mid-session note");

        artifact.Status.ShouldBe(PlanningArtifactStatus.Draft);
        artifact.Body.ShouldContain("draft-stage addendum");
        PlanningLog.Parse(artifact.Body).Last().Text.ShouldBe("mid-session note");
    }

    [Theory]
    [InlineData(PlanningArtifactKind.Spec)]
    [InlineData(PlanningArtifactKind.Plan)]
    [InlineData(PlanningArtifactKind.Task)]
    public void AmendResult_NonResultKind_ThrowsNamingTheResultScope(PlanningArtifactKind kind)
    {
        var artifact = PlanningArtifact.Create(FeatureId, kind, PatchBody);

        var ex = Should.Throw<PlanningException>(() => artifact.AmendResult("text", "note"));

        ex.Code.ShouldBe(PlanningFailureCode.AmendmentNotAllowedForKind);
        ex.Message.ShouldContain("result");
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void AmendResult_RepeatedCalls_AccumulateBlocks()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Result, ResultBody);

        artifact.AmendResult("first correction", "first note");
        artifact.AmendResult("second correction\nSupersedes: the first correction", "second note");

        artifact.Body.Split('\n')
            .Count(line => line.StartsWith($"## {PlanningArtifact.AmendmentMarker} ", StringComparison.Ordinal))
            .ShouldBe(2);
        artifact.Body.IndexOf("second correction", StringComparison.Ordinal)
            .ShouldBeGreaterThan(artifact.Body.IndexOf("first correction", StringComparison.Ordinal));
        artifact.Version.ShouldBe(3);
    }

    [Theory]
    [InlineData("", "note")]
    [InlineData("  ", "note")]
    [InlineData("text", "")]
    [InlineData("text", "  ")]
    public void AmendResult_BlankTextOrNote_Throws(string amendmentText, string logNote)
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Result, ResultBody);

        Should.Throw<ArgumentException>(() => artifact.AmendResult(amendmentText, logNote));

        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void AmendResult_TextCarryingALogBlock_ThrowsAndLeavesArtifactUntouched()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Result, ResultBody);

        var ex = Should.Throw<PlanningException>(() =>
            artifact.AmendResult("see below\n<log>\n- 2026-10-01 — smuggled\n</log>", "note"));

        ex.Code.ShouldBe(PlanningFailureCode.LogBlocksDuplicated);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
        artifact.Body.ShouldBe(ResultBody);
    }

    [Fact]
    public void AmendResult_AnchoredSectionPairInText_ThrowsAndLeavesArtifactUntouched()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Result, ResultBody);

        var ex = Should.Throw<PlanningException>(() =>
            artifact.AmendResult("see the shape below\n<goal>\nquoted goal\n</goal>", "note"));

        ex.Code.ShouldBe(PlanningFailureCode.AmendmentSectionPairRefused);
        ex.Message.ShouldContain("<goal>");
        ex.Message.ShouldContain("&lt;goal&gt;");
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
        artifact.Body.ShouldBe(ResultBody);
    }

    [Fact]
    public void AmendResult_InlineSectionOneLinerInText_ThrowsTheSameRefusal()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Result, ResultBody);

        var ex = Should.Throw<PlanningException>(() =>
            artifact.AmendResult("shape:\n<goal>quoted</goal>", "note"));

        ex.Code.ShouldBe(PlanningFailureCode.AmendmentSectionPairRefused);
        artifact.Version.ShouldBe(1);
        artifact.Body.ShouldBe(ResultBody);
    }

    [Fact]
    public void AmendResult_FencedSectionPairInText_IsRefusedToo()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Result, ResultBody);

        var ex = Should.Throw<PlanningException>(() =>
            artifact.AmendResult("```xml\n<goal>\nquoted\n</goal>\n```", "note"));

        ex.Code.ShouldBe(PlanningFailureCode.AmendmentSectionPairRefused);
        artifact.Version.ShouldBe(1);
        artifact.Body.ShouldBe(ResultBody);
    }

    [Fact]
    public void AmendResult_EscapedSectionQuote_IsAccepted_AndNeverShadowsTheRealSection()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Result, ResultBody);

        artifact.AmendResult("quote: &lt;goal&gt;quoted&lt;/goal&gt; stays prose", "note");

        artifact.Version.ShouldBe(2);
        artifact.Body.ShouldContain("&lt;goal&gt;quoted&lt;/goal&gt;");
        PlanningSections.Find("goal", artifact.Body)!
            .Content.ShouldBe("close out the feature");
    }

    [Fact]
    public void TickAc_ByIndex_FlipsMarker_SnapshotsPreviousBody_AndBumpsVersion()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);
        var original = artifact.Body;

        var ticked = artifact.TickAc(1, null);

        ticked.Changed.ShouldBeTrue();
        ticked.Index.ShouldBe(1);
        ticked.Text.ShouldBe("write the parser");
        artifact.Body.ShouldContain("[x] write the parser");
        artifact.Body.ShouldNotContain("[ ] write the parser");
        artifact.Body.ShouldContain("[ ] indented criterion with trailing note");
        artifact.Body.ShouldContain("[x] ship the docs");
        artifact.Body.ShouldContain("- plain bullet");
        artifact.Version.ShouldBe(2);
        artifact.Versions.ShouldHaveSingleItem().Body.ShouldBe(original);
    }

    [Fact]
    public void TickAc_ByIndex_AmongDuplicateTexts_FlipsOnlyTheTargetedLine()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, AmbiguousAcBody);

        artifact.TickAc(1, null);

        artifact.Body.ShouldContain("[x] deploy the service");
        artifact.Body.ShouldContain("[ ] deploy the service");
    }

    [Fact]
    public void TickAc_IndentedItem_PreservesIndentationAndSuffix()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        artifact.TickAc(2, null);

        artifact.Body.ShouldContain("\n  - [x] indented criterion with trailing note\n");
    }

    [Fact]
    public void TickAc_ByText_ExactMatch_TicksSingleItem()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        var ticked = artifact.TickAc(null, "write the parser");

        ticked.Changed.ShouldBeTrue();
        ticked.Index.ShouldBe(1);
        ticked.Text.ShouldBe("write the parser");
        artifact.Body.ShouldContain("[x] write the parser");
        artifact.Version.ShouldBe(2);
    }

    [Fact]
    public void TickAc_ByText_MatchesCaseSensitively()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        var ex = Should.Throw<PlanningException>(() => artifact.TickAc(null, "Write the parser"));

        ex.Code.ShouldBe(PlanningFailureCode.TickItemNotFound);
    }

    [Fact]
    public void TickAc_ByText_ReadableAcText_MatchesStoredEscapedItem()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, EscapedAcBody);

        var ticked = artifact.TickAc(null, "<key> is required");

        ticked.Changed.ShouldBeTrue();
        ticked.Index.ShouldBe(1);
        ticked.Text.ShouldBe("&lt;key&gt; is required");
        artifact.Body.ShouldContain("[x] &lt;key&gt; is required");
        artifact.Version.ShouldBe(2);
    }

    [Fact]
    public void TickAc_ByText_EscapedAcText_MatchesStoredReadableItem()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, ReadableAcBody);

        var ticked = artifact.TickAc(null, "&lt;key&gt; is required");

        ticked.Changed.ShouldBeTrue();
        ticked.Index.ShouldBe(1);
        ticked.Text.ShouldBe("<key> is required");
        artifact.Body.ShouldContain("[x] <key> is required");
        artifact.Version.ShouldBe(2);
    }

    [Fact]
    public void TickAc_ByText_EscapedAcText_MatchesStoredEscapedItem_ThroughTheSameNormalization()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, EscapedAcBody);

        artifact.TickAc(null, "&lt;key&gt; is required");

        artifact.Body.ShouldContain("[x] &lt;key&gt; is required");
        artifact.Version.ShouldBe(2);
    }

    [Fact]
    public void TickAc_ByText_NormalizedAmbiguity_ThrowsListingMatchingIndices()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, MixedEscapedAcBody);

        var ex = Should.Throw<PlanningException>(() => artifact.TickAc(null, "<alpha> step"));

        ex.Code.ShouldBe(PlanningFailureCode.TickTextAmbiguous);
        ex.Message.ShouldContain("<alpha> step");
        ex.Message.ShouldContain("indices 1, 2");
    }

    [Fact]
    public void TickAc_ByText_DoubleEscapedAcText_MissesWithVisibleItems()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, EscapedAcBody);

        var ex = Should.Throw<PlanningException>(
            () => artifact.TickAc(null, "&amp;lt;key&amp;gt; is required"));

        ex.Code.ShouldBe(PlanningFailureCode.TickItemNotFound);
        ex.Message.ShouldContain("Visible items");
        ex.Message.ShouldContain("1: [ ] &lt;key&gt; is required");
    }

    [Fact]
    public void UntickAc_ByText_EscapedAcText_ClearsStoredEscapedItem()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, EscapedAcBody);
        artifact.TickAc(null, "<key> is required");

        var unticked = artifact.UntickAc(null, "&lt;key&gt; is required");

        unticked.Changed.ShouldBeTrue();
        unticked.Index.ShouldBe(1);
        artifact.Body.ShouldContain("[ ] &lt;key&gt; is required");
        artifact.Version.ShouldBe(3);
    }

    [Fact]
    public void TickAc_AlreadyTicked_IsNoOp_EchoingResolvedItem()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);
        var original = artifact.Body;
        var updatedAtBefore = artifact.UpdatedAt;

        var ticked = artifact.TickAc(3, null);

        ticked.Changed.ShouldBeFalse();
        ticked.Index.ShouldBe(3);
        ticked.Text.ShouldBe("ship the docs");
        artifact.Body.ShouldBe(original);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
        artifact.UpdatedAt.ShouldBe(updatedAtBefore);
    }

    [Fact]
    public void TickAc_ByText_AlreadyTicked_IsNoOp()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        artifact.TickAc(null, "ship the docs");

        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void TickAc_AlreadyTicked_OnFrozenArtifactWithoutNote_IsNoOp()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);
        artifact.Validate();

        artifact.TickAc(3, null);

        artifact.Status.ShouldBe(PlanningArtifactStatus.Validated);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(0)]
    [InlineData(-1)]
    public void TickAc_IndexOutOfRange_ThrowsListingVisibleItemsOneBased(int index)
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        var ex = Should.Throw<PlanningException>(() => artifact.TickAc(index, null));

        ex.Code.ShouldBe(PlanningFailureCode.TickItemNotFound);
        ex.Message.ShouldContain(index.ToString());
        ex.Message.ShouldContain("write the parser");
        ex.Message.ShouldContain("plain bullet");
        ex.Message.ShouldContain("3: [x] ship the docs");
        ex.Message.ShouldContain("4: plain bullet");
    }

    [Fact]
    public void TickAc_TextWithoutMatch_ThrowsListingVisibleItems()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        var ex = Should.Throw<PlanningException>(() => artifact.TickAc(null, "nonexistent criterion"));

        ex.Code.ShouldBe(PlanningFailureCode.TickItemNotFound);
        ex.Message.ShouldContain("nonexistent criterion");
        ex.Message.ShouldContain("2: [ ] indented criterion with trailing note");
    }

    [Fact]
    public void TickAc_AmbiguousText_ThrowsListingMatchingIndices()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, AmbiguousAcBody);

        var ex = Should.Throw<PlanningException>(() => artifact.TickAc(null, "deploy the service"));

        ex.Code.ShouldBe(PlanningFailureCode.TickTextAmbiguous);
        ex.Message.ShouldContain("deploy the service");
        ex.Message.ShouldContain("indices 1, 2");
    }

    [Fact]
    public void TickAc_ItemWithoutCheckboxMarker_ThrowsNotCheckable()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        var ex = Should.Throw<PlanningException>(() => artifact.TickAc(4, null));

        ex.Code.ShouldBe(PlanningFailureCode.TickItemNotCheckable);
        ex.Message.ShouldContain("plain bullet");
    }

    [Fact]
    public void TickAc_NoAcceptanceCriteriaSection_ThrowsSayingBodyHasNone()
    {
        var artifact = PlanningArtifact.Create(
            FeatureId, PlanningArtifactKind.Spec, "# Spec\n\n<goal>\nno AC here\n</goal>\n");

        var ex = Should.Throw<PlanningException>(() => artifact.TickAc(1, null));

        ex.Code.ShouldBe(PlanningFailureCode.TickItemNotFound);
        ex.Message.ShouldContain("no acceptance-criteria items");
    }

    [Fact]
    public void TickAc_FrozenWithoutNote_Throws_AndLeavesArtifactUntouched()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);
        artifact.Validate();

        Should.Throw<InvalidOperationException>(() => artifact.TickAc(1, null));

        artifact.Status.ShouldBe(PlanningArtifactStatus.Validated);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void TickAc_FrozenWithNote_DemotesToDraft_SnapshotsFrozenBody_AppendsLog()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);
        artifact.Validate();
        var frozenBody = artifact.Body;

        artifact.TickAc(1, null, "tick after reopening");

        artifact.Status.ShouldBe(PlanningArtifactStatus.Draft);
        artifact.FrozenAt.ShouldBeNull();
        artifact.Version.ShouldBe(2);
        artifact.Versions.ShouldHaveSingleItem().Body.ShouldBe(frozenBody);
        artifact.Body.ShouldContain("[x] write the parser");
        artifact.Body.ShouldContain("tick after reopening");
    }

    [Fact]
    public void TickAc_CrlfBody_FlipsBytePredictably()
    {
        var body = "# Task\r\n\r\n<acceptance-criteria>\r\n- [ ] first item\r\n- [x] done item\r\n</acceptance-criteria>\r\n";
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, body);

        artifact.TickAc(1, null);

        artifact.Body.ShouldBe("# Task\r\n\r\n<acceptance-criteria>\r\n- [x] first item\r\n- [x] done item\r\n</acceptance-criteria>\r\n");
    }

    [Fact]
    public void TickAc_IndexAndTextTogether_Throws()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        Should.Throw<ArgumentException>(() => artifact.TickAc(1, "write the parser"));
    }

    [Fact]
    public void TickAc_NeitherIndexNorText_Throws()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        Should.Throw<ArgumentException>(() => artifact.TickAc(null, null));
    }

    [Fact]
    public void TickAc_WithNote_OnDraftArtifact_AppendsLogLine()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, PatchBody);

        artifact.TickAc(1, null, "criterion met");

        artifact.Version.ShouldBe(2);
        artifact.Body.ShouldContain("criterion met");
        artifact.Status.ShouldBe(PlanningArtifactStatus.Todo);
    }

    [Fact]
    public void UntickAc_ByIndex_ClearsMarker_SnapshotsPreviousBody()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);
        artifact.TickAc(1, null);
        var tickedBody = artifact.Body;

        var unticked = artifact.UntickAc(1, null);

        unticked.Changed.ShouldBeTrue();
        unticked.Index.ShouldBe(1);
        unticked.Text.ShouldBe("write the parser");
        artifact.Body.ShouldContain("[ ] write the parser");
        artifact.Body.ShouldContain("[x] ship the docs");
        artifact.Version.ShouldBe(3);
        artifact.Versions.Count.ShouldBe(2);
        artifact.Versions.Last().Body.ShouldBe(tickedBody);
    }

    [Fact]
    public void UntickAc_ByIndex_AmongDuplicateTexts_FlipsOnlyTheTargetedLine()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, AmbiguousAcBody);

        artifact.TickAc(2, null);
        artifact.UntickAc(2, null);

        artifact.Body.ShouldNotContain("[x]");
    }

    [Fact]
    public void UntickAc_ByText_ClearsTickedItem()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        var unticked = artifact.UntickAc(null, "ship the docs");

        unticked.Changed.ShouldBeTrue();
        unticked.Index.ShouldBe(3);
        unticked.Text.ShouldBe("ship the docs");
        artifact.Body.ShouldContain("[ ] ship the docs");
        artifact.Version.ShouldBe(2);
    }

    [Fact]
    public void UntickAc_AlreadyUnticked_IsNoOp_EchoingResolvedItem()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);
        var original = artifact.Body;
        var updatedAtBefore = artifact.UpdatedAt;

        var unticked = artifact.UntickAc(1, null);

        unticked.Changed.ShouldBeFalse();
        unticked.Index.ShouldBe(1);
        unticked.Text.ShouldBe("write the parser");
        artifact.Body.ShouldBe(original);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
        artifact.UpdatedAt.ShouldBe(updatedAtBefore);
    }

    [Fact]
    public void UntickAc_AlreadyUnticked_OnFrozenArtifactWithoutNote_IsNoOp()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);
        artifact.Validate();

        artifact.UntickAc(1, null);

        artifact.Status.ShouldBe(PlanningArtifactStatus.Validated);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void UntickAc_UpperCaseMarker_ClearsToEmptyBox()
    {
        var body = "<acceptance-criteria>\n- [X] done in a hurry\n</acceptance-criteria>\n";
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, body);

        var unticked = artifact.UntickAc(1, null);

        unticked.Changed.ShouldBeTrue();
        artifact.Body.ShouldContain("[ ] done in a hurry");
        artifact.Body.ShouldNotContain("[X]");
    }

    [Fact]
    public void UntickAc_UpperCaseMarker_WithLowercaseBoxInTheTail_ClearsOnlyTheMarker()
    {
        var body = "<acceptance-criteria>\n- [X] todo [x] already covered by task 03\n</acceptance-criteria>\n";
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, body);

        var unticked = artifact.UntickAc(1, null);

        unticked.Changed.ShouldBeTrue();
        artifact.Body.ShouldContain("- [ ] todo [x] already covered by task 03");
        artifact.Body.ShouldNotContain("- [x]");
    }

    [Fact]
    public void UntickAc_ItemWithoutCheckboxMarker_ThrowsNotCheckable()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        var ex = Should.Throw<PlanningException>(() => artifact.UntickAc(4, null));

        ex.Code.ShouldBe(PlanningFailureCode.TickItemNotCheckable);
        ex.Message.ShouldContain("plain bullet");
    }

    [Fact]
    public void UntickAc_IndexOutOfRange_ThrowsListingVisibleItemsOneBased()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        var ex = Should.Throw<PlanningException>(() => artifact.UntickAc(9, null));

        ex.Code.ShouldBe(PlanningFailureCode.TickItemNotFound);
        ex.Message.ShouldContain("9");
        ex.Message.ShouldContain("1: [ ] write the parser");
        ex.Message.ShouldContain("4: plain bullet");
    }

    [Fact]
    public void UntickAc_FrozenWithoutNote_Throws_AndLeavesArtifactUntouched()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);
        artifact.Validate();

        Should.Throw<InvalidOperationException>(() => artifact.UntickAc(3, null));

        artifact.Status.ShouldBe(PlanningArtifactStatus.Validated);
        artifact.Version.ShouldBe(1);
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void UntickAc_IndexAndTextTogether_Throws()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        Should.Throw<ArgumentException>(() => artifact.UntickAc(1, "ship the docs"));
    }

    [Fact]
    public void UntickAc_NeitherIndexNorText_Throws()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);

        Should.Throw<ArgumentException>(() => artifact.UntickAc(null, null));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AppendLog_EmptyNote_Throws(string note)
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body");

        Should.Throw<ArgumentException>(() => artifact.AppendLog(note));
    }

    [Fact]
    public void Restore_RoundTripsAllFields()
    {
        var featureId = Guid.NewGuid();
        var frozenAt = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        var artifact = PlanningArtifact.Restore(
            featureId,
            PlanningArtifactKind.Spec,
            PlanningArtifactStatus.Validated,
            PlanningReviewLevel.Hard,
            "imported body",
            order: 3,
            frozenAt: frozenAt,
            version: 5,
            createdAt: createdAt,
            updatedAt: updatedAt);

        artifact.FeatureId.ShouldBe(featureId);
        artifact.Kind.ShouldBe(PlanningArtifactKind.Spec);
        artifact.Status.ShouldBe(PlanningArtifactStatus.Validated);
        artifact.Review.ShouldBe(PlanningReviewLevel.Hard);
        artifact.Body.ShouldBe("imported body");
        artifact.Order.ShouldBe(3);
        artifact.FrozenAt.ShouldBe(frozenAt);
        artifact.Version.ShouldBe(5);
        artifact.CreatedAt.ShouldBe(createdAt);
        artifact.UpdatedAt.ShouldBe(updatedAt);
        artifact.IsFrozen.ShouldBeTrue();
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void Restore_WithPreservedFrontmatter_SetsPreservedFrontmatter()
    {
        var artifact = PlanningArtifact.Restore(
            Guid.NewGuid(),
            PlanningArtifactKind.Task,
            PlanningArtifactStatus.Todo,
            PlanningReviewLevel.Light,
            "body",
            1,
            null,
            1,
            DateTime.UtcNow,
            DateTime.UtcNow,
            preservedFrontmatter: "{\"depends_on\":\"02-other\"}");

        artifact.PreservedFrontmatter.ShouldBe("{\"depends_on\":\"02-other\"}");
    }

    [Fact]
    public void Restore_WithoutPreservedFrontmatter_DefaultsToNull()
    {
        var artifact = PlanningArtifact.Restore(
            Guid.NewGuid(),
            PlanningArtifactKind.Task,
            PlanningArtifactStatus.Todo,
            PlanningReviewLevel.Light,
            "body",
            1,
            null,
            1,
            DateTime.UtcNow,
            DateTime.UtcNow);

        artifact.PreservedFrontmatter.ShouldBeNull();
    }

    [Fact]
    public void SetPreservedFrontmatter_ReplacesValueAndClearsWithNull()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body content");

        artifact.SetPreservedFrontmatter("{\"risk\":\"low\"}");
        artifact.PreservedFrontmatter.ShouldBe("{\"risk\":\"low\"}");

        artifact.SetPreservedFrontmatter(null);
        artifact.PreservedFrontmatter.ShouldBeNull();
    }

    [Fact]
    public void SetBrief_AtCap_IsAccepted()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body content");
        var brief = new string('x', PlanningArtifact.BriefMaxLength);

        artifact.SetBrief(brief);

        artifact.Brief.ShouldBe(brief);
    }

    [Fact]
    public void SetBrief_GraceBandStoresWhole_OverCeilingThrowsInvalidBrief()
    {
        // 266: BriefMaxLength is the soft cap enforced at the doors; the entity keeps the
        // grace ceiling as the storage backstop, so band values are legal here.
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body content");

        artifact.SetBrief(new string('x', PlanningArtifact.BriefMaxLength + 1));
        artifact.Brief!.Length.ShouldBe(PlanningArtifact.BriefMaxLength + 1);

        var ex = Should.Throw<PlanningException>(
            () => artifact.SetBrief(new string('x', SoftCap.Ceiling(PlanningArtifact.BriefMaxLength) + 1)));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidBrief);
        artifact.Brief!.Length.ShouldBe(PlanningArtifact.BriefMaxLength + 1);
    }

    [Fact]
    public void SetBrief_Multiline_ThrowsInvalidBrief()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body content");

        Should.Throw<PlanningException>(() => artifact.SetBrief("first line\nsecond line"))
            .Code.ShouldBe(PlanningFailureCode.InvalidBrief);
    }

    [Fact]
    public void SetBrief_BlankText_ClearsTheBrief()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body content");
        artifact.SetBrief("real brief");

        artifact.SetBrief(string.Empty);
        artifact.Brief.ShouldBeNull();

        artifact.SetBrief("real brief");
        artifact.SetBrief("   ");
        artifact.Brief.ShouldBeNull();
    }

    [Fact]
    public void Create_WithMultilineBrief_ThrowsInvalidBrief()
    {
        Should.Throw<PlanningException>(() => PlanningArtifact.Create(
                FeatureId, PlanningArtifactKind.Task, "body content", brief: "one\ntwo"))
            .Code.ShouldBe(PlanningFailureCode.InvalidBrief);
    }

    [Fact]
    public void Create_InitializesContentUpdatedAtToCreationTime()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "body content");

        artifact.ContentUpdatedAt.ShouldBeGreaterThanOrEqualTo(artifact.CreatedAt);
        artifact.ContentUpdatedAt.ShouldBe(artifact.UpdatedAt);
    }

    [Fact]
    public void Restore_InitializesContentUpdatedAtToUpdatedAt()
    {
        var updatedAt = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        var artifact = PlanningArtifact.Restore(
            Guid.NewGuid(),
            PlanningArtifactKind.Spec,
            PlanningArtifactStatus.Draft,
            PlanningReviewLevel.Light,
            "body",
            0,
            null,
            1,
            DateTime.UtcNow,
            updatedAt);

        artifact.ContentUpdatedAt.ShouldBe(updatedAt);
    }

    [Fact]
    public void AppendLog_LeavesContentUpdatedAtUntouched()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "body content");
        var contentUpdatedAtBefore = artifact.ContentUpdatedAt;

        artifact.AppendLog("service line after the tap");

        artifact.UpdatedAt.ShouldBeGreaterThanOrEqualTo(contentUpdatedAtBefore);
        artifact.ContentUpdatedAt.ShouldBe(contentUpdatedAtBefore);
    }

    [Fact]
    public void EditNewestLog_LeavesContentUpdatedAtUntouched()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "body content");
        artifact.AppendLog("agent line", PlanningLogSource.Agent);
        var contentUpdatedAtBefore = artifact.ContentUpdatedAt;

        artifact.EditNewestLog("agent line reworded", PlanningLogSource.Agent);

        artifact.ContentUpdatedAt.ShouldBe(contentUpdatedAtBefore);
    }

    [Fact]
    public void SetStatus_WithLogLine_LeavesContentUpdatedAtUntouched()
    {
        var artifact = PlanArtifact(PlanningArtifactKind.Task, PlanningArtifactStatus.Todo);
        var contentUpdatedAtBefore = artifact.ContentUpdatedAt;

        artifact.SetStatus(PlanningArtifactStatus.InProgress, "taking the task", PlanningLogSource.Agent);

        artifact.Status.ShouldBe(PlanningArtifactStatus.InProgress);
        artifact.ContentUpdatedAt.ShouldBe(contentUpdatedAtBefore);
    }

    [Fact]
    public void Validate_WithProofLine_LeavesContentUpdatedAtUntouched()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "body content");
        var contentUpdatedAtBefore = artifact.ContentUpdatedAt;

        artifact.Validate("validated with human proof: notification x replied y");

        artifact.Status.ShouldBe(PlanningArtifactStatus.Validated);
        artifact.ContentUpdatedAt.ShouldBe(contentUpdatedAtBefore);
    }

    [Fact]
    public void UpdateBody_WithBodyChange_BumpsContentUpdatedAt()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "spec v1");
        var contentUpdatedAtBefore = artifact.ContentUpdatedAt;

        artifact.UpdateBody("spec v2", "reworked after review");

        artifact.ContentUpdatedAt.ShouldBeGreaterThan(contentUpdatedAtBefore);
        artifact.UpdatedAt.ShouldBeGreaterThanOrEqualTo(artifact.ContentUpdatedAt);
    }

    [Fact]
    public void UpdateBody_LogNoteOnly_LeavesContentUpdatedAtUntouched()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "spec v1");
        var contentUpdatedAtBefore = artifact.ContentUpdatedAt;

        artifact.UpdateBody("spec v1", "service note, no content change");

        artifact.Body.ShouldContain("service note, no content change");
        artifact.ContentUpdatedAt.ShouldBe(contentUpdatedAtBefore);
    }

    [Fact]
    public void PatchSection_BumpsContentUpdatedAt()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Plan, PatchBody);
        var contentUpdatedAtBefore = artifact.ContentUpdatedAt;

        artifact.PatchSection("goal", "replaced goal", "scope tweak");

        artifact.ContentUpdatedAt.ShouldBeGreaterThan(contentUpdatedAtBefore);
    }

    [Fact]
    public void TickAc_BumpsContentUpdatedAt()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, PatchBody);
        var contentUpdatedAtBefore = artifact.ContentUpdatedAt;

        artifact.TickAc(1, null);

        artifact.ContentUpdatedAt.ShouldBeGreaterThan(contentUpdatedAtBefore);
    }

    [Fact]
    public void SetBrief_WithChange_BumpsContentUpdatedAt()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body content");
        var contentUpdatedAtBefore = artifact.ContentUpdatedAt;

        artifact.SetBrief("new brief");

        artifact.ContentUpdatedAt.ShouldBeGreaterThan(contentUpdatedAtBefore);
    }

    [Fact]
    public void SetBrief_WithSameValue_LeavesContentUpdatedAtUntouched()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body content");
        artifact.SetBrief("same brief");
        var contentUpdatedAtBefore = artifact.ContentUpdatedAt;

        artifact.SetBrief("same brief");

        artifact.ContentUpdatedAt.ShouldBe(contentUpdatedAtBefore);
    }

    [Fact]
    public void AmendResult_BumpsContentUpdatedAt()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Result, ResultBody);
        var contentUpdatedAtBefore = artifact.ContentUpdatedAt;

        artifact.AmendResult("corrected", "human pre-approved the correction");

        artifact.ContentUpdatedAt.ShouldBeGreaterThan(contentUpdatedAtBefore);
    }

    [Fact]
    public void Restore_InvalidStatus_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            PlanningArtifact.Restore(
                Guid.NewGuid(),
                PlanningArtifactKind.Spec,
                PlanningArtifactStatus.Done,
                PlanningReviewLevel.Light,
                "body",
                0,
                null,
                1,
                DateTime.UtcNow,
                DateTime.UtcNow));
    }

    [Fact]
    public void Restore_ValidatedWithoutFrozenAt_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            PlanningArtifact.Restore(
                Guid.NewGuid(),
                PlanningArtifactKind.Spec,
                PlanningArtifactStatus.Validated,
                PlanningReviewLevel.Light,
                "body",
                0,
                frozenAt: null,
                1,
                DateTime.UtcNow,
                DateTime.UtcNow));
    }

    [Fact]
    public void Restore_DraftWithFrozenAt_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            PlanningArtifact.Restore(
                Guid.NewGuid(),
                PlanningArtifactKind.Spec,
                PlanningArtifactStatus.Draft,
                PlanningReviewLevel.Light,
                "body",
                0,
                frozenAt: DateTime.UtcNow,
                1,
                DateTime.UtcNow,
                DateTime.UtcNow));
    }

    [Theory]
    [InlineData(PlanningArtifactKind.Spec, PlanningArtifactStatus.Draft)]
    [InlineData(PlanningArtifactKind.Plan, PlanningArtifactStatus.Draft)]
    [InlineData(PlanningArtifactKind.Result, PlanningArtifactStatus.Draft)]
    [InlineData(PlanningArtifactKind.Task, PlanningArtifactStatus.Todo)]
    [InlineData(PlanningArtifactKind.Task, PlanningArtifactStatus.InProgress)]
    [InlineData(PlanningArtifactKind.Task, PlanningArtifactStatus.Review)]
    [InlineData(PlanningArtifactKind.Task, PlanningArtifactStatus.Blocked)]
    public void Delete_AgentDoor_DraftStatuses_StampMarker(PlanningArtifactKind kind, PlanningArtifactStatus status)
    {
        var artifact = PlanArtifact(kind, status);
        var before = DateTime.UtcNow;

        artifact.Delete(byHuman: false);

        artifact.IsDeleted.ShouldBeTrue();
        artifact.DeletedAt.ShouldNotBeNull();
        artifact.DeletedAt!.Value.ShouldBeGreaterThanOrEqualTo(before);
        artifact.DeletedBy.ShouldBe(PlanningArtifact.AgentDeletionMarker);
    }

    [Theory]
    [InlineData(PlanningArtifactKind.Spec, PlanningArtifactStatus.Validated)]
    [InlineData(PlanningArtifactKind.Plan, PlanningArtifactStatus.Validated)]
    [InlineData(PlanningArtifactKind.Result, PlanningArtifactStatus.Validated)]
    [InlineData(PlanningArtifactKind.Task, PlanningArtifactStatus.Done)]
    public void Delete_AgentDoor_ValidatedOrDone_ThrowsNamingTheReason(
        PlanningArtifactKind kind, PlanningArtifactStatus status)
    {
        var artifact = PlanArtifact(kind, status);

        var ex = Should.Throw<PlanningException>(() => artifact.Delete(byHuman: false));

        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Code.ShouldBe(PlanningFailureCode.ArtifactDeletionRefused);
        ex.Message.ShouldContain("human door");
        artifact.IsDeleted.ShouldBeFalse();
    }

    [Theory]
    [InlineData(PlanningArtifactKind.Spec, PlanningArtifactStatus.Validated)]
    [InlineData(PlanningArtifactKind.Plan, PlanningArtifactStatus.Validated)]
    [InlineData(PlanningArtifactKind.Result, PlanningArtifactStatus.Draft)]
    [InlineData(PlanningArtifactKind.Task, PlanningArtifactStatus.Done)]
    public void Delete_HumanDoor_Unrestricted_StampMarker(PlanningArtifactKind kind, PlanningArtifactStatus status)
    {
        var artifact = PlanArtifact(kind, status);

        artifact.Delete(byHuman: true);

        artifact.IsDeleted.ShouldBeTrue();
        artifact.DeletedBy.ShouldBe(PlanningArtifact.HumanDeletionMarker);
    }

    private static PlanningArtifact PlanArtifact(PlanningArtifactKind kind, PlanningArtifactStatus status)
    {
        var artifact = PlanningArtifact.Create(Guid.NewGuid(), kind, "body content");

        if (status == PlanningArtifactStatus.Validated)
        {
            artifact.Validate();
        }
        else if (status != PlanningArtifactStatus.Draft)
        {
            artifact.SetStatus(status);
        }

        artifact.Status.ShouldBe(status);
        return artifact;
    }
}
