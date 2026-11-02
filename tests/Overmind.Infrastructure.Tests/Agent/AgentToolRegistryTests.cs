using System.Text.Json;
using Overmind.Infrastructure.Agent;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class AgentToolRegistryTests
{
    [Fact]
    public void Build_ExposesAllFamiliesWithoutDuplicateNames()
    {
        var registry = CreateRegistry();

        registry.Count.ShouldBeGreaterThanOrEqualTo(50);
        registry.CreateTools().Count.ShouldBe(registry.Count - 1);
    }

    [Fact]
    public async Task InvokeAsync_UnknownTool_ReturnsErrorJson()
    {
        var registry = CreateRegistry();

        var result = await registry.InvokeAsync("no_such_tool", EmptyArgs(), CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldStartWith("Unknown tool 'no_such_tool'.");
    }

    [Fact]
    public async Task InvokeAsync_HumanGatedTool_IsRefusedWithoutAnyRequest()
    {
        var registry = CreateRegistry();

        var result = await registry.InvokeAsync(
            "planning_validate",
            JsonSerializer.SerializeToElement(new { artifactId = Guid.NewGuid() }),
            CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldContain("human-gated");
    }

    [Fact]
    public async Task InvokeAsync_MissingArgument_ReturnsArgumentError()
    {
        var registry = CreateRegistry();

        var result = await registry.InvokeAsync(
            "planning_create_feature",
            JsonSerializer.SerializeToElement(new { projectId = Guid.NewGuid() }),
            CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldContain("'title' is required");
    }

    private static AgentToolRegistry CreateRegistry()
    {
        var http = new HttpClient(new RecordingHandler()) { BaseAddress = new Uri("http://127.0.0.1:1") };
        var state = new EmbeddedAgentSessionState();
        state.Set(Guid.NewGuid());
        return new AgentToolRegistry(new AgentLoopbackClient(http), state);
    }

    private static JsonElement EmptyArgs() => JsonSerializer.SerializeToElement(new { });

    private sealed class RecordingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            });
    }
}
