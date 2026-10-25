using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(AdapterSessionState))]
public sealed class WaveRoleToolContractTests : IAsyncLifetime
{
    private static readonly Guid SessionId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private StubOvermindApi _stub = null!;
    private OvermindApiClient _api = null!;

    public Task InitializeAsync()
    {
        _stub = StubOvermindApi.Start();
        _api = new OvermindApiClient(OvermindApiClient.CreateHttpClient(_stub.BaseUrl));
        AdapterSession.Id = SessionId;

        _stub.Route("POST", $"/api/agent/sessions/{SessionId}/role", 200, SessionJson);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        AdapterSession.Id = null;
        await _stub.DisposeAsync();
    }

    [Fact]
    public async Task SetRole_Marshal_PostsToTheSessionEndpoint_AndEchoes()
    {
        var result = await Tool().SetRole("marshal");

        var request = _stub.Requests.Single();
        request.Path.ShouldBe($"/api/agent/sessions/{SessionId}/role");
        request.Body.ShouldContain("""{"role":"marshal"}""");
        Text(result).ShouldBe("Role set to 'marshal'.");
    }

    [Fact]
    public async Task SetRole_Worker_EchoesTheDefaultRole()
    {
        var result = await Tool().SetRole("worker");

        Text(result).ShouldBe("Role set to 'worker'.");
    }

    [Theory]
    [InlineData("wizard")]
    [InlineData("Marshal")]
    [InlineData("")]
    public async Task SetRole_UnknownRole_FailsWithTheNamedValidationError(string role)
    {
        var ex = await Should.ThrowAsync<McpException>(() => Tool().SetRole(role));

        ex.Message.ShouldContain($"Unknown wave role '{role}'");
        ex.Message.ShouldContain("'worker'");
        ex.Message.ShouldContain("'marshal'");
        _stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetRole_WithoutASession_FailsWithTheNamedSessionError()
    {
        AdapterSession.Id = null;

        var ex = await Should.ThrowAsync<McpException>(() => Tool().SetRole("marshal"));

        ex.Message.ShouldContain("No registered agent session");
        _stub.Requests.ShouldBeEmpty();
    }

    private static string SessionJson =>
        $$"""{"id":"{{SessionId}}","alias":"dan","vendor":"claude","startedAt":"2026-10-25T09:00:00Z","lastSeenAt":"2026-10-25T09:30:00Z","endedAt":null}""";

    private static string Text(CallToolResult result) =>
        ((TextContentBlock)result.Content[0]).Text;

    private WaveRoleTools Tool() => new(_api, new MutationGuard(_api));
}
