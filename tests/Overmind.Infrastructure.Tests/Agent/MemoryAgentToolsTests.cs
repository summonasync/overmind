using System.Text.Json;
using NSubstitute;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class MemoryAgentToolsTests
{
    private static readonly GrimoireMemoryEndpoint Endpoint = new("http://127.0.0.1:19997", "crew", "overmind-agent");
    private static readonly Guid RememberedId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task InvokeAsync_MemoryRemember_LandsInConfiguredTomeWithProvenance()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory.ResolveEndpointAsync(Arg.Any<CancellationToken>()).Returns(Endpoint);
        memory
            .RememberAsync(Endpoint, Arg.Any<GrimoireMemoryWriteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GrimoireMemoryWriteResult(RememberedId, null, "crew"));
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync(
            "memory_remember",
            JsonSerializer.SerializeToElement(new
            {
                content = "owner prefers feature branches",
                kind = "fact",
                importance = 0.9,
                tags = new[] { "pref" },
            }),
            default,
            CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse(result.GetRawText());
        result.GetProperty("memoryId").GetString().ShouldBe(RememberedId.ToString());
        result.GetProperty("tome").GetString().ShouldBe("crew");
        result.GetProperty("actor").GetString().ShouldBe("overmind-agent+overmind-host");
        await memory.Received(1).RememberAsync(
            Endpoint,
            Arg.Is<GrimoireMemoryWriteRequest>(request =>
                request.Content == "owner prefers feature branches" &&
                request.Kind == "fact" &&
                request.Scope == "global" &&
                request.Importance == 0.9 &&
                request.Tags.Contains("pref")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_MemoryNote_WritesJournalWithDefaultImportance()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory.ResolveEndpointAsync(Arg.Any<CancellationToken>()).Returns(Endpoint);
        memory
            .RememberAsync(Endpoint, Arg.Any<GrimoireMemoryWriteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GrimoireMemoryWriteResult(RememberedId, null, "crew"));
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync(
            "memory_note",
            JsonSerializer.SerializeToElement(new { content = "deploy went sideways at 14:05" }),
            default,
            CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse(result.GetRawText());
        await memory.Received(1).RememberAsync(
            Endpoint,
            Arg.Is<GrimoireMemoryWriteRequest>(request =>
                request.Content == "deploy went sideways at 14:05" &&
                request.Kind == "journal" &&
                request.Importance == 0.5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_MemoryRecall_ReturnsHitsIntoTheLoop()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory.ResolveEndpointAsync(Arg.Any<CancellationToken>()).Returns(Endpoint);
        memory
            .RecallAsync(Endpoint, "branch policy", null, null, 5, Arg.Any<CancellationToken>())
            .Returns([
                new GrimoireMemoryRecord(
                    RememberedId, "crew ships on branches", "fact", ["policy"],
                    DateTimeOffset.Parse("2026-11-01T10:00:00Z"), DateTimeOffset.Parse("2026-11-01T10:00:00Z"),
                    "billy-herrington"),
            ]);
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync(
            "memory_recall",
            JsonSerializer.SerializeToElement(new { query = "branch policy" }),
            default,
            CancellationToken.None);

        result.ValueKind.ShouldBe(JsonValueKind.Array);
        var hit = result.EnumerateArray().Single();
        hit.GetProperty("memoryId").GetString().ShouldBe(RememberedId.ToString());
        hit.GetProperty("content").GetString().ShouldBe("crew ships on branches");
    }

    [Fact]
    public async Task InvokeAsync_GrimoireDown_ReturnsHonestUnavailableError()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory
            .ResolveEndpointAsync(Arg.Any<CancellationToken>())
            .Returns<GrimoireMemoryEndpoint>(_ => throw new GrimoireMemoryException(
                "grimoire memory is unavailable: no running grimoire instance was detected"));
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync(
            "memory_recall",
            JsonSerializer.SerializeToElement(new { query = "anything" }),
            default,
            CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldContain("grimoire memory is unavailable");
    }

    [Fact]
    public async Task InvokeAsync_MemoryRemember_MissingContent_ReturnsArgumentError()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory.ResolveEndpointAsync(Arg.Any<CancellationToken>()).Returns(Endpoint);
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync(
            "memory_remember",
            JsonSerializer.SerializeToElement(new { importance = 0.9 }),
            default,
            CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldContain("'content' is required");
        await memory.DidNotReceive().RememberAsync(
            Arg.Any<GrimoireMemoryEndpoint>(), Arg.Any<GrimoireMemoryWriteRequest>(), Arg.Any<CancellationToken>());
    }

    private static AgentToolRegistry CreateRegistry(IGrimoireMemoryClient memory)
    {
        var http = new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://127.0.0.1:1") };
        var state = new EmbeddedAgentSessionState();
        state.Set(Guid.NewGuid());
        return new AgentToolRegistry(new AgentLoopbackClient(http), state, memory);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
