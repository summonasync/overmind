using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class GitBranchListParserTests
{
    [Fact]
    public void Parse_TabSeparatedLines_ReadsNameHashAndDate()
    {
        var heads = GitBranchListParser.Parse(
            "main\tf67669f\t2026-10-05T10:15:00+03:00\nfeat/063-worktree-feature-binding\tcceb4fe\t2026-10-04T18:40:00Z\n");

        heads.Count.ShouldBe(2);
        heads[0].Name.ShouldBe("main");
        heads[0].ShortHash.ShouldBe("f67669f");
        heads[0].CommitDate.ShouldBe("2026-10-05T10:15:00+03:00");
        heads[1].Name.ShouldBe("feat/063-worktree-feature-binding");
    }

    [Fact]
    public void Parse_EmptyOutput_ReturnsEmptyList()
    {
        GitBranchListParser.Parse(string.Empty).ShouldBeEmpty();
    }

    [Fact]
    public void Parse_MalformedLine_IsSkipped()
    {
        GitBranchListParser.Parse("main\tf67669f\n").ShouldBeEmpty();
    }
}
