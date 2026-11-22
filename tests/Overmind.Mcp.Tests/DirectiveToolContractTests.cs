using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(ProcessEnvironment))]
public sealed class DirectiveToolContractTests : IAsyncLifetime
{
    private static readonly Guid DirectiveId = Guid.Parse("16161616-1616-1616-1616-161616161616");

    private static readonly string AppendRoute =
        $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/directives";

    private static readonly string AckRoute =
        $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/directives/{DirectiveId}/ack";

    private static readonly string DirectiveJson = new JsonObject
    {
        ["id"] = DirectiveId,
        ["text"] = "HOLD: shared file inbound",
        ["authorAlias"] = "Marshal",
        ["authorVendor"] = "wave-marshal",
        ["createdAt"] = "2026-10-17T12:00:00Z",
        ["ack"] = false
    }.ToJsonString();

    private StubOvermindApi _stub = null!;
    private OvermindApiClient _api = null!;

    public Task InitializeAsync()
    {
        _stub = StubOvermindApi.Start();
        _api = new OvermindApiClient(OvermindApiClient.CreateHttpClient(_stub.BaseUrl));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _stub.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingProjectId_ResolvesThroughTheOnboardCwd(bool mutating)
    {
        _stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);
        _stub.Route("POST", AppendRoute, 200, DirectiveJson);
        _stub.Route("POST", AckRoute, 200,
            $$"""{"id":"{{DirectiveId}}","ack":true,"ackByAlias":"Dan","ackVendor":"qwen","ackAt":"2026-10-17T12:05:00Z"}""");

        if (mutating)
        {
            var appended = await Tool().AppendDirective(Canned.PlanningSlug, "HOLD: shared file inbound");
            Text(appended).ShouldContain(DirectiveId.ToString());
        }
        else
        {
            var acked = await Tool().AckDirective(Canned.PlanningSlug, DirectiveId.ToString());
            Text(acked).ShouldBe($"Directive {DirectiveId} acknowledged.");
        }

        var onboard = _stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        var body = JsonNode.Parse(onboard.Body)!;
        body["cwd"]!.GetValue<string>().ShouldBe(Environment.CurrentDirectory);
        body["create"]!.GetValue<bool>().ShouldBeFalse();

        _stub.Requests.ShouldContain(r =>
            r.Method == "POST" && r.Path == (mutating ? AppendRoute : AckRoute));
    }

    [Fact]
    public async Task UnresolvableCwd_ReturnsTheGuidanceWithoutPostingTheDirective()
    {
        _stub.Route("POST", "/api/onboard", 404, Canned.OnboardUnregisteredCwdJson);

        var ex = await Should.ThrowAsync<McpException>(
            () => Tool().AppendDirective(Canned.PlanningSlug, "text"));

        ex.Message.ShouldContain("Could not resolve the project for the working directory");
        ex.Message.ShouldContain("Pass projectId explicitly");
        _stub.Requests.ShouldNotContain(r => r.Path == AppendRoute);
    }

    [Fact]
    public async Task ExplicitProjectId_SkipsTheOnboardRoundTrip()
    {
        _stub.Route("POST", AppendRoute, 200, DirectiveJson);

        await Tool().AppendDirective(Canned.PlanningSlug, "HOLD: shared file inbound", Canned.ProjectId);

        _stub.Requests.ShouldNotContain(r => r.Path == "/api/onboard");
    }

    [Fact]
    public async Task AppendDirective_PostsTheText_AndReportsTheDirective()
    {
        _stub.Route("POST", AppendRoute, 200, DirectiveJson);

        var result = await Tool().AppendDirective(
            Canned.PlanningSlug, "HOLD: shared file inbound", Canned.ProjectId);

        var request = _stub.Requests.Single(r => r.Method == "POST");
        request.Path.ShouldBe(AppendRoute);
        request.Body.ShouldContain("\"text\":\"HOLD: shared file inbound\"");
        Text(result).ShouldContain(DirectiveId.ToString());
        Text(result).ShouldContain("planning_ack_directive");
    }

