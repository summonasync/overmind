using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramMarkdownStripperTests
{
    [Fact]
    public void Strip_RemovesEmphasisAndCodeMarkers()
    {
        TelegramMarkdownStripper.Strip("**bold** and _italic_ and ~~gone~~ and `code`")
            .ShouldBe("bold and italic and gone and code");
    }

    [Fact]
    public void Strip_UnwrapsFencedCodeBlocks()
    {
        TelegramMarkdownStripper.Strip("before\n```csharp\nvar x = 1;\n```\nafter")
            .ShouldBe("before\nvar x = 1;\nafter");
    }

    [Fact]
    public void Strip_ConvertsLinksToTextAndUrl()
    {
        TelegramMarkdownStripper.Strip("see [the docs](https://example.dev/kit) for details")
            .ShouldBe("see the docs (https://example.dev/kit) for details");
    }

    [Fact]
    public void Strip_BareHeadingMarkersAreRemoved()
    {
        TelegramMarkdownStripper.Strip("## Plan\n\n### Details here")
            .ShouldBe("Plan\n\nDetails here");
    }

    [Fact]
    public void Strip_PlainTextPassesThrough()
    {
        var text = "plain reply, nothing fancy";
        TelegramMarkdownStripper.Strip(text).ShouldBe(text);
    }

    [Fact]
    public void Strip_BlankInput_ReturnsEmpty()
    {
        TelegramMarkdownStripper.Strip("   ").ShouldBe(string.Empty);
    }
}
