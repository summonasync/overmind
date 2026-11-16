using System.Text.Json.Nodes;
using ModelContextProtocol;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(AdapterSessionState))]
public sealed class AgentWakeToolContractTests : IAsyncLifetime
{
    private static readonly Guid SessionId = Guid.Parse("18181818-1818-1818-1818-181818181818");

    private const string ReplyId = "13131313-1313-1313-1313-131313131313";
    private const string MentionId = "16161616-1616-1616-1616-161616161616";

    private const string FirstPageJson =
        $$"""{"signals":[{"kind":"notification-reply","refId":"{{ReplyId}}","roomId":null,"from":"human","createdAt":"2026-10-29T09:00:00Z","excerpt":"Ship it as planned."},{"kind":"wave-mention","refId":"{{MentionId}}","roomId":"lane-a","from":"kai","createdAt":"2026-10-29T09:05:00Z","excerpt":"@dan the merge is green"}],"cursor":"2026-10-29T09:05:00.0000000Z|{{MentionId}}"}""";

    private const string SecondPageJson =
        $$"""{"signals":[],"cursor":"2026-10-29T09:05:00.0000000Z"}""";

    private StubOvermindApi _stub = null!;
    private OvermindApiClient _api = null!;
    private string? _originalProjectOverride;

    public Task InitializeAsync()
    {
        _stub = StubOvermindApi.Start();
        _api = new OvermindApiClient(OvermindApiClient.CreateHttpClient(_stub.BaseUrl));
        AdapterSession.Id = SessionId;
        _originalProjectOverride = Environment.GetEnvironmentVariable("OVERMIND_PROJECT_ID");
        Environment.SetEnvironmentVariable("OVERMIND_PROJECT_ID", null);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("OVERMIND_PROJECT_ID", _originalProjectOverride);
        AdapterSession.Id = null;
        await _stub.DisposeAsync();
    }

    [Fact]
    public async Task Peek_WithoutSince_HitsTheBareRoute_AndStampsTheSessionHeader()
    {
        _stub.Route("GET", "/api/agent/wake", 200, FirstPageJson);

        var wake = await Peek(null);

        var request = WakeRequests().Single();
        request.Path.ShouldBe("/api/agent/wake");
        request.Query.ShouldBeEmpty();
        request.Headers[AdapterSession.HeaderName].ShouldBe(SessionId.ToString());
        wake["cursor"]!.GetValue<string>().ShouldBe($"2026-10-29T09:05:00.0000000Z|{MentionId}");
        var signals = wake["signals"]!.AsArray().Select(s => s!.AsObject()).ToArray();
        signals.Select(s => s["kind"]!.GetValue<string>())
            .ShouldBe(["notification-reply", "wave-mention"]);
        signals[0].Select(kv => kv.Key).ShouldBe(["kind", "refId", "roomId", "from", "createdAt", "excerpt"]);
        signals[0]["roomId"].ShouldBeNull();
        signals[1]["roomId"]!.GetValue<string>().ShouldBe("lane-a");
        signals[1]["from"]!.GetValue<string>().ShouldBe("kai");
    }

