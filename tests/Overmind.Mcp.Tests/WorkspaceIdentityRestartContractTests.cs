using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class WorkspaceIdentityRestartContractTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("overmind-restart-home").FullName;
    private readonly StubOvermindApi _stub = StubOvermindApi.Start();
    private readonly ConcurrentQueue<Guid> _registeredSessions = new();
    private readonly List<McpProcessDriver> _drivers = [];

    public WorkspaceIdentityRestartContractTests()
    {
        _stub.RouteGet("/api/health", Canned.HealthJson);
        _stub.Route("POST", "/api/agent/sessions", _ =>
        {
            var id = Guid.NewGuid();
            _registeredSessions.Enqueue(id);
            return new StubOvermindApi.Response(201, $$"""{"id":"{{id}}"}""");
        });
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
    public async Task RestartWithoutExplicitAlias_RegistersUnderTheSavedWorkspaceIdentity()
    {
        await SetWorkspaceIdentity("mark-wolff", "claude");

        var second = await StartAdapter();
        var sessionId = _registeredSessions.ToArray()[1];

        await WaitForRequest(
            r => r.Path == $"/api/agent/sessions/{sessionId}/identity",
            "the restored identity call");

        var restored = JsonNode.Parse(RequestBody($"/api/agent/sessions/{sessionId}/identity"))!;
        restored["alias"]!.GetValue<string>().ShouldBe("mark-wolff");
        restored["vendor"]!.GetValue<string>().ShouldBe("claude");

        // The alias is claimed through the host's liveness door, never smuggled into the register call.
        JsonNode.Parse(RegistrationBody(1))!["alias"]!.GetValue<string>().ShouldBe("agent");

        second.Stderr.ShouldContain("workspace identity restored: mark-wolff by claude");
        await second.ListToolsAsync();

        var saved = ReadWorkspaceIdentity();
        saved["alias"]!.GetValue<string>().ShouldBe("mark-wolff");
        saved["sessionId"]!.GetValue<string>().ShouldBe(sessionId.ToString());
    }

    [Fact]
    public async Task RestartWithLiveHolderOnTheSavedAlias_FallsBackToEnvAndStillServes()
    {
        await SetWorkspaceIdentity("mark-wolff", "claude");

        _stub.Route("POST", "/api/agent/sessions/[^/]+/identity", _ =>
            new StubOvermindApi.Response(
                409,
                """{"title":"AliasHeld","detail":"Alias 'mark-wolff' is held by the alive session 'mark-wolff' """
                + """by 'claude'; a stale or ended holder frees the alias.","status":409}"""));

        var second = await StartAdapter();
        var sessionId = _registeredSessions.ToArray()[1];

        await WaitForRequest(r => r.Path == $"/api/agent/sessions/{sessionId}/identity", "the refused restore call");

        second.Stderr.ShouldContain(
            "workspace identity 'mark-wolff' by 'claude' is held by an alive session, registering as 'agent'");
        second.HasExited.ShouldBeFalse();

        var tools = (await second.ListToolsAsync())["result"]!["tools"]!.AsArray();
        tools.Count.ShouldBeGreaterThan(0);

        ReadWorkspaceIdentity()["alias"]!.GetValue<string>().ShouldBe("mark-wolff");
    }

    [Fact]
    public async Task RestartWithExplicitAlias_WinsOverTheWorkspaceFile()
    {
        await SetWorkspaceIdentity("mark-wolff", "claude");

        var second = await StartAdapter(alias: "crew-otter");
        var sessionId = _registeredSessions.ToArray()[1];

        JsonNode.Parse(RegistrationBody(1))!["alias"]!.GetValue<string>().ShouldBe("crew-otter");
        _stub.Requests.ShouldNotContain(r => r.Path == $"/api/agent/sessions/{sessionId}/identity");
        second.Stderr.ShouldNotContain("workspace identity restored");
        await second.ListToolsAsync();

        ReadWorkspaceIdentity()["alias"]!.GetValue<string>().ShouldBe("crew-otter");
    }

    private async Task SetWorkspaceIdentity(string alias, string vendor)
    {
        var first = await StartAdapter();

        var result = await first.CallToolAsync("session_identity_set", new JsonObject
        {
            ["alias"] = alias,
            ["vendor"] = vendor,
        });

        Canned.ToolResultText(result).ShouldContain($"Identity set to '{alias}' by '{vendor}'");

        var saved = ReadWorkspaceIdentity();
        saved["alias"]!.GetValue<string>().ShouldBe(alias);
        saved["vendor"]!.GetValue<string>().ShouldBe(vendor);

        first.Dispose();
    }

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

    private async Task<McpProcessDriver> StartAdapter(string alias = "")
    {
        var driver = McpProcessDriver.Start(_stub.BaseUrl, new Dictionary<string, string>
        {
            ["OVERMIND_ALIAS"] = alias,
            ["OVERMIND_VENDOR"] = string.Empty,
            ["HOME"] = _home,
            ["USERPROFILE"] = _home,
            ["DOTNET_CLI_HOME"] = _home,
        });
        _drivers.Add(driver);
        await driver.InitializeAsync();
        return driver;
    }

    private string RegistrationBody(int index) =>
        _stub.Requests
            .Where(r => r.Method == "POST" && r.Path == "/api/agent/sessions")
            .Select(r => r.Body)
            .ToArray()[index];

    private string RequestBody(string path) =>
        _stub.Requests.Single(r => r.Method == "POST" && r.Path == path).Body;

    private JsonNode ReadWorkspaceIdentity()
    {
        var file = Directory.GetFiles(Path.Combine(_home, ".overmind", "identity"), "workspace-*.json").Single();
        return JsonNode.Parse(File.ReadAllText(file))!;
    }

    private async Task WaitForRequest(Func<StubOvermindApi.Request, bool> matches, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!_stub.Requests.Any(matches) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        _stub.Requests.Any(matches).ShouldBeTrue($"no request for {what}");
    }
}
