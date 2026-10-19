using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class OnboardBlockContentTests
{
    public const string DomainBoundarySentence =
        "Domain boundary: work knowledge — projects, features, tasks, planning entries — goes into Overmind entries; " +
        "persona, communication and relational notes plus agent journals go into Grimoire when its `grimoire` MCP server is wired.";

    [Fact]
    public void Render_WithoutProject_UsesPlaceholders()
    {
        var content = OnboardBlockContent.Render(null);

        content.ShouldContain("This repository is Overmind project 'unknown'");
        content.ShouldContain("(id: resolved on first overmind_onboard call)");
        content.ShouldContain("call `overmind_onboard` first");
        content.ShouldContain("OVERMIND_PROJECT_ID");
    }

    [Fact]
    public void Render_ContainsDomainBoundaryParagraph_Verbatim()
    {
        var content = OnboardBlockContent.Render(null);

        content.ShouldContain(DomainBoundarySentence);
        content.ShouldContain("Escape hatch: set OVERMIND_PROJECT_ID");
    }

    [Fact]
    public void Render_BoundaryParagraph_SeparatedIntoOwnParagraph()
    {
        var content = OnboardBlockContent.Render(null);

        var paragraphs = content.Split("\n\n", StringSplitOptions.None);
        paragraphs.ShouldContain(paragraph => paragraph == DomainBoundarySentence);
    }
}
