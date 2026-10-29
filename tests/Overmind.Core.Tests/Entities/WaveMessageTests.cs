using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public sealed class WaveMessageTests
{
    [Fact]
    public void Create_TrimsText_AndCarriesRoomAndAuthor()
    {
        var message = WaveMessage.Create(Guid.NewGuid(), "  backend  ", " Dan ", "marshal", "  ping the lane  ");

        message.Room.ShouldBe("backend");
        message.AuthorAlias.ShouldBe("Dan");
        message.AuthorRole.ShouldBe("marshal");
        message.Text.ShouldBe("ping the lane");
        message.CreatedAt.ShouldNotBe(default);
    }

    [Fact]
    public void Create_NormalizesAllRoom_AnyCasing()
    {
        WaveMessage.Create(Guid.NewGuid(), "ALL", "Dan", "worker", "hi").Room.ShouldBe("all");
        WaveMessage.Create(Guid.NewGuid(), "All", "Dan", "worker", "hi").Room.ShouldBe("all");
    }

    [Fact]
    public void Create_RejectsBlankAndOverCapText()
    {
        Should.Throw<ArgumentException>(() => WaveMessage.Create(Guid.NewGuid(), "all", "Dan", "worker", "   "));

        var overCap = Should.Throw<ArgumentException>(() =>
            WaveMessage.Create(Guid.NewGuid(), "all", "Dan", "worker", new string('x', WaveMessage.MaxTextLength + 1)));
        overCap.Message.ShouldContain("500");
    }

    [Fact]
    public void Create_RejectsBlankRoom_AndOverCapIdentity()
    {
        Should.Throw<ArgumentException>(() => WaveMessage.Create(Guid.NewGuid(), "  ", "Dan", "worker", "hi"));
        Should.Throw<ArgumentException>(() =>
            WaveMessage.Create(Guid.NewGuid(), "all", new string('x', WaveMessage.MaxIdentityLength + 1), "worker", "hi"));
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
    public void ParseMentions_RequiresWordBoundaryBeforeTheAt()
    {
        var mentions = WaveMessage.ParseMentions(
            "agent-name@summonasync.dev alias@summonasync.dev mail x@alias");

        mentions.ShouldBeEmpty();
    }

    [Fact]
    public void ParseMentions_MatchesAtPunctuationAndEndBoundaries()
    {
        var mentions = WaveMessage.ParseMentions("(@alias), ping @marshal");

        mentions.ShouldBe(["alias", "marshal"]);
    }

    [Fact]
    public void ParseMentions_RejectsTokenTruncatedMidWord()
    {
        var mentions = WaveMessage.ParseMentions($"@{new string('a', WaveMessage.MaxIdentityLength + 1)}");

        mentions.ShouldBeEmpty();
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
        var message = WaveMessage.Create(Guid.NewGuid(), "all", "Dan", "worker", "heads up @Marshal");

        message.Mentions.ShouldBe(" marshal ");
        message.MentionList.ShouldBe(["marshal"]);
    }

    [Fact]
    public void Create_WithoutMentions_LeavesMentionsEmpty()
    {
        var message = WaveMessage.Create(Guid.NewGuid(), "all", "Dan", "worker", "no pings here");

        message.Mentions.ShouldBeEmpty();
        message.MentionList.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("marshal", "marshal")]
    [InlineData("  marshal  ", "marshal")]
    [InlineData("worker", "worker")]
    [InlineData("admiral", "worker")]
    [InlineData("Marshal", "worker")]
    public void Create_StampsAuthorRole(string role, string expected)
    {
        var message = WaveMessage.Create(Guid.NewGuid(), "all", "Dan", role, "stamping the role");

        message.AuthorRole.ShouldBe(expected);
    }

    [Fact]
    public void Create_RejectsBlankAuthorRole()
    {
        Should.Throw<ArgumentException>(() => WaveMessage.Create(Guid.NewGuid(), "all", "Dan", "   ", "hi"));
        Should.Throw<ArgumentException>(() => WaveMessage.Create(Guid.NewGuid(), "all", "Dan", null!, "hi"));
    }

    [Fact]
    public void Create_RejectsOverCapAuthorRole()
    {
        Should.Throw<ArgumentException>(() =>
            WaveMessage.Create(Guid.NewGuid(), "all", "Dan", new string('x', WaveMessage.MaxIdentityLength + 1), "hi"));
    }
}
