using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

// 266/T3 row 8 — the replies poll used to spill a 79KB single-line result the caller could not read
// in context. Measured at the Host: without `since` there is no window at all, so the size is the
// stored notification texts multiplied by the whole reply backlog. The reply SET is unchanged; the
// row shape is projected, and full=true is the untouched path.
public class RepliesProjectionContractTests : McpSessionTestBase
{
    private const string ShortReplyJson =
        """{"items":[{"id":"13131313-1313-1313-1313-131313131313","projectId":"11111111-1111-1111-1111-111111111111","kind":"question","featureSlug":"demo-feature","tab":"spec","text":"Spec gate wants your call on the persistence model.","agent":"coder","createdAt":"2026-09-20T10:00:00Z","readAt":"2026-09-20T10:05:00Z","replyText":"Ship it as planned.","repliedAt":"2026-09-20T10:05:00Z","hiddenAt":null}]}""";

    // The gate-question payload the survey measured: the stored notification text is what the agent
    // wrote, and it is what ate the context window (4000 chars here, 79KB across a week of them).
    private static readonly string VerboseReplyJson = new JsonObject
    {
        ["items"] = new JsonArray(new JsonObject
        {
            ["id"] = "13131313-1313-1313-1313-131313131313",
            ["projectId"] = "11111111-1111-1111-1111-111111111111",
            ["kind"] = "approval",
            ["featureSlug"] = "demo-feature",
            ["tab"] = "plan",
            ["text"] = new string('p', 4_000),
            ["agent"] = "coder",
            ["createdAt"] = "2026-09-20T10:00:00Z",
            ["readAt"] = null,
            ["replyText"] = "approved via telegram button by @johnbulino",
            ["repliedAt"] = "2026-09-20T11:00:00Z",
            ["hiddenAt"] = null,
        }),
    }.ToJsonString();

    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        stub.RouteGet("/api/health", Canned.HealthJson);
        stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);
        stub.RouteGet("/api/projects", Canned.ProjectsJson);
        stub.Route("GET", "/api/agent/notifications/replies", req =>
            req.Query.Contains("featureSlug=verbose")
                ? new StubOvermindApi.Response(200, VerboseReplyJson)
                : new StubOvermindApi.Response(200, ShortReplyJson));
    }

    [Fact]
    public async Task Replies_ByDefault_AreProjectedToTheDecisionKeys()
    {
        var response = await Driver.CallToolAsync("overmind_replies", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
        });

        var payload = JsonNode.Parse(ResultText(response))!.AsObject();
        payload["projection"]!.GetValue<string>().ShouldBe("compact");
        var row = payload["items"]!.AsArray().Single()!.AsObject();
        row["replyText"]!.GetValue<string>().ShouldBe("Ship it as planned.");
        row["id"]!.GetValue<string>().ShouldBe("13131313-1313-1313-1313-131313131313");
        row["repliedAt"]!.GetValue<string>().ShouldBe("2026-09-20T10:05:00Z");
        row["agent"]!.GetValue<string>().ShouldBe("coder");

        // Dropped by design: the project the query already scoped, and the read/hidden stamps.
        row.ContainsKey("projectId").ShouldBeFalse();
        row.ContainsKey("readAt").ShouldBeFalse();
        row.ContainsKey("hiddenAt").ShouldBeFalse();
    }

    [Fact]
    public async Task Replies_LongNotificationText_IsExcerptedAndSaysSo()
    {
        var response = await Driver.CallToolAsync("overmind_replies", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = "verbose",
        });

        var payload = JsonNode.Parse(ResultText(response))!.AsObject();
        payload["textExcerpted"]!.GetValue<int>().ShouldBe(1);
        payload["fullRows"]!.GetValue<string>().ShouldContain("full=true");
        var row = payload["items"]!.AsArray().Single()!.AsObject();
        row["text"]!.GetValue<string>().Length.ShouldBe(200);
        row["replyText"]!.GetValue<string>().ShouldBe("approved via telegram button by @johnbulino");
    }

    [Fact]
    public async Task Replies_FullTrue_ReplaysTheStoredRowsUntouched()
    {
        var response = await Driver.CallToolAsync("overmind_replies", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["full"] = true,
        });

        var text = ResultText(response);
        JsonNode.Parse(text)!.AsObject()["items"]!.AsArray().Single()!.AsObject()["projectId"]!
            .GetValue<string>().ShouldBe("11111111-1111-1111-1111-111111111111");
        JsonNode.Parse(text)!.AsObject().ContainsKey("projection").ShouldBeFalse();
    }

    private static string ResultText(JsonNode response)
    {
        response["error"].ShouldBeNull();
        return response["result"]!["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