    [Fact]
    public async Task AckDirective_PostsTheAck_AndReportsIt()
    {
        _stub.Route("POST", AckRoute, 200,
            $$"""{"id":"{{DirectiveId}}","ack":true,"ackByAlias":"Dan","ackVendor":"qwen","ackAt":"2026-10-17T12:05:00Z"}""");

        var result = await Tool().AckDirective(Canned.PlanningSlug, DirectiveId.ToString(), Canned.ProjectId);

        var request = _stub.Requests.Single(r => r.Method == "POST");
        request.Path.ShouldBe(AckRoute);
        Text(result).ShouldBe($"Directive {DirectiveId} acknowledged.");
    }

    [Fact]
    public async Task AckDirective_AlreadyAcked_ReportsTheNoOp()
    {
        _stub.Route("POST", AckRoute, 200,
            $$"""{"id":"{{DirectiveId}}","ack":false,"ackByAlias":"Dan","ackVendor":"qwen","ackAt":"2026-10-17T12:05:00Z"}""");

        var result = await Tool().AckDirective(Canned.PlanningSlug, DirectiveId.ToString(), Canned.ProjectId);

        Text(result).ShouldBe($"Directive {DirectiveId} was already acknowledged — no change.");
    }

    [Fact]
    public async Task AppendDirective_HostFailure_SurfacesAsMcpException()
    {
        _stub.Route("POST", AppendRoute, 404, """{"title":"MissingFeature","detail":"Feature 'demo-feature' was not found."}""");

        var ex = await Should.ThrowAsync<McpException>(
            () => Tool().AppendDirective(Canned.PlanningSlug, "text", Canned.ProjectId));

        ex.Message.ShouldContain("was not found");
    }

    [Fact]
    public async Task AppendDirective_GraceBand_SendsTheWholeCommandAndEchoesWarnings()
    {
        var warned = new JsonObject
        {
            ["id"] = DirectiveId,
            ["text"] = new string('x', 2001),
            ["authorAlias"] = "Marshal",
            ["createdAt"] = "2026-10-17T12:00:00Z",
            ["ack"] = false,
            ["warnings"] = new JsonArray(new JsonObject
            {
                ["accepted_len"] = 2001,
                ["limit"] = 2000,
                ["truncated"] = false,
            })
        }.ToJsonString();
        _stub.Route("POST", AppendRoute, 200, warned);
        var text = new string('x', 2001);

        var result = await Tool().AppendDirective(Canned.PlanningSlug, text, Canned.ProjectId);

        _stub.Requests.Single(r => r.Method == "POST").Body.ShouldContain($"\"text\":\"{text}\"");
        Text(result).ShouldContain("Warnings:");
        Text(result).ShouldContain("accepted_len");
    }

    [Fact]
    public async Task AppendDirective_BeyondCeiling_NoLocalLint_AndTheHostRejectSurfaces()
    {
        _stub.Route("POST", AppendRoute, 400,
            """{"title":"DirectiveTextTooLong","detail":"Directive text is 2441 characters; the limit is 2000 """ +
            """and the accept ceiling is 2440. Nothing was stored: a directive is a command, so this door """ +
            """never truncates. The tail that would have been lost reads \"tail\". Compress and resend."}""");

        var ex = await Should.ThrowAsync<McpException>(
            () => Tool().AppendDirective(Canned.PlanningSlug, new string('x', 2441), Canned.ProjectId));

        ex.Message.ShouldContain("is 2441 characters");
        ex.Message.ShouldContain("limit is 2000");
        ex.Message.ShouldContain("tail");
    }

    [Fact]
    public async Task AppendDirective_AtCap_ReachesTheWire()
    {
        _stub.Route("POST", AppendRoute, 200, DirectiveJson);
        var atCap = new string('x', 2000);

        await Tool().AppendDirective(Canned.PlanningSlug, atCap, Canned.ProjectId);

        _stub.Requests.Single(r => r.Method == "POST").Body.ShouldContain($"\"text\":\"{atCap}\"");
    }

    private static string Text(CallToolResult result) =>
        ((TextContentBlock)result.Content[0]).Text;

    // ---- 266/T3 row 6: the ack takes a unique hex prefix, because relays and snapshots show one ----

    private static readonly string CurrentRoute =
        $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/current";

    private static readonly string AnyAckRoute =
        $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/directives/[^/]+/ack";

    private static string OpenChannelJson(params Guid[] ids) => new JsonObject
    {
        ["openDirectives"] = new JsonArray([.. ids.Select(id => (JsonNode)new JsonObject
        {
            ["id"] = id,
            ["text"] = "HOLD: shared file inbound",
            ["authorAlias"] = "Marshal",
            ["createdAt"] = "2026-10-17T12:00:00Z",
        })]),
    }.ToJsonString();

