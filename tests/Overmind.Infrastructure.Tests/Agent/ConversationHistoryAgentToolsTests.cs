using System.Text.Json;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class ConversationHistoryAgentToolsTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly Conversation _conversation;
    private readonly ConversationMessageStore _messages;
    private readonly List<ConversationMessage> _userRows = [];
    private readonly List<ConversationMessage> _assistantRows = [];

    public ConversationHistoryAgentToolsTests()
    {
        _conversation = Conversation.Create(null, "history tool tests");
        using var context = _temp.CreateContext();
        context.Conversations.Add(_conversation);
        context.SaveChanges();
        _messages = new ConversationMessageStore(_temp.CreateContext());
    }

    [Fact]
    public async Task InvokeAsync_WithoutCursor_PagesNewestTwentyByDefault()
    {
        await SeedTurnsAsync(30);

        var result = await InvokeAsync(new { });

        var items = result.GetProperty("items").EnumerateArray().ToList();
        result.GetProperty("count").GetInt32().ShouldBe(20);
        items[0].GetProperty("content").GetString().ShouldBe("user 21");
        items[0].GetProperty("role").GetString().ShouldBe(ConversationMessage.UserRole);
        items[^1].GetProperty("content").GetString().ShouldBe("assistant 30");
        items.Select(item => item.GetProperty("id").GetGuid())
            .ShouldBe(InterleavedRows().Skip(40).Select(row => row.Id));
    }

    [Fact]
    public async Task InvokeAsync_BeforeCursor_PagesTheOlderStretch()
    {
        await SeedTurnsAsync(30);
        var cursor = _userRows[20];

        var result = await InvokeAsync(new { before = cursor.Id.ToString() });

        var items = result.GetProperty("items").EnumerateArray().ToList();
        result.GetProperty("count").GetInt32().ShouldBe(20);
        items[0].GetProperty("content").GetString().ShouldBe("user 11");
        items[^1].GetProperty("content").GetString().ShouldBe("assistant 20");
        items.ShouldNotContain(item => item.GetProperty("id").GetGuid() == cursor.Id);
    }

    [Fact]
    public async Task InvokeAsync_LimitIsClampedToFiftyAndToOne()
    {
        await SeedTurnsAsync(30);

        var overCap = await InvokeAsync(new { limit = 500 });
        overCap.GetProperty("count").GetInt32().ShouldBe(50);
        overCap.GetProperty("items").EnumerateArray().First()
            .GetProperty("content").GetString().ShouldBe("user 6");

        var underFloor = await InvokeAsync(new { limit = 0 });
        underFloor.GetProperty("count").GetInt32().ShouldBe(1);
        underFloor.GetProperty("items").EnumerateArray().Single()
            .GetProperty("content").GetString().ShouldBe("assistant 30");
    }

    [Fact]
    public async Task InvokeAsync_UnknownCursor_ReturnsArgumentError()
    {
        await SeedTurnsAsync(2);

        var result = await InvokeAsync(new { before = Guid.NewGuid().ToString() });

        result.GetProperty("error").GetString()!.ShouldContain("'before'");
    }

    [Fact]
    public async Task InvokeAsync_CursorFromAnotherConversation_IsRejected()
    {
        await SeedTurnsAsync(1);
        var stranger = Conversation.Create(null, "another chat");
        using (var context = _temp.CreateContext())
        {
            context.Conversations.Add(stranger);
            context.SaveChanges();
        }

        var strangerRow = await _messages.AppendAsync(
            stranger.Id, ConversationMessage.UserRole, "foreign question", CancellationToken.None);

        var result = await InvokeAsync(new { before = strangerRow.Id.ToString() });

        result.GetProperty("error").GetString()!.ShouldContain("'before'");
    }

    [Fact]
    public async Task InvokeAsync_WithoutConversationScope_ReturnsHonestError()
    {
        await SeedTurnsAsync(1);
        var registry = CreateRegistry();

        var result = await registry.InvokeAsync(
            "conversation_history", JsonSerializer.SerializeToElement(new { }), default, CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldContain("no conversation scope");
    }

    [Fact]
    public async Task InvokeAsync_LongRowsAreClippedInTheListing()
    {
        await _messages.AppendAsync(
            _conversation.Id, ConversationMessage.UserRole, new string('x', 2500), CancellationToken.None);
        await _messages.AppendAsync(
            _conversation.Id, ConversationMessage.AssistantRole, "short answer", CancellationToken.None);

        var result = await InvokeAsync(new { });

        var items = result.GetProperty("items").EnumerateArray().ToList();
        items[0].GetProperty("content").GetString()!.ShouldBe(new string('x', 2000) + "…");
        items[^1].GetProperty("content").GetString().ShouldBe("short answer");
    }

    private async Task<JsonElement> InvokeAsync(object args)
    {
        var registry = CreateRegistry();
        return await registry.InvokeAsync(
            "conversation_history",
            JsonSerializer.SerializeToElement(args),
            new AgentToolInvocation(false, null, _conversation.Id),
            CancellationToken.None);
    }

    private IReadOnlyList<ConversationMessage> InterleavedRows()
    {
        var rows = new List<ConversationMessage>();
        for (var i = 0; i < _userRows.Count; i++)
        {
            rows.Add(_userRows[i]);
            rows.Add(_assistantRows[i]);
        }

        return rows;
    }

    private async Task SeedTurnsAsync(int count)
    {
        for (var turn = 1; turn <= count; turn++)
        {
            _userRows.Add(await _messages.AppendAsync(
                _conversation.Id, ConversationMessage.UserRole, $"user {turn}", CancellationToken.None));
            _assistantRows.Add(await _messages.AppendAsync(
                _conversation.Id, ConversationMessage.AssistantRole, $"assistant {turn}", CancellationToken.None));
        }
    }

    private AgentToolRegistry CreateRegistry()
    {
        var http = new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://127.0.0.1:1") };
        var state = new EmbeddedAgentSessionState();
        state.Set(Guid.NewGuid());
        return new AgentToolRegistry(
            new AgentLoopbackClient(http), state, Substitute.For<IGrimoireMemoryClient>(), _messages,
            new AgentRouteRecorder(new AgentRouteMemoryStore()));
    }

    public void Dispose() => _temp.Dispose();

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
