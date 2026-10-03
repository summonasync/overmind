using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public class IssueTests
{
    private static readonly Guid BoardProjectId = Guid.NewGuid();
    private static readonly Guid AuthorProjectId = Guid.NewGuid();

    private static Issue NewIssue() => Issue.Create(BoardProjectId, AuthorProjectId, "Crash on save", "## Repro\nOpen the file.\n\n## Expected\nNo crash.\n\n## Actual\nBoom.");

    [Fact]
    public void Create_SetsDefaults()
    {
        var before = DateTime.UtcNow;

        var issue = Issue.Create(BoardProjectId, AuthorProjectId, "  Crash on save  ", "  Steps inside.  ");

        issue.Id.ShouldNotBe(Guid.Empty);
        issue.ProjectId.ShouldBe(BoardProjectId);
        issue.AuthorProjectId.ShouldBe(AuthorProjectId);
        issue.Title.ShouldBe("Crash on save");
        issue.Body.ShouldBe("Steps inside.");
        issue.Status.ShouldBe(IssueStatus.Open);
        issue.StatusSource.ShouldBe(PlanningLogSource.Agent);
        issue.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
        issue.ClosedAt.ShouldBeNull();
        issue.PromotedToArtifactId.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_BlankTitle_Throws(string? title)
    {
        Should.Throw<ArgumentException>(() =>
            Issue.Create(BoardProjectId, AuthorProjectId, title!, "body"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_BlankBody_Throws(string? body)
    {
        Should.Throw<ArgumentException>(() =>
            Issue.Create(BoardProjectId, AuthorProjectId, "title", body!));
    }

    [Fact]
    public void Append_ReturnsEntryWithProvenanceInOrder()
    {
        var issue = NewIssue();

        var first = issue.Append(AuthorProjectId, "still reproducible");
        var second = issue.Append(Guid.NewGuid(), "worse on nightly");

        first.IssueId.ShouldBe(issue.Id);
        first.AuthorProjectId.ShouldBe(AuthorProjectId);
        first.Text.ShouldBe("still reproducible");
        second.AuthorProjectId.ShouldNotBe(AuthorProjectId);
        second.Text.ShouldBe("worse on nightly");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Append_BlankText_Throws(string? text)
    {
        var issue = NewIssue();

        Should.Throw<ArgumentException>(() => issue.Append(AuthorProjectId, text!));
    }

    [Fact]
    public void SetStatus_ForwardChain_Allowed()
    {
        var issue = NewIssue();
        issue.SetStatus(IssueStatus.Triaged, PlanningLogSource.Agent);
        issue.Status.ShouldBe(IssueStatus.Triaged);
        issue.StatusSource.ShouldBe(PlanningLogSource.Agent);
    }

    [Fact]
    public void SetStatus_ClosedFromAnyNonClosed_SetsClosedAt()
    {
        var open = NewIssue();
        open.SetStatus(IssueStatus.Closed, PlanningLogSource.Ui);
        open.Status.ShouldBe(IssueStatus.Closed);
        open.StatusSource.ShouldBe(PlanningLogSource.Ui);
        open.ClosedAt.ShouldNotBeNull();

        var triaged = NewIssue();
        triaged.SetStatus(IssueStatus.Triaged, PlanningLogSource.Agent);
        triaged.SetStatus(IssueStatus.Closed, PlanningLogSource.Agent);
        triaged.ClosedAt.ShouldNotBeNull();

        var promoted = NewIssue();
        promoted.Promote(Guid.NewGuid(), PlanningLogSource.Agent);
        promoted.SetStatus(IssueStatus.Closed, PlanningLogSource.Agent);
        promoted.ClosedAt.ShouldNotBeNull();
    }

    [Fact]
    public void SetStatus_NoReopen_FromClosed_Throws()
    {
        var issue = NewIssue();
        issue.SetStatus(IssueStatus.Closed, PlanningLogSource.Agent);

        foreach (var target in new[] { IssueStatus.Open, IssueStatus.Triaged, IssueStatus.Promoted })
        {
            var ex = Should.Throw<PlanningException>(() => issue.SetStatus(target, PlanningLogSource.Agent));
            ex.Kind.ShouldBe(PlanningFailureKind.InvalidStatus);
            ex.Code.ShouldBe(PlanningFailureCode.IssueInvalidTransition);
        }
    }

    [Fact]
    public void SetStatus_SameStatus_IsNoOp()
    {
        var issue = NewIssue();
        var createdAt = issue.CreatedAt;

        issue.SetStatus(IssueStatus.Open, PlanningLogSource.Ui);

        issue.Status.ShouldBe(IssueStatus.Open);
        issue.CreatedAt.ShouldBe(createdAt);
    }

    [Fact]
    public void SetStatus_DirectPromotion_Throws()
    {
        var issue = NewIssue();

        var ex = Should.Throw<PlanningException>(
            () => issue.SetStatus(IssueStatus.Promoted, PlanningLogSource.Agent));

        ex.Code.ShouldBe(PlanningFailureCode.IssueInvalidTransition);
        ex.Message.ShouldContain("issues_promote");
    }

    [Fact]
    public void Promote_FromOpenAndTriaged_SetsLinkAndSource()
    {
        var artifactId = Guid.NewGuid();

        var fromOpen = NewIssue();
        fromOpen.Promote(artifactId, PlanningLogSource.Agent);
        fromOpen.Status.ShouldBe(IssueStatus.Promoted);
        fromOpen.PromotedToArtifactId.ShouldBe(artifactId);
        fromOpen.StatusSource.ShouldBe(PlanningLogSource.Agent);

        var fromTriaged = NewIssue();
        fromTriaged.SetStatus(IssueStatus.Triaged, PlanningLogSource.Agent);
        fromTriaged.Promote(artifactId, PlanningLogSource.Ui);
        fromTriaged.Status.ShouldBe(IssueStatus.Promoted);
        fromTriaged.PromotedToArtifactId.ShouldBe(artifactId);
        fromTriaged.StatusSource.ShouldBe(PlanningLogSource.Ui);
    }

    [Theory]
    [InlineData(IssueStatus.Promoted)]
    [InlineData(IssueStatus.Closed)]
    public void Promote_FromPromotedOrClosed_Throws(IssueStatus current)
    {
        var issue = NewIssue();
        if (current == IssueStatus.Promoted)
        {
            issue.Promote(Guid.NewGuid(), PlanningLogSource.Agent);
        }
        else
        {
            issue.SetStatus(IssueStatus.Closed, PlanningLogSource.Agent);
        }

        var ex = Should.Throw<PlanningException>(
            () => issue.Promote(Guid.NewGuid(), PlanningLogSource.Agent));

        ex.Kind.ShouldBe(PlanningFailureKind.InvalidStatus);
        ex.Code.ShouldBe(PlanningFailureCode.IssueInvalidTransition);
    }

    [Fact]
    public void ComposeTaskBody_ComposesAllSections()
    {
        var issue = Issue.Create(BoardProjectId, AuthorProjectId, "Crash on save",
            "## Repro\nOpen the file.\n\n## Expected\nNo crash.\n\n## Actual\nBoom.");
        var appends = new List<IssueAppend>
        {
            issue.Append(AuthorProjectId, "still reproducible on nightly"),
        };

        var body = issue.ComposeTaskBody(appends);

        body.ShouldContain("<goal>");
        body.ShouldContain("Crash on save");
        body.ShouldContain("Open the file.");
        body.ShouldContain("<scope>");
        body.ShouldContain("- Repro: Open the file.");
        body.ShouldContain("- Expected: No crash.");
        body.ShouldContain("- Actual: Boom.");
        body.ShouldContain("<obstacles>");
        body.ShouldContain("still reproducible on nightly");
        body.ShouldContain("<refs>");
        body.ShouldContain($"Issue {issue.Id}");
    }

    [Fact]
    public void ComposeTaskBody_NoTemplateSectionsAndNoAppends_NotesTheirAbsence()
    {
        var issue = Issue.Create(BoardProjectId, AuthorProjectId, "Crash on save", "It just blows up.");

        var body = issue.ComposeTaskBody([]);

        body.ShouldContain("Crash on save");
        body.ShouldContain("It just blows up.");
        body.ShouldContain("no Repro/Expected/Actual sections");
        body.ShouldContain("no follow-ups");
    }

    [Fact]
    public void ComposeTaskBody_PartialSections_MarkMissingOnes()
    {
        var issue = Issue.Create(BoardProjectId, AuthorProjectId, "Crash on save",
            "## Repro\nOpen the file.");

        var body = issue.ComposeTaskBody([]);

        body.ShouldContain("- Repro: Open the file.");
        body.ShouldContain("- Expected: (not provided)");
        body.ShouldContain("- Actual: (not provided)");
    }
}
