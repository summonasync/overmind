using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramReplySplitterTests
{
    [Fact]
    public void Split_ShortText_ReturnsSingleChunk()
    {
        TelegramReplySplitter.Split("hello there").ShouldBe(["hello there"]);
    }

    [Fact]
    public void Split_BlankText_ReturnsNothing()
    {
        TelegramReplySplitter.Split("   ").ShouldBeEmpty();
    }

    [Fact]
    public void Split_ParagraphsPackIntoChunksWithoutBreaking()
    {
        var first = new string('a', 1_500);
        var second = new string('b', 1_500);
        var third = new string('c', 1_500);

        var chunks = TelegramReplySplitter.Split($"{first}\n\n{second}\n\n{third}");

        chunks.Count.ShouldBe(2);
        chunks[0].ShouldBe($"{first}\n\n{second}");
        chunks[1].ShouldBe(third);
    }

    [Fact]
    public void Split_ParagraphOverflow_StartsNewChunkAtBoundary()
    {
        var first = new string('a', 3_000);
        var second = new string('b', 3_000);

        var chunks = TelegramReplySplitter.Split($"{first}\n\n{second}");

        chunks.Count.ShouldBe(2);
        chunks[0].ShouldBe(first);
        chunks[1].ShouldBe(second);
    }

    [Fact]
    public void Split_OversizedParagraph_HardSplitsAtTheLimit()
    {
        var wall = new string('x', TelegramReplySplitter.MaxMessageLength * 2 + 10);

        var chunks = TelegramReplySplitter.Split(wall);

        chunks.Count.ShouldBe(3);
        chunks[0].Length.ShouldBe(TelegramReplySplitter.MaxMessageLength);
        chunks[1].Length.ShouldBe(TelegramReplySplitter.MaxMessageLength);
        chunks[2].Length.ShouldBe(10);
        string.Concat(chunks).ShouldBe(wall);
    }

    [Fact]
    public void Split_MixedContent_AllChunksStayWithinTheLimit()
    {
        var paragraphs = Enumerable.Range(0, 12)
            .Select(index => $"section {index} {new string('m', 700)}")
            .ToArray();
        var text = string.Join("\n\n", paragraphs);

        var chunks = TelegramReplySplitter.Split(text);

        chunks.Count.ShouldBeGreaterThan(1);
        chunks.ShouldAllBe(chunk => chunk.Length <= TelegramReplySplitter.MaxMessageLength);
        chunks[0].ShouldStartWith("section 0");
        chunks.Last().ShouldContain("section 11");
    }
}
