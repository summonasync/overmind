using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

internal static class DescriptionBudgets
{
    public const int ToolDescriptionMax = 400;
    public const int ParameterDescriptionMax = 120;
    public const int PerToolTotalMax = 800;
    public const int RepoTotalMax = 24_000;
}

public class DescriptionBudgetLintTests
{
    internal const string DietPendingCategory = "description-diet-expected-red";

    [Fact]
    public void Scan_SeesTheWholeToolSurfaceOnce()
    {
        var snapshot = ToolDescriptionScanner.Scan();

        snapshot.ToolTypes.Count
            .ShouldBe(ToolDescriptionScanner.ExpectedToolTypeCount,
                $"the scan walked {snapshot.ToolTypes.Count} [McpServerToolType] types: {string.Join(", ", snapshot.ToolTypes)}");

        snapshot.Tools.Select(t => t.ToolName)
            .ToList()
            .ShouldBe([.. ToolDescriptionScanner.ExpectedToolNames],
                "the tool surface moved — budgets and the diet baseline are counted over this exact inventory");
    }

    [Fact]
    public void Scan_ReportsMissingAndDuplicatedToolsByName()
    {
        var scanned = ToolDescriptionScanner.Scan().Tools.Select(t => t.ToolName).ToList();
        var frozen = ToolDescriptionScanner.ExpectedToolNames;

        scanned
            .GroupBy(n => n, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} declared {g.Count()} times")
            .ToList()
            .ShouldBeEmpty("a tool name can only be issued once per adapter");

        scanned.Except(frozen, StringComparer.Ordinal).ToList()
            .ShouldBeEmpty($"not in the frozen inventory of {frozen.Length} tools");

        frozen.Except(scanned, StringComparer.Ordinal).ToList()
            .ShouldBeEmpty("the scan never saw these");
    }

    [Fact]
    [Trait("Category", DietPendingCategory)]
    public void Budgets_Descriptions_FitAllFourCeilings()
    {
        var snapshot = ToolDescriptionScanner.Scan();

        snapshot.Tools.Count
            .ShouldBe(ToolDescriptionScanner.ExpectedToolNames.Length,
                "the budget report would be vacuous — the scan did not see the full tool surface; run Scan_SeesTheWholeToolSurfaceOnce first");

        DescriptionDietReport.ViolatedBudgets(snapshot)
            .ShouldBeEmpty(DescriptionDietReport.Violations(snapshot));
    }
}
