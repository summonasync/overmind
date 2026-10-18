using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class LaneToolContractTests : IAsyncLifetime
{
    private static readonly string LanesRoute =
        $"/api/agent/projects/{Canned.ProjectId}/planning/lanes";

    private static readonly string RosterJson = new System.Text.Json.Nodes.JsonObject
    {
        ["lanes"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = "frontend",
            ["contour"] = "web/src",
            ["serialOrder"] = 1,
            ["openFeatureCount"] = 1,
            ["holder"] = null
        })
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
    public async Task MissingProjectId_ThrowsThePlanningGuard(bool mutating)
    {
        var ex = await (mutating
            ? Should.ThrowAsync<McpException>(() => Tool().SetLanes(default))
            : Should.ThrowAsync<McpException>(() => Tool().ListLanes()));

        ex.Message.ShouldBe(
            "projectId is required for planning_* tools — entries tools resolve the project from cwd, planning tools do not.");
    }

    [Fact]
    public async Task SetLanes_PutsTheFullRosterThrough()
    {
        _stub.Route("PUT", LanesRoute, 200, RosterJson);

        var result = await Tool().SetLanes(
            Element("""[{"name":"frontend","contour":"web/src","serialOrder":1,"priority":0}]"""),
            Canned.ProjectId);

        var request = _stub.Requests.Single(r => r.Method == "PUT");
        request.Path.ShouldBe(LanesRoute);
        request.Body.ShouldContain("\"name\":\"frontend\"");
        request.Body.ShouldContain("\"contour\":\"web/src\"");
        request.Body.ShouldContain("\"serialOrder\":1");
        request.Body.ShouldContain("\"priority\":0");
        Text(result).ShouldBe("Lane roster holds 1 lane(s): frontend.");
    }

    [Fact]
    public async Task SetLanes_EmptyArrayClearsTheRoster()
    {
        _stub.Route("PUT", LanesRoute, 200, """{"lanes":[]}""");

        var result = await Tool().SetLanes(Element("[]"), Canned.ProjectId);

        Text(result).ShouldBe("Lane roster cleared.");
    }

    [Fact]
    public async Task SetLanes_NonArrayPayload_IsRefusedBeforeTheWire()
    {
        var ex = await Should.ThrowAsync<McpException>(
            () => Tool().SetLanes(Element("\"frontend\""), Canned.ProjectId));

        ex.Message.ShouldBe("lanes must be an array of lane objects.");
        _stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetLanes_ItemWithoutName_IsRefusedBeforeTheWire()
    {
        var ex = await Should.ThrowAsync<McpException>(
            () => Tool().SetLanes(Element("""[{"contour":"web"}]"""), Canned.ProjectId));

        ex.Message.ShouldBe("lanes[0]: name is required.");
        _stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetLanes_StringEncodedArray_IsDecoded()
    {
        _stub.Route("PUT", LanesRoute, 200, RosterJson);

        var encoded = JsonValue.Create("""[{"name":"frontend"}]""")!.ToJsonString();
        await Tool().SetLanes(Element(encoded), Canned.ProjectId);

        _stub.Requests.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ListLanes_PassesTheHostPayloadThrough()
    {
        _stub.RouteGet(LanesRoute, RosterJson);

        var result = await Tool().ListLanes(Canned.ProjectId);

        result.GetProperty("lanes").GetArrayLength().ShouldBe(1);
        result.GetProperty("lanes")[0].GetProperty("name").GetString().ShouldBe("frontend");
        result.GetProperty("lanes")[0].GetProperty("openFeatureCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task ListLanes_HostFailure_SurfacesAsMcpException()
    {
        _stub.Route("GET", LanesRoute, 404, """{"title":"NotFound","detail":"nope"}""");

        var ex = await Should.ThrowAsync<McpException>(() => Tool().ListLanes(Canned.ProjectId));

        ex.Message.ShouldNotBeEmpty();
    }

    private static string Text(CallToolResult result) =>
        ((TextContentBlock)result.Content[0]).Text;

    private static JsonElement Element(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private LaneTools Tool() => new(_api, new MutationGuard(_api));
}