    [Fact]
    public async Task Peek_PassesTheCursorThroughAsSince_AndEchoesThePageVerbatim()
    {
        var cursor = $"2026-10-29T09:05:00.0000000Z|{MentionId}";
        _stub.Route("GET", "/api/agent/wake", 200, SecondPageJson);

        var wake = await Peek(cursor);

        var request = WakeRequests().Single();
        request.Query.ShouldBe($"since={Uri.EscapeDataString(cursor)}");
        wake.ToJsonString().ShouldBe(JsonNode.Parse(SecondPageJson)!.ToJsonString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Peek_BlankSince_RequestsTheBareRoute(string? since)
    {
        _stub.Route("GET", "/api/agent/wake", 200, FirstPageJson);

        await Peek(since);

        WakeRequests().Single().Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task Peek_AppendsTheResolvedProjectId_WhenTheResolverYieldsOne()
    {
        _stub.Route("GET", "/api/agent/wake", 200, FirstPageJson);
        _stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);

        await Peek(null);

        var wake = WakeRequests().Single();
        wake.Query.ShouldBe($"projectId={Canned.ProjectId}");
    }

    [Fact]
    public async Task Peek_ProjectIdFollowsTheCursor_WhenTheProjectResolves()
    {
        var cursor = $"2026-10-29T09:05:00.0000000Z|{MentionId}";
        _stub.Route("GET", "/api/agent/wake", 200, SecondPageJson);
        _stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);

        await Peek(cursor);

        WakeRequests().Single().Query.ShouldBe(
            $"since={Uri.EscapeDataString(cursor)}&projectId={Canned.ProjectId}");
    }

    [Fact]
    public async Task Peek_WhenResolutionReturnsGuidance_TheWindowStaysMachineWide()
    {
        _stub.Route("GET", "/api/agent/wake", 200, FirstPageJson);
        _stub.Route("POST", "/api/onboard", 404,
            """{"title":"Not Found","detail":"unregistered directory guidance","status":404}""");

        await Peek(null);

        WakeRequests().Single().Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task Peek_FeedsTheEchoedCursorBackToPageForward()
    {
        var firstCursor = $"2026-10-29T09:05:00.0000000Z|{MentionId}";
        _stub.Route("GET", "/api/agent/wake",
            req => new StubOvermindApi.Response(200,
                req.Query.Contains("since=") ? SecondPageJson : FirstPageJson));

        var first = await Peek(null);
        var second = await Peek(first["cursor"]!.GetValue<string>());

        var reads = _stub.Requests.Where(r => r.Path == "/api/agent/wake").ToArray();
        reads.Length.ShouldBe(2);
        reads[0].Query.ShouldBeEmpty();
        reads[1].Query.ShouldBe($"since={Uri.EscapeDataString(firstCursor)}");
        second["signals"]!.AsArray().ShouldBeEmpty();
        second["cursor"]!.GetValue<string>().ShouldBe("2026-10-29T09:05:00.0000000Z");
    }

    [Fact]
    public async Task Peek_MalformedSince_SurfacesTheHostDetail()
    {
        _stub.Route("GET", "/api/agent/wake", 400,
            """{"title":"BadHttpRequestException","detail":"The since parameter must be an ISO 8601 timestamp, optionally followed by '|' and the cursor ref id echoed from the previous page.","status":400}""");

        var ex = await Should.ThrowAsync<McpException>(() => Tool().Peek("yesterday-ish"));

        ex.Message.ShouldContain("must be an ISO 8601 timestamp");
        ex.Message.ShouldContain("cursor ref id echoed from the previous page");
    }

    [Fact]
    public async Task Peek_WithoutASession_SurfacesTheIdentityRequirement()
    {
        AdapterSession.Id = null;
        _stub.Route("GET", "/api/agent/wake", 400,
            """{"title":"BadHttpRequestException","detail":"Wake polling requires the X-Overmind-Session header and a registered agent session — register via POST /api/agent/sessions.","status":400}""");

        var ex = await Should.ThrowAsync<McpException>(() => Tool().Peek(null));

        ex.Message.ShouldContain("requires the X-Overmind-Session header");
        ex.Message.ShouldContain("register via POST /api/agent/sessions");
    }

    private async Task<JsonObject> Peek(string? since)
    {
        var element = await Tool().Peek(since);
        return JsonNode.Parse(element.GetRawText())!.AsObject();
    }

    private StubOvermindApi.Request[] WakeRequests() =>
        [.. _stub.Requests.Where(r => r.Method == "GET" && r.Path == "/api/agent/wake")];

    private AgentWakeTools Tool() => new(_api, new ProjectResolver(_api));
}
