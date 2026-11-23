using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class ContractsDocTests
{
    [Fact]
    public void Sections_HaveExactlyOneNonEmptyAnchorPerFrozenTool()
    {
        var headings = ContractsDoc.Headings();

        var duplicates = headings
            .GroupBy(head => head, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} x{group.Count()}")
            .ToArray();
        duplicates.ShouldBeEmpty($"duplicated '## ' anchors in {ContractsDoc.DocSource}: {string.Join(", ", duplicates)}");

        foreach (var toolName in ToolDescriptionScanner.ExpectedUnionToolNames)
        {
            headings.Count(head => string.Equals(head, toolName, StringComparison.Ordinal))
                .ShouldBe(1, $"expected exactly one '## {toolName}' anchor in {ContractsDoc.DocSource}");
            ContractsDoc.Section(toolName).ShouldNotBeNullOrWhiteSpace();
        }

        headings.Count.ShouldBe(ToolDescriptionScanner.ExpectedUnionToolNames.Length,
            $"{ContractsDoc.DocSource} carries anchors for tools the frozen roster does not list");
    }

    [Fact]
    public void Preamble_DeclaresTheTwoTierBudgetTheDescriptionsFitUnder()
    {
        var preamble = ContractsDoc.Preamble();

        preamble.ShouldContain("only selection-critical prose");
        preamble.ShouldContain("≤400 chars per tool description");
        preamble.ShouldContain("enforced by DescriptionBudgetLintTests");
        preamble.ShouldContain("the section heading is exactly the tool name");
    }
}
