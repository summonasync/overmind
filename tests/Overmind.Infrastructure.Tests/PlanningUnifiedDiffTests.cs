using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningUnifiedDiffTests
{
    [Fact]
    public void Build_IdenticalBodies_ReturnsEmpty()
    {
        PlanningUnifiedDiff.Build("line one\nline two\n", "line one\nline two\n")
            .ShouldBe(string.Empty);
    }

    [Fact]
    public void Build_TrailingNewlineDifference_Ignored()
    {
        PlanningUnifiedDiff.Build("same body\n", "same body")
            .ShouldBe(string.Empty);
    }

    [Fact]
    public void Build_PureInsert_ProducesSingleHunk()
    {
        PlanningUnifiedDiff.Build("a\nc\n", "a\nb\nc\n")
            .ShouldBe("@@ -1,2 +1,3 @@\n a\n+b\n c");
    }

    [Fact]
    public void Build_PureDelete_ProducesSingleHunk()
    {
        PlanningUnifiedDiff.Build("a\nb\nc\n", "a\nc\n")
            .ShouldBe("@@ -1,3 +1,2 @@\n a\n-b\n c");
    }

    [Fact]
    public void Build_OneLineModify_ExactlyOneHunk()
    {
        var diff = PlanningUnifiedDiff.Build("x=1\ny=2\nz=3\n", "x=1\ny=9\nz=3\n");

        diff.ShouldBe("@@ -1,3 +1,3 @@\n x=1\n-y=2\n+y=9\n z=3");
        diff.Split('\n').Count(line => line.StartsWith("@@")).ShouldBe(1);
    }

    [Fact]
    public void Build_DistantChanges_ProducesTwoHunks()
    {
        var from = string.Join("\n", Enumerable.Range(1, 10).Select(i => $"l{i}")) + "\n";
        var to = from
            .Replace("l2\n", "X\n")
            .Replace("l9\n", "Y\n");

        PlanningUnifiedDiff.Build(from, to).ShouldBe(
            "@@ -1,4 +1,4 @@\n l1\n-l2\n+X\n l3\n l4\n" +
            "@@ -7,4 +7,4 @@\n l7\n l8\n-l9\n+Y\n l10");
    }

    [Fact]
    public void Build_EmptyFromBody_UsesZeroCountRange()
    {
        PlanningUnifiedDiff.Build("", "a\nb\n")
            .ShouldBe("@@ -0,0 +1,2 @@\n+a\n+b");
    }

    [Fact]
    public void Build_EmptyToBody_UsesZeroCountRange()
    {
        PlanningUnifiedDiff.Build("a\nb\n", "")
            .ShouldBe("@@ -1,2 +0,0 @@\n-a\n-b");
    }

    [Fact]
    public void Build_SingleLineSide_OmitsCount()
    {
        PlanningUnifiedDiff.Build("a\n", "a\nb\n")
            .ShouldBe("@@ -1 +1,2 @@\n a\n+b");
    }

    [Fact]
    public void Build_Deterministic_SameInputSameOutput()
    {
        var first = PlanningUnifiedDiff.Build("one\ntwo\nthree\n", "one\nTWO\nthree\nfour\n");
        var second = PlanningUnifiedDiff.Build("one\ntwo\nthree\n", "one\nTWO\nthree\nfour\n");

        second.ShouldBe(first);
        first.ShouldNotBeEmpty();
    }
}
