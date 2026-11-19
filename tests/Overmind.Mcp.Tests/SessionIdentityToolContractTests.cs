using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(ProcessEnvironment))]
public sealed class SessionIdentityToolContractTests : IAsyncLifetime
{
    private static readonly Guid SessionId = Guid.Parse("18181818-1818-1818-1818-181818181818");

    private StubOvermindApi _stub = null!;
    private OvermindApiClient _api = null!;
    private readonly string _identityRoot = Path.Combine(
        Path.GetTempPath(), "overmind-identity-tool", Guid.NewGuid().ToString("N"));

    public Task InitializeAsync()
    {
        _stub = StubOvermindApi.Start();
        _api = new OvermindApiClient(OvermindApiClient.CreateHttpClient(_stub.BaseUrl));
        AdapterSession.Id = SessionId;

        _stub.Route("POST", $"/api/agent/sessions/{SessionId}/identity", 200, IdentityJson("dan", "claude"));

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (Directory.Exists(_identityRoot))
        {
            Directory.Delete(_identityRoot, recursive: true);
        }

        AdapterSession.Id = null;
        AdapterIdentityState.Reset();
        await _stub.DisposeAsync();
    }

    [Fact]
    public async Task SetIdentity_PostsCallingSession_AndEchoesWarnings()
    {
        _stub.Route("POST", $"/api/agent/sessions/{SessionId}/identity", 200, IdentityWithWarningsJson);

        var result = await Tool().SetIdentity("dan", "claude");

        var request = _stub.Requests.Single();
        request.Path.ShouldBe($"/api/agent/sessions/{SessionId}/identity");
        request.Body.ShouldBe("""{"alias":"dan","vendor":"claude"}""");
        AdapterIdentityState.TryGet().ShouldBe(new AdapterIdentityState.Identity("dan", "claude"));
        Text(result).ShouldBe(
            $"Identity set to 'dan' by 'claude' (session {SessionId}).\n" +
            "Warnings:\n" +
            "- The placeholder identity 'agent' by 'unknown' was set; invent a project alias and set it again.\n" +
            "- Identity changed while holding claims: demo-feature; " +
            "the claim snapshots now read 'dan' by 'claude' and each feature logged the move.");
    }

    [Fact]
    public async Task SetIdentity_TrimsInput_AndFillsSlotWithTheEchoedPair()
    {
        _stub.Route("POST", $"/api/agent/sessions/{SessionId}/identity", 200, IdentityJson("dan", "qwen"));

        var result = await Tool().SetIdentity("  dan  ", " qwen ");

        _stub.Requests.Single().Body.ShouldBe("""{"alias":"dan","vendor":"qwen"}""");
        AdapterIdentityState.TryGet().ShouldBe(new AdapterIdentityState.Identity("dan", "qwen"));
        Text(result).ShouldBe($"Identity set to 'dan' by 'qwen' (session {SessionId}).");
    }

    [Fact]
    public async Task SetIdentity_Conflict_RendersTheHolderMessage_AndLeavesTheSlotEmpty()
    {
        _stub.Route("POST", $"/api/agent/sessions/{SessionId}/identity", 409, AliasHeldJson);

        var ex = await Should.ThrowAsync<McpException>(() => Tool().SetIdentity("dan", "claude"));

        ex.Message.ShouldContain(
            "Alias 'dan' is held by the alive session 'van' by 'qwen'; a stale or ended holder frees the alias.");
        ex.Message.ShouldContain("Invent another alias and retry session_identity_set.");
        AdapterIdentityState.TryGet().ShouldBeNull();
    }

