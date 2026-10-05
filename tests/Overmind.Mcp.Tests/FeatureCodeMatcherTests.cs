using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class FeatureCodeMatcherTests
{
    private static FeatureCodeCandidate Named(string kind, params string[] names) =>
        new(kind, names[0], names, "/unused", names[0], null, false);

    [Fact]
    public void Match_SlugSubstring_MatchesWorktreeAndBranch()
    {
        var candidates = new[]
        {
            Named("worktree", "feat/063-worktree-feature-binding"),
            Named("branch", "feat/063-worktree-feature-binding"),
            Named("branch", "main")
        };

        var matches = FeatureCodeMatcher.Match("063-worktree-feature-binding", candidates);

        matches.Count.ShouldBe(2);
        matches.ShouldAllBe(m => m.Name != "main");
    }

    [Fact]
    public void Match_NumberPrefix_MatchesPlainNumberedWorktreeName()
    {
        var candidates = new[] { Named("worktree", "063-worktree-feature-binding") };

        var matches = FeatureCodeMatcher.Match("063-worktree-feature-binding", candidates);

        matches.Count.ShouldBe(1);
    }

    [Fact]
    public void Match_NumberPrefix_MatchesFeatureBranchWithoutSlugSuffix()
    {
        var candidates = new[] { Named("branch", "feat/063-short-rename") };

        var matches = FeatureCodeMatcher.Match("063-worktree-feature-binding", candidates);

        matches.Count.ShouldBe(1);
    }

    [Fact]
    public void Match_NumberInsideLargerRun_IsNotAPrefixMatch()
    {
        var candidates = new[] { Named("branch", "feat/1063-short-rename") };

        FeatureCodeMatcher.Match("063-worktree-feature-binding", candidates).ShouldBeEmpty();
    }

    [Fact]
    public void Match_DigitOnlySlug_MatchesBySubstringOnly()
    {
        var candidates = new[] { Named("branch", "feat/063-short-rename"), Named("branch", "main") };

        var matches = FeatureCodeMatcher.Match("063", candidates);

        matches.Count.ShouldBe(1);
    }

    [Fact]
    public void Match_SlugWithoutNumber_MatchesBySubstringOnly()
    {
        var candidates = new[] { Named("branch", "feat/demo-feature"), Named("branch", "demo-feature-next") };

        var matches = FeatureCodeMatcher.Match("demo-feature", candidates);

        matches.Count.ShouldBe(2);
    }

    [Fact]
    public void Match_NoCandidateMatches_ReturnsEmpty()
    {
        var candidates = new[] { Named("branch", "main"), Named("worktree", "main") };

        FeatureCodeMatcher.Match("063-worktree-feature-binding", candidates).ShouldBeEmpty();
    }
}
