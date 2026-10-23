using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

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
            var acked = await Tool().AckDirective(Canned.PlanningSlug, DirectiveId);
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

        var result = await Tool().AckDirective(Canned.PlanningSlug, DirectiveId, Canned.ProjectId);

        var request = _stub.Requests.Single(r => r.Method == "POST");
        request.Path.ShouldBe(AckRoute);
        Text(result).ShouldBe($"Directive {DirectiveId} acknowledged.");
    }

    [Fact]
    public async Task AckDirective_AlreadyAcked_ReportsTheNoOp()
    {
        _stub.Route("POST", AckRoute, 200,
            $$"""{"id":"{{DirectiveId}}","ack":false,"ackByAlias":"Dan","ackVendor":"qwen","ackAt":"2026-10-17T12:05:00Z"}""");

        var result = await Tool().AckDirective(Canned.PlanningSlug, DirectiveId, Canned.ProjectId);

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

    private static string Text(CallToolResult result) =>
        ((TextContentBlock)result.Content[0]).Text;

    private DirectiveTools Tool() => new(_api, new MutationGuard(_api), new ProjectResolver(_api));
}
