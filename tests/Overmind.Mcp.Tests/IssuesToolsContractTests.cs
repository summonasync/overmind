using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class IssuesToolsContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        RegisterIssueRoutes(stub);
    }

    private static void RegisterIssueRoutes(StubOvermindApi stub)
    {
        stub.Route("POST", $"/api/agent/projects/{Canned.ProjectId}/issues", 200, Canned.IssueCreatedJson);
        stub.RouteGet($"/api/agent/projects/{Canned.ProjectId}/issues", Canned.IssuesListJson);
        stub.Route("GET", $"/api/agent/issues/{Canned.IssueId}", 200, Canned.IssueCardJson);
        stub.Route(
            "POST",
            $"/api/agent/issues/{Canned.IssueId}/append",
            200,
            Canned.IssueAppendEchoJson);
        stub.Route(
            "POST",
            $"/api/agent/issues/{Canned.IssueId}/status",
            200,
            Canned.IssueStatusEchoJson);
        stub.Route(
            "POST",
            $"/api/agent/issues/{Canned.IssueId}/promote",
            200,
            Canned.IssuePromoteEchoJson);
    }

    [Fact]
    public async Task ToolsList_ContainsAllSixIssueTools()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        foreach (var name in new[]
                 {
                     "issues_create", "issues_list", "issues_get",
                     "issues_append", "issues_set_status", "issues_promote",
                 })
        {
            tools.ShouldContainKey(name);
            tools[name]!["description"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task IssuesCreate_Description_CarriesProtocolAndTemplate()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        var description = tools["issues_create"]!["description"]!.GetValue<string>();
        description.ShouldContain("do not touch their repo");
        description.ShouldContain("issues_append");
        description.ShouldContain("triages/promotes");
        description.ShouldContain("## Repro");
        description.ShouldContain("notification-center");
        description.ShouldContain("'issue' (default)");
        description.ShouldContain("'request'");
        description.ShouldContain("both promote into tasks the same way");
    }

    [Fact]
    public async Task IssuesPromote_Description_IsKindAgnostic()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        var description = tools["issues_promote"]!["description"]!.GetValue<string>();
        description.ShouldContain("an issue or a request");
        description.ShouldNotContain("a bug report");
    }

    [Fact]
    public async Task IssuesCreate_PostsIdentityAndReturnsCompactEcho()
    {
        var response = await Driver.CallToolAsync("issues_create", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["authorProjectId"] = Canned.IssueAuthorProjectId.ToString(),
            ["title"] = "  Crash on save  ",
            ["body"] = "  ## Repro\nOpen the file.  "
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/issues", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/projects/{Canned.ProjectId}/issues");
        var body = JsonNode.Parse(request.Body)!;
        body["authorProjectId"]!.GetValue<string>().ShouldBe(Canned.IssueAuthorProjectId.ToString());
        body["title"]!.GetValue<string>().ShouldBe("Crash on save");
        body["body"]!.GetValue<string>().ShouldBe("## Repro\nOpen the file.");
        body.AsObject().ShouldNotContainKey("kind");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.AsObject().Select(p => p.Key).ToArray().ShouldBe(["id", "status", "title", "kind"]);
        echo["title"]!.GetValue<string>().ShouldBe("Crash on save");
        echo["kind"]!.GetValue<string>().ShouldBe("issue");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task IssuesCreate_RequestKind_IsPassedThroughAndEchoed()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/issues",
            200,
            Canned.RequestCreatedJson);

        var response = await Driver.CallToolAsync("issues_create", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["authorProjectId"] = Canned.IssueAuthorProjectId.ToString(),
            ["title"] = "Add dark mode",
            ["body"] = "## Expected\nA dark theme.",
            ["kind"] = " Request "
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/issues", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!;
        body["title"]!.GetValue<string>().ShouldBe("Add dark mode");
        body["kind"]!.GetValue<string>().ShouldBe("request");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.AsObject().Select(p => p.Key).ToArray().ShouldBe(["id", "status", "title", "kind"]);
        echo["kind"]!.GetValue<string>().ShouldBe("request");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task IssuesCreate_JunkKind_RejectedWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("issues_create", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["authorProjectId"] = Canned.IssueAuthorProjectId.ToString(),
            ["title"] = "Crash on save",
            ["body"] = "## Repro\nOpen the file.",
            ["kind"] = "banana"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Invalid kind 'banana'");
        text.ShouldContain("issue, request");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/issues", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Theory]
    [InlineData("title")]
    [InlineData("body")]
    public async Task IssuesCreate_BlankField_RejectedWithoutHttpCall(string field)
    {
        var call = new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["authorProjectId"] = Canned.IssueAuthorProjectId.ToString(),
            ["title"] = "Crash on save",
            ["body"] = "## Repro\nOpen the file."
        };
        call[field] = "   ";

        var response = await Driver.CallToolAsync("issues_create", call);

        var text = ToolErrorText(response);
        text.ShouldContain($"{field} is required");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/issues", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task IssuesList_PassesFiltersAndReturnsRows()
    {
        var response = await Driver.CallToolAsync("issues_list", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["status"] = "open",
            ["authorProjectId"] = Canned.IssueAuthorProjectId.ToString()
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "GET" && r.Path.EndsWith("/issues", StringComparison.Ordinal));
        request.Query.ShouldContain("status=open");
        request.Query.ShouldContain($"authorProjectId={Canned.IssueAuthorProjectId}");
        request.Query.ShouldNotContain("kind");

        var rows = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray();
        rows.ShouldHaveSingleItem();
        rows[0]!["title"]!.GetValue<string>().ShouldBe("Crash on save");
        rows[0]!["appendCount"]!.GetValue<int>().ShouldBe(1);
        JsonNode.Parse(Canned.ToolResultText(response))!.ToJsonString().ShouldNotContain("secret body");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task IssuesList_KindFilter_ReachesQuery()
    {
        var response = await Driver.CallToolAsync("issues_list", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["kind"] = "request"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "GET" && r.Path.EndsWith("/issues", StringComparison.Ordinal));
        request.Query.ShouldContain("kind=request");

        var rows = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray();
        rows.ShouldHaveSingleItem();
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task IssuesList_JunkKind_RejectedWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("issues_list", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["kind"] = "banana"
        });

        ToolErrorText(response).ShouldContain("Invalid kind 'banana'");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "GET" && r.Path.EndsWith("/issues", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task IssuesGet_ReturnsFullCardWithTrail()
    {
        var response = await Driver.CallToolAsync("issues_get", new JsonObject
        {
            ["issueId"] = Canned.IssueId.ToString()
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "GET" && r.Path.StartsWith("/api/agent/issues/", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/issues/{Canned.IssueId}");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["title"]!.GetValue<string>().ShouldBe("Crash on save");
        echo["appends"]!.AsArray().ShouldHaveSingleItem();
        echo["appends"]![0]!["text"]!.GetValue<string>().ShouldBe("still reproducible");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task IssuesAppend_PostsAndReturnsEntry()
    {
        var response = await Driver.CallToolAsync("issues_append", new JsonObject
        {
            ["issueId"] = Canned.IssueId.ToString(),
            ["authorProjectId"] = Canned.IssueAuthorProjectId.ToString(),
            ["text"] = "  still reproducible  "
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/append", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/issues/{Canned.IssueId}/append");
        var body = JsonNode.Parse(request.Body)!;
        body["authorProjectId"]!.GetValue<string>().ShouldBe(Canned.IssueAuthorProjectId.ToString());
        body["text"]!.GetValue<string>().ShouldBe("still reproducible");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.AsObject().Select(p => p.Key).ToArray()
            .ShouldBe(["id", "authorProjectId", "text", "createdAt"]);
        echo["text"]!.GetValue<string>().ShouldBe("still reproducible");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task IssuesAppend_BlankText_RejectedWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("issues_append", new JsonObject
        {
            ["issueId"] = Canned.IssueId.ToString(),
            ["authorProjectId"] = Canned.IssueAuthorProjectId.ToString(),
            ["text"] = "   "
        });

        ToolErrorText(response).ShouldContain("text is required");
        Stub.Requests.ShouldNotContain(r =>
            r.Path.EndsWith("/append", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task IssuesSetStatus_PostsStatusAndSource()
    {
        var response = await Driver.CallToolAsync("issues_set_status", new JsonObject
        {
            ["issueId"] = Canned.IssueId.ToString(),
            ["status"] = "Triaged",
            ["source"] = "agent",
            ["logNote"] = "accepted for protocol symmetry"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/status", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!;
        body["status"]!.GetValue<string>().ShouldBe("triaged");
        body["source"]!.GetValue<string>().ShouldBe("Agent");
        body.AsObject().ShouldNotContainKey("logNote");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.AsObject().Select(p => p.Key).ToArray().ShouldBe(["id", "status", "source", "closedAt"]);
        echo["status"]!.GetValue<string>().ShouldBe("triaged");
        Driver.HasExited.ShouldBeFalse();
    }

    [Theory]
    [InlineData("promoted")]
    [InlineData("banana")]
    public async Task IssuesSetStatus_InvalidStatus_RejectedWithoutHttpCall(string status)
    {
        var response = await Driver.CallToolAsync("issues_set_status", new JsonObject
        {
            ["issueId"] = Canned.IssueId.ToString(),
            ["status"] = status,
            ["source"] = "Agent"
        });

        var text = ToolErrorText(response);
        text.ShouldContain($"Invalid issue status '{status}'");
        Stub.Requests.ShouldNotContain(r =>
            r.Path.EndsWith("/status", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task IssuesSetStatus_InvalidSource_RejectedWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("issues_set_status", new JsonObject
        {
            ["issueId"] = Canned.IssueId.ToString(),
            ["status"] = "triaged",
            ["source"] = "cron"
        });

        ToolErrorText(response).ShouldContain("Invalid source 'cron'");
        Stub.Requests.ShouldNotContain(r =>
            r.Path.EndsWith("/status", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task IssuesPromote_PostsFeatureSlugAndSource()
    {
        var response = await Driver.CallToolAsync("issues_promote", new JsonObject
        {
            ["issueId"] = Canned.IssueId.ToString(),
            ["featureSlug"] = " 050-crash-fix ",
            ["source"] = "Agent"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/promote", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!;
        body["featureSlug"]!.GetValue<string>().ShouldBe("050-crash-fix");
        body["source"]!.GetValue<string>().ShouldBe("Agent");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.AsObject().Select(p => p.Key).ToArray()
            .ShouldBe(["id", "status", "artifactId", "featureId", "artifactVersion"]);
        echo["artifactVersion"]!.GetValue<int>().ShouldBe(1);
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task IssuesPromote_BlankFeatureSlug_RejectedWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("issues_promote", new JsonObject
        {
            ["issueId"] = Canned.IssueId.ToString(),
            ["featureSlug"] = "   ",
            ["source"] = "Agent"
        });

        ToolErrorText(response).ShouldContain("featureSlug is required");
        Stub.Requests.ShouldNotContain(r =>
            r.Path.EndsWith("/promote", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task IssuesPromote_GatedTransition_SurfacesServerDetail()
    {
        Stub.Route(
            "POST",
            $"/api/agent/issues/{Canned.IssueId}/promote",
            400,
            Canned.IssuePromoteGatedJson);

        var response = await Driver.CallToolAsync("issues_promote", new JsonObject
        {
            ["issueId"] = Canned.IssueId.ToString(),
            ["featureSlug"] = "050-crash-fix",
            ["source"] = "Agent"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Issue cannot move from 'promoted' to 'promoted'");
        text.ShouldContain("no reopen");
        Driver.HasExited.ShouldBeFalse();
    }

    private static string ToolErrorText(JsonNode response)
    {
        var result = response["result"];
        result.ShouldNotBeNull("expected a JSON-RPC result, got: " + response.ToJsonString());
        result!["isError"]!.GetValue<bool>().ShouldBeTrue("expected a tool error, got: " + response.ToJsonString());
        return result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
