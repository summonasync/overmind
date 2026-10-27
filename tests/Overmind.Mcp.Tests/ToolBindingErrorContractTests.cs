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
