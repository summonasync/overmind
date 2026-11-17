using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class ContractsDocTests
{
    [Fact]
    public void Sections_HaveExactlyOneNonEmptyAnchorPerFrozenTool()
    {
        foreach (var toolName in ToolDescriptionScanner.ExpectedToolNames)
        {
            ContractsDoc.Section(toolName).ShouldNotBeNullOrWhiteSpace();
        }
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
