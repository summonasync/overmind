using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class FeatureSlugGeneratorTests
{
    [Theory]
    [InlineData("Fix the Wave Chat Roster", "fix-the-wave-chat-roster")]
    [InlineData("  Hello, World! (v2)  ", "hello-world-v2")]
    [InlineData("Привет, Мир!", "privet-mir")]
    [InlineData("Ёлка и Щука", "yolka-i-shchuka")]
    [InlineData("Мой сервер 42", "moy-server-42")]
    [InlineData("??? ... !!!", "feature")]
    public void SlugPart_NormalizesTitleToSlugCharacters(string title, string expected) =>
        FeatureSlugGenerator.SlugPart(title).ShouldBe(expected);

    [Fact]
    public void SlugPart_IsDeterministic() =>
        FeatureSlugGenerator.SlugPart("Ыы Прр Ex")
            .ShouldBe(FeatureSlugGenerator.SlugPart("Ыы Прр Ex"));

    [Fact]
    public void Compose_EmptyBoard_StartsAtPadded001() =>
        FeatureSlugGenerator.Compose([], "My Feature").ShouldBe("001-my-feature");

    [Fact]
    public void Compose_NumberSlotTaken_LandsOnTheNextFreeNumber() =>
        FeatureSlugGenerator.Compose(["001-my-feature"], "My Feature").ShouldBe("002-my-feature");

    [Fact]
    public void Compose_UnpaddedTakenNumber_RespectsNumericCompare() =>
        FeatureSlugGenerator.Compose(["7-pinned"], "Pinned").ShouldBe("008-pinned");

    [Fact]
    public void Compose_ImportedDuplicateNumbers_PicksBeyondBoth() =>
        FeatureSlugGenerator.Compose(["010-alpha", "010-beta"], "Gamma").ShouldBe("011-gamma");

    [Fact]
    public void Compose_LongTitle_TruncatesSlugPartAndKeepsTheNumber()
    {
        var composed = FeatureSlugGenerator.Compose(
            [], "Wave Chat Roster Presence Line With A Very Long Descriptive Title");

        composed.Length.ShouldBeLessThanOrEqualTo(FeatureSlugGenerator.MaxLength);
        composed.ShouldStartWith("001-");
        composed.ShouldNotEndWith("-");
    }

    [Fact]
    public void Compose_CapShrinksTheSlugPartNotTheNumber()
    {
        var title = "Wave Chat Roster Presence Line With A Very Long Descriptive Title";
        var composed = FeatureSlugGenerator.Compose(["168-ship"], title);

        composed.Length.ShouldBeLessThanOrEqualTo(FeatureSlugGenerator.MaxLength);
        composed.Length.ShouldBeLessThan("169-".Length + FeatureSlugGenerator.SlugPart(title).Length);
        composed.ShouldStartWith("169-");
        composed.ShouldNotEndWith("-");
    }
}
