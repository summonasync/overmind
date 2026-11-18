using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class OnboardIdentityChurnContractTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("overmind-onboard-identity").FullName;
    private readonly StubOvermindApi _stub = StubOvermindApi.Start();
    private readonly List<McpProcessDriver> _drivers = [];

    public OnboardIdentityChurnContractTests()
    {
        _stub.RouteGet("/api/health", Canned.HealthJson);
        _stub.Route("POST", "/api/agent/sessions", req =>
            new StubOvermindApi.Response(201, $$"""{"id":"{{Guid.NewGuid()}}"}"""));
        _stub.Route("POST", "/api/agent/sessions/[^/]+/end", 204, "");
        _stub.Route("POST", "/api/agent/sessions/[^/]+/identity", EchoIdentity);
    }

    public void Dispose()
    {
        foreach (var driver in _drivers)
        {
            driver.Dispose();
        }

        _stub.Stop();
        if (Directory.Exists(_home))
        {
            Directory.Delete(_home, recursive: true);
        }
    }

    [Fact]
    public async Task Onboard_PairHeldByTheSessionRow_RendersItAndKeepsThePlaceholderInstructionQuiet()
    {
        Route(IdentityPayload("mark-wolff", "claude"));
        var driver = await StartAdapter();

        var text = Canned.ToolResultText(await driver.CallToolAsync("overmind_onboard", new JsonObject()));

        text.ShouldContain(
            "Agent identity: alias 'mark-wolff', vendor 'claude' (source: session) — pass this pair to " +
            "planning_claim_feature");
        text.ShouldContain("in force on your session row");
        text.ShouldNotContain("Next step:");
    }

    [Fact]
    public async Task Onboard_PlaceholderSessionRow_KeepsTheInventYourAliasInstruction()
    {
        Route(Canned.OnboardAgentIdentityJson);
        var driver = await StartAdapter();

        var text = Canned.ToolResultText(await driver.CallToolAsync("overmind_onboard", new JsonObject()));

        text.ShouldContain("Agent identity: alias 'agent', vendor 'unknown' (source: placeholder)");
        text.ShouldContain("Next step: 'agent' is the shared placeholder");
        text.ShouldNotContain("in force on your session row");
    }

    [Fact]
    public async Task Onboard_EnvironmentPairNamedByTheSessionRow_RendersSourceEnv()
    {
        Route(IdentityPayload("crew-otter", "qwen"));
        var driver = await StartAdapter(alias: "crew-otter", vendor: "qwen");

        var text = Canned.ToolResultText(await driver.CallToolAsync("overmind_onboard", new JsonObject()));

        text.ShouldContain("Agent identity: alias 'crew-otter', vendor 'qwen' (source: env)");
        text.ShouldNotContain("Next step:");
    }

    [Fact]
    public async Task Onboard_IdentitySetInThisSession_RendersSourceSlot()
    {
        var driver = await StartAdapter();
        await driver.CallToolAsync("session_identity_set", new JsonObject
        {
            ["alias"] = "mister-po",
            ["vendor"] = "qwen",
        });
        Route(IdentityPayload("mister-po", "qwen"));

        var text = Canned.ToolResultText(await driver.CallToolAsync("overmind_onboard", new JsonObject()));

        text.ShouldContain("Agent identity: alias 'mister-po', vendor 'qwen' (source: slot)");
        text.ShouldNotContain("Next step:");
    }

    [Fact]
    public async Task Onboard_ChurnRows_RendersHolderAgesSlugsAndTheTakeoverDoor()
    {
        Route(WithChurn(
            ChurnRow("mark-wolff", "claude", "ended", 90, ["010-a", "011-b"], 2),
            ChurnRow("van-darkholme", "qwen", "stale", 1500, ["020-c"], 12)));
        var driver = await StartAdapter();

        var text = Canned.ToolResultText(await driver.CallToolAsync("overmind_onboard", new JsonObject()));

        text.ShouldContain("Claim churn: holders that stopped answering while still holding claims:");
        text.ShouldContain("- 'mark-wolff' by 'claude' (ended, silent 1h 30m): holds 010-a, 011-b");
        text.ShouldContain("- 'van-darkholme' by 'qwen' (stale, silent 1d 1h): holds 020-c +11 more");
        text.ShouldContain("planning_claim_feature is the only take-over door");
    }

    [Fact]
    public async Task Onboard_ChurnBlockAbsentOrEmpty_RendersNoChurnLines()
    {
        var driver = await StartAdapter();

        Route(Canned.OnboardAgentIdentityJson);
        var absent = Canned.ToolResultText(await driver.CallToolAsync("overmind_onboard", new JsonObject()));

        Route(WithChurn());
        var empty = Canned.ToolResultText(await driver.CallToolAsync("overmind_onboard", new JsonObject()));

        absent.ShouldNotContain("Claim churn");
        empty.ShouldNotContain("Claim churn");
    }

    [Fact]
    public async Task Onboard_Brief_RendersNeitherIdentityNorChurn()
    {
        Route(WithChurn(ChurnRow("mark-wolff", "claude", "ended", 20, ["010-a"], 1)));
        var driver = await StartAdapter();

        var text = Canned.ToolResultText(await driver.CallToolAsync(
            "overmind_onboard", new JsonObject { ["brief"] = true }));

        text.ShouldContain("Project: alpha");
        text.ShouldNotContain("Agent identity:");
        text.ShouldNotContain("Claim churn");
    }

    private static string IdentityPayload(string alias, string vendor) =>
        Canned.OnboardAgentIdentityJson
            .Replace("\"alias\":\"agent\"", $"\"alias\":\"{alias}\"")
            .Replace("\"vendor\":\"unknown\"", $"\"vendor\":\"{vendor}\"");

    private static JsonObject ChurnRow(
        string alias, string vendor, string liveness, int deathAgeMinutes, string[] slugs, int slugsTotal) =>
        new()
        {
            ["alias"] = alias,
            ["vendor"] = vendor,
            ["liveness"] = liveness,
            ["unseenSince"] = "2026-11-17T10:00:00Z",
            ["deathAgeMinutes"] = deathAgeMinutes,
            ["slugs"] = new JsonArray([.. slugs.Select(slug => (JsonNode)JsonValue.Create(slug)!)]),
            ["slugsTotal"] = slugsTotal,
        };

    private static string WithChurn(params JsonObject[] rows)
    {
        var payload = JsonNode.Parse(Canned.OnboardAgentIdentityJson)!;
        payload["claimChurn"] = new JsonObject { ["sessions"] = new JsonArray([.. rows]) };
        return payload.ToJsonString();
    }

    private void Route(string onboardJson) => _stub.Route("POST", "/api/onboard", 200, onboardJson);

    private StubOvermindApi.Response EchoIdentity(StubOvermindApi.Request request)
    {
        var submitted = JsonNode.Parse(request.Body)!;
        var echo = new JsonObject
        {
            ["session"] = new JsonObject
            {
                ["id"] = request.Path.Split('/')[4],
                ["alias"] = submitted["alias"]!.GetValue<string>(),
                ["vendor"] = submitted["vendor"]!.GetValue<string>(),
            },
            ["warnings"] = new JsonArray(),
        };
        return new StubOvermindApi.Response(200, echo.ToJsonString());
    }

    private async Task<McpProcessDriver> StartAdapter(string alias = "", string vendor = "")
    {
        var driver = McpProcessDriver.Start(_stub.BaseUrl, new Dictionary<string, string>
        {
            ["OVERMIND_ALIAS"] = alias,
            ["OVERMIND_VENDOR"] = vendor,
            ["HOME"] = _home,
            ["USERPROFILE"] = _home,
            ["DOTNET_CLI_HOME"] = _home,
        });
        _drivers.Add(driver);
        await driver.InitializeAsync();
        return driver;
    }
}
