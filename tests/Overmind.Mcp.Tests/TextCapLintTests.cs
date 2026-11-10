using ModelContextProtocol;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class TextCapLintTests
{
    [Fact]
    public void Cap_OverWaveCap_ThrowsTheExactCutMessage()
    {
        var ex = Should.Throw<McpException>(() => TextCapLint.Cap(new string('a', 507), 500));

        ex.Message.ShouldBe("text is 507 chars; cap is 500; cut 7");
    }

    [Fact]
    public void Cap_OverDirectiveCap_ThrowsTheExactCutMessage()
    {
        var ex = Should.Throw<McpException>(() => TextCapLint.Cap(new string('b', 2050), 2000));

        ex.Message.ShouldBe("text is 2050 chars; cap is 2000; cut 50");
    }

    [Theory]
    [InlineData(499)]
    [InlineData(500)]
    public void Cap_AtOrUnderCap_Passes(int length)
    {
        Should.NotThrow(() => TextCapLint.Cap(new string('c', length), 500));
    }
}
