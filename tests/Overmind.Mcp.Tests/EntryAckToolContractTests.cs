using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class EntryAckToolContractTests : IAsyncLifetime
{
    private static readonly string AckRoute =
        $"/api/agent/projects/{Canned.ProjectId}/entries/{Canned.EntryId}/ack";

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

    [Fact]
    public async Task MissingProjectId_ThrowsThePlanningGuard()
    {
        var ex = await Should.ThrowAsync<McpException>(() => Tool().AckEntry(Canned.EntryId));

        ex.Message.ShouldBe(
            "projectId is required for planning_* tools — entries tools resolve the project from cwd, planning tools do not.");
        _stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AckEntry_PostsTheAck_AndReportsIt()
    {
        _stub.Route("POST", AckRoute, 200,
            $$"""{"entryId":"{{Canned.EntryId}}","ack":true,"alias":"Dan","vendor":"qwen","at":"2026-10-17T12:05:00Z"}""");

        var result = await Tool().AckEntry(Canned.EntryId, Canned.ProjectId);

        var request = _stub.Requests.Single(r => r.Method == "POST");
        request.Path.ShouldBe(AckRoute);
        JsonNode.Parse(request.Body)!.AsObject().Count.ShouldBe(0);
        Text(result).ShouldBe($"Entry {Canned.EntryId} acknowledged.");
    }

    [Fact]
    public async Task AckEntry_AlreadyAcked_ReportsTheNoOp()
    {
        _stub.Route("POST", AckRoute, 200,
            $$"""{"entryId":"{{Canned.EntryId}}","ack":false,"alias":"Dan","vendor":"qwen","at":"2026-10-17T12:05:00Z"}""");

        var result = await Tool().AckEntry(Canned.EntryId, Canned.ProjectId);

        Text(result).ShouldBe($"Entry {Canned.EntryId} was already acknowledged — no change.");
    }

    [Fact]
    public async Task AckEntry_HostFailure_SurfacesAsMcpException()
    {
        _stub.Route("POST", AckRoute, 404, """{"title":"MissingEntry","detail":"Entry 'dead' was not found."}""");

        var ex = await Should.ThrowAsync<McpException>(
            () => Tool().AckEntry(Canned.EntryId, Canned.ProjectId));

        ex.Message.ShouldContain("was not found");
    }

    private static string Text(CallToolResult result) =>
        ((TextContentBlock)result.Content[0]).Text;

    private EntryAckTools Tool() => new(_api, new MutationGuard(_api));
}