    [Fact]
    public async Task AckDirective_UniqueHexPrefix_ResolvesAgainstTheOpenChannel()
    {
        var other = Guid.Parse("27272727-2727-2727-2727-272727272727");
        _stub.Route("GET", CurrentRoute, 200, OpenChannelJson(DirectiveId, other));
        _stub.Route("POST", AnyAckRoute, 200,
            $$"""{"id":"{{DirectiveId}}","ack":true,"ackByAlias":"Dan","ackVendor":"qwen","ackAt":"2026-10-17T12:05:00Z"}""");

        var result = await Tool().AckDirective(Canned.PlanningSlug, "16161616", Canned.ProjectId);

        Text(result).ShouldBe($"Directive {DirectiveId} acknowledged.");
        _stub.Requests.Single(r => r.Method == "POST").Path.ShouldBe(AckRoute);
    }

    [Fact]
    public async Task AckDirective_AmbiguousPrefix_ListsCandidatesAndAcksNothing()
    {
        var rival = Guid.Parse("16161616-2222-2222-2222-222222222222");
        _stub.Route("GET", CurrentRoute, 200, OpenChannelJson(DirectiveId, rival));

        var ex = await Should.ThrowAsync<McpException>(
            () => Tool().AckDirective(Canned.PlanningSlug, "16161616", Canned.ProjectId));

        ex.Message.ShouldContain("is the prefix of 2 open directives on 'demo-feature'");
        ex.Message.ShouldContain("ambiguous");
        ex.Message.ShouldContain("nothing was acknowledged");
        ex.Message.ShouldContain($"Candidates: {DirectiveId}, {rival}");
        _stub.Requests.ShouldNotContain(r => r.Method == "POST");
    }

    [Fact]
    public async Task AckDirective_PrefixOfNothing_OpenIdsAreEchoed()
    {
        _stub.Route("GET", CurrentRoute, 200, OpenChannelJson(DirectiveId));

        var ex = await Should.ThrowAsync<McpException>(
            () => Tool().AckDirective(Canned.PlanningSlug, "abcd1234", Canned.ProjectId));

        ex.Message.ShouldContain("no open directive on 'demo-feature' starts with 'abcd1234'");
        ex.Message.ShouldContain($"Open ids: {DirectiveId}");
        _stub.Requests.ShouldNotContain(r => r.Method == "POST");
    }

    [Fact]
    public async Task AckDirective_ChannelEmpty_TellsTheHolderTheAckProbablyLanded()
    {
        _stub.Route("GET", CurrentRoute, 200, OpenChannelJson());

        var ex = await Should.ThrowAsync<McpException>(
            () => Tool().AckDirective(Canned.PlanningSlug, "16161616", Canned.ProjectId));

        ex.Message.ShouldContain("The feature has no open directives");
        ex.Message.ShouldContain("the ack you are retrying probably already landed");
    }

    [Fact]
    public async Task AckDirective_FullUuid_NeedsNoVocabularyRead()
    {
        _stub.Route("POST", AckRoute, 200,
            $$"""{"id":"{{DirectiveId}}","ack":true,"ackByAlias":"Dan","ackVendor":"qwen","ackAt":"2026-10-17T12:05:00Z"}""");

        await Tool().AckDirective(Canned.PlanningSlug, DirectiveId.ToString(), Canned.ProjectId);

        _stub.Requests.ShouldNotContain(r => r.Path == CurrentRoute);
        _stub.Requests.Single(r => r.Method == "POST").Path.ShouldBe(AckRoute);
    }

    [Fact]
    public async Task AckDirective_NeitherUuidNorHex_IsNamedAsSuch()
    {
        var ex = await Should.ThrowAsync<McpException>(
            () => Tool().AckDirective(Canned.PlanningSlug, "the-first-one", Canned.ProjectId));

        ex.Message.ShouldContain("neither a UUID nor a hex prefix");
        ex.Message.ShouldContain("1-31 hex digits");
        _stub.Requests.ShouldNotContain(r => r.Method == "POST");
    }

    private DirectiveTools Tool() => new(_api, new MutationGuard(_api), new ProjectResolver(_api));
}
