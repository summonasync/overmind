using System.Text.Json;
using NSubstitute;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class MemoryAgentToolsTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    private static readonly GrimoireMemoryEndpoint PersonalEndpoint =
        new("http://127.0.0.1:19997", "overmind-agent", "overmind-agent");
    private static readonly GrimoireMemoryEndpoint CrewEndpoint =
        new("http://127.0.0.1:19997", "crew", "overmind-agent");
    private static readonly Guid RememberedId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task InvokeAsync_MemoryRemember_DefaultsToPersonalTomeWithProvenance()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory.ResolveEndpointAsync(GrimoireTomeTarget.Personal, Arg.Any<CancellationToken>()).Returns(PersonalEndpoint);
        memory
            .RememberAsync(PersonalEndpoint, Arg.Any<GrimoireMemoryWriteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GrimoireMemoryWriteResult(RememberedId, null, "overmind-agent"));
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
        result.GetProperty("tome").GetString().ShouldBe("overmind-agent");
        result.GetProperty("actor").GetString().ShouldBe("overmind-agent+overmind-host");
        await memory.DidNotReceive().ResolveEndpointAsync(GrimoireTomeTarget.Crew, Arg.Any<CancellationToken>());
        await memory.Received(1).RememberAsync(
            PersonalEndpoint,
            Arg.Is<GrimoireMemoryWriteRequest>(request =>
                request.Content == "owner prefers feature branches" &&
                request.Kind == "fact" &&
                request.Scope == "global" &&
                request.Importance == 0.9 &&
                request.Tags.Contains("pref")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_MemoryRemember_TargetCrew_LandsInCrewTome()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory.ResolveEndpointAsync(GrimoireTomeTarget.Crew, Arg.Any<CancellationToken>()).Returns(CrewEndpoint);
        memory
            .RememberAsync(CrewEndpoint, Arg.Any<GrimoireMemoryWriteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GrimoireMemoryWriteResult(RememberedId, null, "crew"));
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync(
            "memory_remember",
            JsonSerializer.SerializeToElement(new { content = "crew standardizes on xunit", target = "crew" }),
            default,
            CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse(result.GetRawText());
        result.GetProperty("tome").GetString().ShouldBe("crew");
        await memory.DidNotReceive().ResolveEndpointAsync(GrimoireTomeTarget.Personal, Arg.Any<CancellationToken>());
        await memory.Received(1).RememberAsync(
            CrewEndpoint,
            Arg.Is<GrimoireMemoryWriteRequest>(request => request.Content == "crew standardizes on xunit"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_MemoryRemember_UnknownTarget_IsRejected()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync(
            "memory_remember",
            JsonSerializer.SerializeToElement(new { content = "note", target = "team" }),
            default,
            CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldContain("'target' must be 'personal' or 'crew'");
        await memory.DidNotReceive().RememberAsync(
            Arg.Any<GrimoireMemoryEndpoint>(), Arg.Any<GrimoireMemoryWriteRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_MemoryNote_AlwaysTargetsPersonalEvenWhenCrewRequested()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory.ResolveEndpointAsync(GrimoireTomeTarget.Personal, Arg.Any<CancellationToken>()).Returns(PersonalEndpoint);
        memory
            .RememberAsync(PersonalEndpoint, Arg.Any<GrimoireMemoryWriteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GrimoireMemoryWriteResult(RememberedId, null, "overmind-agent"));
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync(
            "memory_note",
            JsonSerializer.SerializeToElement(new { content = "deploy went sideways at 14:05", target = "crew" }),
            default,
            CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse(result.GetRawText());
        await memory.Received(1).RememberAsync(
            PersonalEndpoint,
            Arg.Is<GrimoireMemoryWriteRequest>(request =>
                request.Content == "deploy went sideways at 14:05" &&
                request.Kind == "journal" &&
                request.Importance == 0.5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_MemoryRecall_MergesBothTomesWithSourceLabels()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory.ResolveEndpointAsync(GrimoireTomeTarget.Personal, Arg.Any<CancellationToken>()).Returns(PersonalEndpoint);
        memory.ResolveEndpointAsync(GrimoireTomeTarget.Crew, Arg.Any<CancellationToken>()).Returns(CrewEndpoint);
        memory
            .RecallAsync(PersonalEndpoint, "reply style", null, null, 5, Arg.Any<CancellationToken>())
            .Returns([Record("owner prefers terse replies")]);
        memory
            .RecallAsync(CrewEndpoint, "reply style", null, null, 5, Arg.Any<CancellationToken>())
            .Returns([Record("crew ships on branches")]);
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync(
            "memory_recall",
            JsonSerializer.SerializeToElement(new { query = "reply style" }),
            default,
            CancellationToken.None);

        result.ValueKind.ShouldBe(JsonValueKind.Array);
        var hits = result.EnumerateArray().ToList();
        hits.Count.ShouldBe(2);
        hits[0].GetProperty("source").GetString().ShouldBe("personal");
        hits[0].GetProperty("content").GetString().ShouldBe("owner prefers terse replies");
        hits[1].GetProperty("source").GetString().ShouldBe("crew");
        hits[1].GetProperty("content").GetString().ShouldBe("crew ships on branches");
    }

    [Fact]
    public async Task InvokeAsync_MemoryRecall_CapsMergedHitsAtTheRequestedLimit()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory.ResolveEndpointAsync(GrimoireTomeTarget.Personal, Arg.Any<CancellationToken>()).Returns(PersonalEndpoint);
        memory.ResolveEndpointAsync(GrimoireTomeTarget.Crew, Arg.Any<CancellationToken>()).Returns(CrewEndpoint);
        memory
            .RecallAsync(PersonalEndpoint, "budget", null, null, 3, Arg.Any<CancellationToken>())
            .Returns([Record("personal one"), Record("personal two")]);
        memory
            .RecallAsync(CrewEndpoint, "budget", null, null, 3, Arg.Any<CancellationToken>())
            .Returns([Record("crew one"), Record("crew two")]);
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync(
            "memory_recall",
            JsonSerializer.SerializeToElement(new { query = "budget", limit = 3 }),
            default,
            CancellationToken.None);

        var hits = result.EnumerateArray().ToList();
        hits.Count.ShouldBe(3);
        hits.Select(hit => hit.GetProperty("source").GetString()).ShouldBe(["personal", "personal", "crew"]);
    }

    [Fact]
    public async Task InvokeAsync_MemoryRecall_OneTomeDegraded_ReturnsHitsFromTheHealthyTome()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory
            .ResolveEndpointAsync(GrimoireTomeTarget.Personal, Arg.Any<CancellationToken>())
            .Returns<GrimoireMemoryEndpoint>(_ => throw new GrimoireMemoryException("personal is down"));
        memory.ResolveEndpointAsync(GrimoireTomeTarget.Crew, Arg.Any<CancellationToken>()).Returns(CrewEndpoint);
        memory
            .RecallAsync(CrewEndpoint, "branch policy", null, null, 5, Arg.Any<CancellationToken>())
            .Returns([Record("crew ships on branches")]);
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync(
            "memory_recall",
            JsonSerializer.SerializeToElement(new { query = "branch policy" }),
            default,
            CancellationToken.None);

        result.ValueKind.ShouldBe(JsonValueKind.Array);
        var hit = result.EnumerateArray().Single();
        hit.GetProperty("source").GetString().ShouldBe("crew");
    }

    [Fact]
    public async Task InvokeAsync_MemoryRecall_OneTomeEmptyOtherFailed_ReturnsEmptyArray()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory.ResolveEndpointAsync(GrimoireTomeTarget.Personal, Arg.Any<CancellationToken>()).Returns(PersonalEndpoint);
        memory.ResolveEndpointAsync(GrimoireTomeTarget.Crew, Arg.Any<CancellationToken>()).Returns(CrewEndpoint);
        memory
            .RecallAsync(PersonalEndpoint, "quiet week", null, null, 5, Arg.Any<CancellationToken>())
            .Returns([]);
        memory
            .RecallAsync(CrewEndpoint, "quiet week", null, null, 5, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<GrimoireMemoryRecord>>(_ => throw new GrimoireMemoryException("crew call failed with 500"));
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync(
            "memory_recall",
            JsonSerializer.SerializeToElement(new { query = "quiet week" }),
            default,
            CancellationToken.None);

        result.ValueKind.ShouldBe(JsonValueKind.Array);
        result.EnumerateArray().Count().ShouldBe(0);
    }

    [Fact]
    public async Task InvokeAsync_MemoryRecall_NegativeLimit_ClampedToOne()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory.ResolveEndpointAsync(GrimoireTomeTarget.Personal, Arg.Any<CancellationToken>()).Returns(PersonalEndpoint);
        memory.ResolveEndpointAsync(GrimoireTomeTarget.Crew, Arg.Any<CancellationToken>()).Returns(CrewEndpoint);
        memory
            .RecallAsync(PersonalEndpoint, "budget", null, null, 1, Arg.Any<CancellationToken>())
            .Returns([]);
        memory
            .RecallAsync(CrewEndpoint, "budget", null, null, 1, Arg.Any<CancellationToken>())
            .Returns([]);
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync(
            "memory_recall",
            JsonSerializer.SerializeToElement(new { query = "budget", limit = -3 }),
            default,
            CancellationToken.None);

        result.ValueKind.ShouldBe(JsonValueKind.Array);
        await memory.Received(1).RecallAsync(
            PersonalEndpoint, "budget", null, null, 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_MemoryRecall_BothTomesDown_ReturnsHonestUnavailableError()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory
            .ResolveEndpointAsync(Arg.Any<GrimoireTomeTarget>(), Arg.Any<CancellationToken>())
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
        memory.ResolveEndpointAsync(GrimoireTomeTarget.Personal, Arg.Any<CancellationToken>()).Returns(PersonalEndpoint);
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

    private static GrimoireMemoryRecord Record(string content) => new(
        Guid.NewGuid(), content, "fact", [],
        DateTimeOffset.Parse("2026-11-01T10:00:00Z"), DateTimeOffset.Parse("2026-11-01T10:00:00Z"),
        "billy-herrington");

    private AgentToolRegistry CreateRegistry(IGrimoireMemoryClient memory)
    {
        var http = new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://127.0.0.1:1") };
        var state = new EmbeddedAgentSessionState();
        state.Set(Guid.NewGuid());
        return new AgentToolRegistry(
            new AgentLoopbackClient(http), state, memory,
            new ConversationMessageStore(_temp.CreateContext()));
    }

    public void Dispose() => _temp.Dispose();

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
