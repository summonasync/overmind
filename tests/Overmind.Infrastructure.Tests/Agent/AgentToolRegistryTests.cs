using System.Text.Json;
using NSubstitute;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class AgentToolRegistryTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    [Fact]
    public void Build_ExposesAllFamiliesWithoutDuplicateNames()
    {
        var registry = CreateRegistry();

        registry.Count.ShouldBeGreaterThanOrEqualTo(50);
        registry.CreateTools(includeHumanGated: false).Count.ShouldBe(registry.Count - 1);
        registry.CreateTools(includeHumanGated: true).Count.ShouldBe(registry.Count);
    }

    [Fact]
    public async Task InvokeAsync_UnknownTool_ReturnsErrorJson()
    {
        var registry = CreateRegistry();

        var result = await registry.InvokeAsync(
            "no_such_tool", EmptyArgs(), default, CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldStartWith("Unknown tool 'no_such_tool'.");
    }

    [Fact]
    public async Task InvokeAsync_HumanGatedTool_IsRefusedWithoutAnyRequest()
    {
        var registry = CreateRegistry();

        var result = await registry.InvokeAsync(
            "planning_validate",
            JsonSerializer.SerializeToElement(new { artifactId = Guid.NewGuid() }),
            default,
            CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldContain("human-gated");
    }

    [Fact]
    public async Task InvokeAsync_HumanGatedTool_WithoutInstruction_IsRefusedEvenWhenArmed()
    {
        var registry = CreateRegistry();

        var result = await registry.InvokeAsync(
            "planning_validate",
            JsonSerializer.SerializeToElement(new { artifactId = Guid.NewGuid() }),
            new AgentToolInvocation(true, null, Guid.NewGuid()),
            CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldContain("no owner instruction");
    }

    [Fact]
    public async Task InvokeAsync_HumanGatedTool_ArmedWithInstruction_DispatchesAndCarriesTheDelegationHeader()
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);
        var instructionId = Guid.NewGuid();

        var result = await registry.InvokeAsync(
            "planning_validate",
            JsonSerializer.SerializeToElement(new { artifactId = Guid.NewGuid() }),
            new AgentToolInvocation(true, instructionId, Guid.NewGuid()),
            CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse(result.GetRawText());
        handler.Requested.ShouldNotBeNull();
        handler.Requested!.Headers.GetValues(AgentLoopbackClient.DelegatedByHeader)
            .ShouldBe([instructionId.ToString()]);
    }

    [Fact]
    public async Task InvokeAsync_StandardToolWithoutDelegatedInvoke_UsesThePlainDispatch()
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync(
            "planning_create_feature",
            JsonSerializer.SerializeToElement(new { projectId = Guid.NewGuid(), title = "delegation off path" }),
            default,
            CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse(result.GetRawText());
        handler.Requested!.Headers.Contains(AgentLoopbackClient.DelegatedByHeader).ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_StandardToolWithDelegatedInvoke_ArmedDispatchCarriesTheHeader()
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);
        var instructionId = Guid.NewGuid();

        var result = await registry.InvokeAsync(
            "planning_set_feature_status",
            JsonSerializer.SerializeToElement(new { projectId = Guid.NewGuid(), slug = "feat", status = "done", logNote = "owner said close it" }),
            new AgentToolInvocation(true, instructionId, Guid.NewGuid()),
            CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse(result.GetRawText());
        handler.Requested!.Headers.GetValues(AgentLoopbackClient.DelegatedByHeader)
            .ShouldBe([instructionId.ToString()]);
    }

    [Fact]
    public async Task InvokeAsync_MissingArgument_ReturnsArgumentError()
    {
        var registry = CreateRegistry();

        var result = await registry.InvokeAsync(
            "planning_create_feature",
            JsonSerializer.SerializeToElement(new { projectId = Guid.NewGuid() }),
            default,
            CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldContain("'title' is required");
    }

    [Fact]
    public void Build_IncludesConversationHistoryAmongStandardTools()
    {
        var registry = CreateRegistry();

        registry.CreateTools(includeHumanGated: false)
            .Select(tool => tool.Name)
            .ShouldContain("conversation_history");
    }

    private AgentToolRegistry CreateRegistry(RecordingHandler? handler = null)
    {
        var http = new HttpClient(handler ?? new RecordingHandler()) { BaseAddress = new Uri("http://127.0.0.1:1") };
        var state = new EmbeddedAgentSessionState();
        state.Set(Guid.NewGuid());
        return new AgentToolRegistry(
            new AgentLoopbackClient(http), state, Substitute.For<IGrimoireMemoryClient>(),
            new ConversationMessageStore(_temp.CreateContext()));
    }

    public void Dispose() => _temp.Dispose();

    private static JsonElement EmptyArgs() => JsonSerializer.SerializeToElement(new { });

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Requested { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
