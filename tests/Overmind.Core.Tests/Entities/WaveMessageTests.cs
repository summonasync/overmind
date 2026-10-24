using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public sealed class WaveMessageTests
{
    [Fact]
    public void Create_TrimsText_AndCarriesRoomAndAuthor()
    {
        var message = WaveMessage.Create(Guid.NewGuid(), "  backend  ", " Dan ", "  ping the lane  ");

        message.Room.ShouldBe("backend");
        message.AuthorAlias.ShouldBe("Dan");
        message.Text.ShouldBe("ping the lane");
        message.CreatedAt.ShouldNotBe(default);
    }

    [Fact]
    public void Create_NormalizesAllRoom_AnyCasing()
    {
        WaveMessage.Create(Guid.NewGuid(), "ALL", "Dan", "hi").Room.ShouldBe("all");
        WaveMessage.Create(Guid.NewGuid(), "All", "Dan", "hi").Room.ShouldBe("all");
    }

    [Fact]
    public void Create_RejectsBlankAndOverCapText()
    {
        Should.Throw<ArgumentException>(() => WaveMessage.Create(Guid.NewGuid(), "all", "Dan", "   "));

        var overCap = Should.Throw<ArgumentException>(() =>
            WaveMessage.Create(Guid.NewGuid(), "all", "Dan", new string('x', WaveMessage.MaxTextLength + 1)));
        overCap.Message.ShouldContain("500");
    }

    [Fact]
    public void Create_RejectsBlankRoom_AndOverCapIdentity()
    {
        Should.Throw<ArgumentException>(() => WaveMessage.Create(Guid.NewGuid(), "  ", "Dan", "hi"));
        Should.Throw<ArgumentException>(() =>
            WaveMessage.Create(Guid.NewGuid(), "all", new string('x', WaveMessage.MaxIdentityLength + 1), "hi"));
    }

    [Fact]
    public void ParseMentions_ExtractsDistinctLowercaseAliases()
    {
        var mentions = WaveMessage.ParseMentions("ping @Dan and @dan, plus @wave-marshal_2");

        mentions.ShouldBe(["dan", "wave-marshal_2"]);
    }

    [Fact]
    public void ParseMentions_IgnoresEmails_AndNonTokenTail()
    {
        var mentions = WaveMessage.ParseMentions("mail me at dan@example.com or @dan!");

        mentions.ShouldBe(["dan"]);
    }

    [Fact]
    public void ParseMentions_CapsTheDistinctSet()
    {
        var text = string.Join(' ', Enumerable
            .Range(0, WaveMessage.MaxMentions + 10)
            .Select(i => $"@alias{i:00}"));

        var mentions = WaveMessage.ParseMentions(text);

        mentions.Count.ShouldBe(WaveMessage.MaxMentions);
    }

    [Fact]
    public void Create_CarriesMentions_PaddedForServerSideMatching()
    {
        var message = WaveMessage.Create(Guid.NewGuid(), "all", "Dan", "heads up @Marshal");

        message.Mentions.ShouldBe(" marshal ");
        message.MentionList.ShouldBe(["marshal"]);
    }

    [Fact]
    public void Create_WithoutMentions_LeavesMentionsEmpty()
    {
        var message = WaveMessage.Create(Guid.NewGuid(), "all", "Dan", "no pings here");

        message.Mentions.ShouldBeEmpty();
        message.MentionList.ShouldBeEmpty();
    }
}
