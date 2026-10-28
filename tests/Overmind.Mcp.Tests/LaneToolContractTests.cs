using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class LaneToolContractTests : IAsyncLifetime
{
    private static readonly string RosterRoute =
        $"/api/agent/projects/{Canned.ProjectId}/planning/lanes-roster";

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
    public async Task MissingProjectId_ResolvesThroughTheOnboardCwd(bool mutating)
    {
        _stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);
        _stub.Route("PUT", RosterRoute, 200, RosterJson);
        _stub.RouteGet(LanesRoute, RosterJson);

        if (mutating)
        {
            var confirmation = await Tool().SetLanes(Element("""[{"name":"frontend"}]"""));
            Text(confirmation).ShouldBe("Lane roster holds 1 lane(s): frontend.");
        }
        else
        {
            var roster = await Tool().ListLanes();
            roster.GetProperty("lanes").GetArrayLength().ShouldBe(1);
        }

        var onboard = _stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        var body = JsonNode.Parse(onboard.Body)!;
        body["cwd"]!.GetValue<string>().ShouldBe(Environment.CurrentDirectory);
        body["create"]!.GetValue<bool>().ShouldBeFalse();

        _stub.Requests.ShouldContain(r =>
            r.Method == (mutating ? "PUT" : "GET") &&
            r.Path == (mutating ? RosterRoute : LanesRoute));
    }

    [Fact]
    public async Task UnresolvableCwd_ReturnsTheGuidanceWithoutHittingLanes()
    {
        _stub.Route("POST", "/api/onboard", 404, Canned.OnboardUnregisteredCwdJson);

        var ex = await Should.ThrowAsync<McpException>(() => Tool().ListLanes());

        ex.Message.ShouldContain("Could not resolve the project for the working directory");
        ex.Message.ShouldContain("Pass projectId explicitly");
        _stub.Requests.ShouldNotContain(r => r.Path == LanesRoute);
    }

    [Fact]
    public async Task ExplicitProjectId_SkipsTheOnboardRoundTrip()
    {
        _stub.RouteGet(LanesRoute, RosterJson);

        await Tool().ListLanes(Canned.ProjectId);

        _stub.Requests.ShouldNotContain(r => r.Path == "/api/onboard");
    }

    [Fact]
    public async Task SetLanes_PutsTheFullRosterThrough()
    {
        _stub.Route("PUT", RosterRoute, 200, RosterJson);

        var result = await Tool().SetLanes(
            Element("""[{"name":"frontend","contour":"web/src","serialOrder":1,"priority":0}]"""),
            Canned.ProjectId);

        var request = _stub.Requests.Single(r => r.Method == "PUT");
        request.Path.ShouldBe(RosterRoute);
        request.Body.ShouldContain("\"name\":\"frontend\"");
        request.Body.ShouldContain("\"contour\":\"web/src\"");
        request.Body.ShouldContain("\"serialOrder\":1");
        request.Body.ShouldContain("\"priority\":0");
        Text(result).ShouldBe("Lane roster holds 1 lane(s): frontend.");
    }

    [Fact]
    public async Task SetLanes_PutsLiveSurfacesThrough()
    {
        _stub.Route("PUT", RosterRoute, 200, RosterJson);

        var result = await Tool().SetLanes(
            Element("""[{"name":"frontend","liveSurfaces":[{"glob":"web/src/**","owner":"van-darkholme"}]}]"""),
            Canned.ProjectId);

        var request = _stub.Requests.Single(r => r.Method == "PUT");
        request.Body.ShouldContain("\"liveSurfaces\":[{\"glob\":\"web/src/**\",\"owner\":\"van-darkholme\"}]");
        Text(result).ShouldBe("Lane roster holds 1 lane(s): frontend.");
    }

    [Fact]
    public async Task SetLanes_EmptyArray_IsANoOpThatEchoesTheUnchangedRoster()
    {
        _stub.Route("PUT", RosterRoute, 200, """{"lanes":[]}""");

        var result = await Tool().SetLanes(Element("[]"), Canned.ProjectId);

        Text(result).ShouldBe(
            "No lanes in the roster — the payload wrote nothing: an empty payload is a no-op, " +
            "and lanes are born via planning_update_feature (lane=...).");
    }

    [Fact]
    public async Task RemoveLane_DeletesTheLaneThroughTheHostDoor()
    {
        _stub.Route("DELETE", $"{LanesRoute}/frontend", 200, """{"lanes":[]}""");

        var result = await Tool().RemoveLane("frontend", Canned.ProjectId);

        var request = _stub.Requests.Single(r => r.Method == "DELETE");
        request.Path.ShouldBe($"{LanesRoute}/frontend");
        Text(result).ShouldBe("No lane named 'frontend' remains — the roster is empty.");
    }

    [Fact]
    public async Task RemoveLane_NameIsTrimmedAndEscapedForTheRoute()
    {
        _stub.Route("DELETE", $"{LanesRoute}/web%20src", 200, RosterJson);

        await Tool().RemoveLane(" web src ", Canned.ProjectId);

        _stub.Requests.Single(r => r.Method == "DELETE").Path.ShouldBe($"{LanesRoute}/web%20src");
    }

    [Fact]
    public async Task RemoveLane_UnknownLane_EchoesTheSurvivingRoster()
    {
        _stub.Route("DELETE", $"{LanesRoute}/ghost", 200, RosterJson);

        var result = await Tool().RemoveLane("ghost", Canned.ProjectId);

        Text(result).ShouldBe("No lane named 'ghost' remains; the roster holds 1 lane(s): frontend.");
    }

    [Fact]
    public async Task RemoveLane_OccupiedLane_SurfacesTheRefusalDetail()
    {
        _stub.Route(
            "DELETE",
            $"{LanesRoute}/frontend",
            409,
            """{"title":"LaneStillOccupied","detail":"Lane 'frontend' still holds open features: 011-a."}""");

        var ex = await Should.ThrowAsync<McpException>(() => Tool().RemoveLane("frontend", Canned.ProjectId));

        ex.Message.ShouldContain("Lane 'frontend' still holds open features: 011-a");
    }

    [Fact]
    public async Task RemoveLane_BlankName_IsRefusedBeforeTheWire()
    {
        var ex = await Should.ThrowAsync<McpException>(() => Tool().RemoveLane("   ", Canned.ProjectId));

        ex.Message.ShouldBe("name is required: the lane to remove, as shown by planning_list_lanes.");
        _stub.Requests.ShouldBeEmpty();
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
        _stub.Route("PUT", RosterRoute, 200, RosterJson);

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

    private LaneTools Tool() => new(_api, new MutationGuard(_api), new ProjectResolver(_api));
}
