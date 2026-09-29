using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class NotificationToolContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub) => RegisterDefaultRoutes(stub);

    [Fact]
    public async Task ToolsList_NotificationTools_DescribeTheHumanChannel()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        var notify = tools["overmind_notify"]!["description"]!.GetValue<string>();
        notify.ShouldContain("human");
        notify.ShouldContain("info");
        notify.ShouldContain("question");
        notify.ShouldContain("approval");
        notify.ShouldContain("overmind_replies");
        notify.ShouldContain("quote the url in chat when asking for an approval");

        tools["overmind_replies"]!["description"]!.GetValue<string>().ShouldContain("answers");
    }

    [Fact]
    public async Task Notify_HostResponseUrl_ReachesTheToolResultUnmodified()
    {
        Stub.Route("POST", "/api/agent/notifications", 201, Canned.NotificationWithUrlJson);

        var response = await Driver.CallToolAsync("overmind_notify", new JsonObject
        {
            ["kind"] = "approval",
            ["text"] = "Spec gate wants your call on the persistence model.",
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tab"] = "spec"
        });

        Canned.ToolResultText(response).ShouldContain(Canned.NotificationUrl);
    }

    [Fact]
    public async Task Notify_ExplicitProject_PostsKindTextAndDeepLinkTarget()
    {
        var response = await Driver.CallToolAsync("overmind_notify", new JsonObject
        {
            ["kind"] = "question",
            ["text"] = "Spec gate wants your call on the persistence model.",
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tab"] = "spec",
            ["from"] = "coder"
        });

        var request = FindRequest("POST", "/api/agent/notifications");
        request.Path.ShouldBe("/api/agent/notifications");
        var body = JsonNode.Parse(request.Body)!.AsObject();
        body["kind"]!.GetValue<string>().ShouldBe("question");
        body["text"]!.GetValue<string>().ShouldBe("Spec gate wants your call on the persistence model.");
        body["projectId"]!.GetValue<string>().ShouldBe(Canned.ProjectId.ToString());
        body["featureSlug"]!.GetValue<string>().ShouldBe(Canned.PlanningSlug);
        body["tab"]!.GetValue<string>().ShouldBe("spec");
        body["from"]!.GetValue<string>().ShouldBe("coder");

        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path == "/api/onboard");
        Canned.ToolResultText(response).ShouldContain("Spec gate wants your call");
    }

    [Fact]
    public async Task Notify_WithoutOptionals_OmitsThemFromThePostedJson()
    {
        await Driver.CallToolAsync("overmind_notify", new JsonObject
        {
            ["kind"] = "info",
            ["text"] = "Deploy finished.",
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var request = FindRequest("POST", "/api/agent/notifications");
        JsonNode.Parse(request.Body)!.AsObject().Select(p => p.Key).ToArray().ShouldBe(
            ["kind", "text", "projectId"], ignoreOrder: true);
    }

    [Fact]
    public async Task Notify_ProjectIdEnvOverride_SkipsOnboard()
    {
        using var driver = McpProcessDriver.Start(Stub.BaseUrl, new Dictionary<string, string>
        {
            ["OVERMIND_PROJECT_ID"] = Canned.ProjectId.ToString()
        });
        await driver.InitializeAsync();

        await driver.CallToolAsync("overmind_notify", new JsonObject
        {
            ["kind"] = "info",
            ["text"] = "Deploy finished."
        });

        var request = Stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/agent/notifications");
        JsonNode.Parse(request.Body)!["projectId"]!.GetValue<string>().ShouldBe(Canned.ProjectId.ToString());
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path == "/api/onboard");
    }

    [Fact]
    public async Task Notify_NoProjectNoEnv_ResolvesThroughOnboardCwd()
    {
        await Driver.CallToolAsync("overmind_notify", new JsonObject
        {
            ["kind"] = "info",
            ["text"] = "Deploy finished."
        });

        var onboard = Stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        var onboardBody = JsonNode.Parse(onboard.Body)!;
        onboardBody["cwd"]!.GetValue<string>().ShouldBe(Environment.CurrentDirectory);
        onboardBody["projectId"].ShouldBeNull();

        var request = FindRequest("POST", "/api/agent/notifications");
        JsonNode.Parse(request.Body)!["projectId"]!.GetValue<string>().ShouldBe(Canned.ProjectId.ToString());
    }

    [Fact]
    public async Task Notify_OnboardFails_ClearErrorTellingAgentToOnboard()
    {
        Stub.Route("POST", "/api/onboard", 500, """{"title":"Internal Server Error","detail":"host exploded","status":500}""");

        var response = await Driver.CallToolAsync("overmind_notify", new JsonObject
        {
            ["kind"] = "info",
            ["text"] = "Deploy finished."
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Could not resolve the project");
        text.ShouldContain("overmind_onboard");
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path == "/api/agent/notifications");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task Notify_UnknownKind_RejectedWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("overmind_notify", new JsonObject
        {
            ["kind"] = "urgent",
            ["text"] = "Deploy finished.",
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Invalid notification kind 'urgent'");
        text.ShouldContain("info, question, approval");
        Stub.Requests.ShouldNotContain(r => r.Path.StartsWith("/api/agent/notifications", StringComparison.Ordinal));
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path == "/api/onboard");
    }

    [Fact]
    public async Task Replies_ExplicitProject_GetsRepliesWithQuery()
    {
        var response = await Driver.CallToolAsync("overmind_replies", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["includeRead"] = false
        });

        var request = FindRequest("GET", "/api/agent/notifications/replies");
        request.Query.ShouldBe($"projectId={Canned.ProjectId}&includeRead=false");
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path == "/api/onboard");

        Canned.ToolResultText(response).ShouldContain("Ship it as planned.");
    }

    [Fact]
    public async Task Replies_IncludeReadOmitted_SendsProjectIdOnly()
    {
        await Driver.CallToolAsync("overmind_replies", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var request = FindRequest("GET", "/api/agent/notifications/replies");
        request.Query.ShouldBe($"projectId={Canned.ProjectId}");
    }

    [Fact]
    public async Task Replies_NoProject_ResolvesThroughOnboard()
    {
        await Driver.CallToolAsync("overmind_replies", new JsonObject());

        Stub.Requests.ShouldContain(r => r.Method == "POST" && r.Path == "/api/onboard");
        var request = FindRequest("GET", "/api/agent/notifications/replies");
        request.Query.ShouldBe($"projectId={Canned.ProjectId}");
    }

    private static string ToolErrorText(JsonNode response)
    {
        var result = response["result"];
        result.ShouldNotBeNull("expected a JSON-RPC result, got: " + response.ToJsonString());
        result!["isError"]!.GetValue<bool>().ShouldBeTrue("expected a tool error, got: " + response.ToJsonString());
        return result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
