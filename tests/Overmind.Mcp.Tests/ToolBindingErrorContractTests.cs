using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class ToolBindingErrorContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub) => RegisterDefaultRoutes(stub);

    [Fact]
    public async Task WrongParameterName_ReturnsNamedErrorWithToolParameterAndShape()
    {
        var response = await Driver.CallToolAsync("read_entry", new JsonObject
        {
            ["entryIdd"] = Canned.EntryId.ToString()
        });

        var text = ErrorText(response);
        text.ShouldContain("read_entry");
        text.ShouldContain("entryIdd");
        text.ShouldContain("entryId");
        text.ShouldContain("string (uuid)");
        text.ShouldContain("[required]");
    }

    [Fact]
    public async Task NoParameterTool_RejectsUnknownParameterByName()
    {
        var response = await Driver.CallToolAsync("list_projects", new JsonObject
        {
            ["slug"] = "alpha"
        });

        var text = ErrorText(response);
        text.ShouldContain("list_projects");
        text.ShouldContain("slug");
        text.ShouldContain("takes no parameters");
    }

    [Fact]
    public async Task MissingRequiredParameter_ReturnsNamedError()
    {
        var response = await Driver.CallToolAsync("read_entry", new JsonObject());

        ErrorText(response).ShouldContain("entryId is required but was not provided");
    }

    [Fact]
    public async Task WrongValueType_ReturnsNamedErrorWithExpectedType()
    {
        var response = await Driver.CallToolAsync("read_entry", new JsonObject
        {
            ["entryId"] = "not-a-guid"
        });

        ErrorText(response).ShouldContain("entryId expects a UUID string");
    }

    [Fact]
    public async Task ValidCall_IsNotIntercepted()
    {
        var response = await Driver.CallToolAsync("read_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString()
        });

        response["error"].ShouldBeNull();
        response["result"]!["isError"].ShouldBeNull();
        ResultText(response).ShouldContain("Entry body here.");
    }

    // ---- 266/T3 row 1: the near-miss hint on a wrong parameter name ----

    [Fact]
    public async Task WrongParameterName_SingularPluralConfusion_NamesTheRealParameter()
    {
        var response = await Driver.CallToolAsync("wave_chat_unread", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["room"] = "all",
        });

        var text = ErrorText(response);
        text.ShouldContain("room is not a parameter of this tool; did you mean 'rooms'?");
        text.ShouldContain("rooms");
    }

    [Fact]
    public async Task WrongParameterName_NothingClose_IsRefusedWithoutAGuess()
    {
        var response = await Driver.CallToolAsync("planning_append_log", new JsonObject
        {
            ["slug"] = "demo-feature/T1",
            ["note"] = "a line",
        });

        var text = ErrorText(response);
        text.ShouldContain("slug is not a parameter of this tool.");
        text.ShouldNotContain("did you mean");
        text.ShouldContain("Expected parameters:");
    }

    [Fact]
    public async Task MissingRequiredParameter_FilledUnderANearName_PointsAtBoth()
    {
        // The crew's standing mix-up, measured at the real door: planning_append_log takes `note`,
        // and `logNote` is the name from the other doors. That is an unknown parameter, not a
        // missing required one — and the hint names the right key either way.
        var response = await Driver.CallToolAsync("planning_append_log", new JsonObject
        {
            ["alias"] = "demo-feature/T1",
            ["logNote"] = "the note belongs under note",
        });

        var text = ErrorText(response);
        text.ShouldContain("logNote is not a parameter of this tool; did you mean 'note'?");
        text.ShouldContain("note");
    }

    // ---- 266/T3 row 3: an empty optional falls back to its documented default, out loud ----

    [Fact]
    public async Task EmptyOptionalParameter_PayloadStands_AndTheSecondBlockNamesTheDefault()
    {
        var response = await Driver.CallToolAsync("search", new JsonObject
        {
            ["query"] = "hello",
            ["mode"] = "",
        });

        response["error"].ShouldBeNull();
        var blocks = response["result"]!["content"]!.AsArray();
        blocks.Count.ShouldBe(2);
        blocks[0]!["text"]!.GetValue<string>().ShouldContain("Entry One");
        blocks[1]!["text"]!.GetValue<string>().ShouldContain(
            "'mode' arrived empty and was dropped — mode=fulltext applies");
    }

    [Fact]
    public async Task NoEmptyOptional_KeepsASingleContentBlock()
    {
        var response = await Driver.CallToolAsync("search", new JsonObject
        {
            ["query"] = "hello",
        });

        response["result"]!["content"]!.AsArray().Count.ShouldBe(1);
    }

    [Fact]
    public async Task EmptyOptionalParameter_WithoutADocumentedRow_StillNamesTheDrop()
    {
        var response = await Driver.CallToolAsync("overmind_replies", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = "  ",
        });

        var blocks = response["result"]!["content"]!.AsArray();
        blocks.Count.ShouldBe(2);
        blocks[1]!["text"]!.GetValue<string>().ShouldContain(
            "'featureSlug' arrived empty and was dropped");
        blocks[1]!["text"]!.GetValue<string>().ShouldContain("as if you had omitted it");
    }

    [Fact]
    public async Task EmptyOptionalMeaningClear_NotCalledADroppedDefault()
    {
        // planning_update_feature documents `lane: ""` as the explicit clear and forwards it — the
        // empty-optional note would misdescribe a deliberate write as a silent default.
        var response = await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["slug"] = Canned.PlanningSlug,
            ["logNote"] = "leaving the lane",
            ["lane"] = "",
            ["projectId"] = Canned.ProjectId.ToString(),
        });

        response["result"]!["content"]!.AsArray().Count.ShouldBe(1);
        Stub.Requests.Single(request => request.Method == "POST" && request.Path.EndsWith("/update"))
            .Body.ShouldContain("\"lane\":\"\"");
    }

    [Fact]
    public async Task ApplicationLevelError_PassesThroughUnchanged()
    {
        Stub.Stop();

        var response = await Driver.CallToolAsync("read_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString()
        });

        var text = ErrorText(response);
        text.ShouldStartWith("An error occurred invoking 'read_entry':");
        text.ShouldContain("unreachable");
    }

    private static string ErrorText(JsonNode response)
    {
        response["error"].ShouldBeNull();
        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        return ResultText(response);
    }

    private static string ResultText(JsonNode response) => response["result"]!
        ["content"]!.AsArray().First()!["text"]!.GetValue<string>();
}