    [Fact]
    public async Task SetIdentity_BadRequest_RendersTheValidationDetail()
    {
        _stub.Route("POST", $"/api/agent/sessions/{SessionId}/identity", 400, ValidationFailedJson);

        var ex = await Should.ThrowAsync<McpException>(() => Tool().SetIdentity("dan", "claude"));

        ex.Message.ShouldContain("Agent identity must not exceed 32 characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetIdentity_BlankAlias_FailsWithoutCallingTheHost(string? alias)
    {
        var ex = await Should.ThrowAsync<McpException>(() => Tool().SetIdentity(alias!, "claude"));

        ex.Message.ShouldContain("'alias' is required");
        _stub.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ddddddddddddddddddddddddddddddddd")]
    [InlineData("claudeclaudeclaudeclaudeclaudeclaude")]
    public async Task SetIdentity_OverlongValues_FailWithoutCallingTheHost(string vendor)
    {
        var ex = await Should.ThrowAsync<McpException>(() => Tool().SetIdentity("dan", vendor));

        ex.Message.ShouldContain("exceeds the 32-char identity limit");
        _stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetIdentity_WithoutASession_FailsWithTheNamedSessionError()
    {
        AdapterSession.Id = null;

        var ex = await Should.ThrowAsync<McpException>(() => Tool().SetIdentity("dan", "claude"));

        ex.Message.ShouldContain("No registered agent session");
        _stub.Requests.ShouldBeEmpty();
    }

    private static string IdentityJson(string alias, string vendor) =>
        $$"""{"session":{"id":"{{SessionId}}","alias":"{{alias}}","vendor":"{{vendor}}","startedAt":"2026-10-27T09:00:00Z","lastSeenAt":"2026-10-27T09:30:00Z","endedAt":null},"warnings":[]}""";

    private static string IdentityWithWarningsJson =>
        $$"""{"session":{"id":"{{SessionId}}","alias":"dan","vendor":"claude","startedAt":"2026-10-27T09:00:00Z","lastSeenAt":"2026-10-27T09:30:00Z","endedAt":null},"warnings":["The placeholder identity 'agent' by 'unknown' was set; invent a project alias and set it again.","Identity changed while holding claims: demo-feature; the claim snapshots now read 'dan' by 'claude' and each feature logged the move."]}""";

    private const string AliasHeldJson =
        """{"title":"AliasHeld","detail":"Alias 'dan' is held by the alive session 'van' by 'qwen'; a stale or ended holder frees the alias.","status":409}""";

    private const string ValidationFailedJson =
        """{"title":"ValidationFailure","detail":"Agent identity must not exceed 32 characters. (Parameter 'alias')","status":400}""";

    private static string Text(CallToolResult result) =>
        ((TextContentBlock)result.Content[0]).Text;

    private sealed class FixedParentPid(int? pid) : IParentProcessIdSource
    {
        public int? Resolve() => pid;
    }

    private SessionIdentityTools Tool(SessionIdentityFileWriter? identityFile = null) =>
        new(_api, identityFile ?? IdentityFile(4242));

    private SessionIdentityFileWriter IdentityFile(int? parentPid) =>
        new(new FixedParentPid(parentPid), _identityRoot);

    private string IdentityFilePath() =>
        Path.Combine(_identityRoot, "identity", "parent-4242.json");

    [Fact]
    public async Task SetIdentity_OverwritesIdentityFile_WithTheEchoedPair()
    {
        _stub.Route("POST", $"/api/agent/sessions/{SessionId}/identity", 200, IdentityJson("dan", "claude"));
        await Tool().SetIdentity("dan", "claude");

        _stub.Route("POST", $"/api/agent/sessions/{SessionId}/identity", 200, IdentityJson("mister-po", "qwen"));
        await Tool().SetIdentity("mister-po", "qwen");

        var payload = JsonNode.Parse(File.ReadAllText(IdentityFilePath()))!;
        payload["alias"]!.GetValue<string>().ShouldBe("mister-po");
        payload["vendor"]!.GetValue<string>().ShouldBe("qwen");
        payload["sessionId"]!.GetValue<string>().ShouldBe(SessionId.ToString());
    }

    [Fact]
    public async Task SetIdentity_IdentityFileFailure_DoesNotFailTheTool()
    {
        var brokenRoot = Path.Combine(Path.GetTempPath(), $"overmind-identity-broken-{Guid.NewGuid():N}");
        File.WriteAllText(brokenRoot, "not a directory");

        try
        {
            var result = await Tool(new SessionIdentityFileWriter(new FixedParentPid(4242), brokenRoot))
                .SetIdentity("dan", "claude");

            Text(result).ShouldBe($"Identity set to 'dan' by 'claude' (session {SessionId}).");
            AdapterIdentityState.TryGet().ShouldBe(new AdapterIdentityState.Identity("dan", "claude"));
        }
        finally
        {
            File.Delete(brokenRoot);
        }
    }
}
