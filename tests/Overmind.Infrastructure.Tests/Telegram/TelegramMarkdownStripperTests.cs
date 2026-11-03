using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramMarkdownStripperTests
{
    [Fact]
    public void Strip_RemovesEmphasisAndCodeMarkers()
    {
        TelegramMarkdownStripper.Strip("**bold** and *italic* and ~~gone~~ and `code`")
            .ShouldBe("bold and italic and gone and code");
    }

    [Fact]
    public void Strip_KeepsSnakeCaseIdentifiersIntact()
    {
        TelegramMarkdownStripper.Strip("check get_user_data and user_id in `fn_call`")
            .ShouldBe("check get_user_data and user_id in fn_call");
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
    public void Strip_KeepsHeadingMarkersAsPlainText()
    {
        TelegramMarkdownStripper.Strip("## Plan\n\nDetails here")
            .ShouldBe("## Plan\n\nDetails here");
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
