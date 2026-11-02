using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class AgentPromptComposerTests
{
    private static readonly GrimoireMemoryEndpoint Endpoint = new("http://127.0.0.1:19996", "crew", "overmind-agent");

    private static readonly Guid ProjectOne = Guid.NewGuid();
    private static readonly Guid ProjectTwo = Guid.NewGuid();

    private readonly Conversation _conversation = Conversation.Create(null, "composer tests");

    [Fact]
    public async Task ComposeAsync_GrimoireUp_IncludesTopFactsIntoTheSnapshot()
    {
        var memory = FakeMemory();
        memory
            .ListTopFactsAsync(Endpoint, AgentPromptComposer.MaxMemoryFacts, Arg.Any<CancellationToken>())
            .Returns([
                new GrimoireMemoryRecord(
                    Guid.NewGuid(),
                    "crew ships features through PRs opened from feat/NNN-slug branches",
                    "fact", [],
                    DateTimeOffset.Parse("2026-11-02T09:00:00Z"), DateTimeOffset.Parse("2026-11-02T09:00:00Z"),
                    "billy-herrington"),
            ]);
        var composer = CreateComposer(memory);

        var messages = await composer.ComposeAsync(_conversation, [], "hello there", CancellationToken.None);

        var system = messages[0].Text;
        messages[0].Role.ShouldBe(ChatRole.System);
        system.ShouldContain("memoryFacts");
        system.ShouldContain("crew ships features through PRs");
        system.ShouldContain("2026-11-02");
    }

    [Fact]
    public async Task ComposeAsync_GrimoireDown_DegradesSilentlyAndStillComposes()
    {
        var memory = FakeMemory();
        memory
            .ResolveEndpointAsync(Arg.Any<CancellationToken>())
            .Returns<GrimoireMemoryEndpoint>(_ => throw new GrimoireMemoryException(
                "grimoire memory is unavailable: no running grimoire instance was detected"));
        var composer = CreateComposer(memory);

        var messages = await composer.ComposeAsync(_conversation, [], "hello there", CancellationToken.None);

        messages.Count.ShouldBe(2);
        messages[0].Text.ShouldNotContain("memoryFacts");
        messages[0].Text.ShouldContain("You are overmind-agent");
        messages.Last().Role.ShouldBe(ChatRole.User);
        messages.Last().Text.ShouldBe("hello there");
    }

    [Fact]
    public async Task ComposeAsync_AllSourcesUp_SectionsLandInPinnedOrder()
    {
        var memory = FakeMemory();
        memory
            .ListTopFactsAsync(Endpoint, AgentPromptComposer.MaxMemoryFacts, Arg.Any<CancellationToken>())
            .Returns([Fact("scope approvals stay human-door unless delegation is armed")]);
        var composer = CreateComposer(memory, Routes(twoProjects: true, rowsPerProject: 1, replies: 1));

        var messages = await composer.ComposeAsync(_conversation, [], "status?", CancellationToken.None);
        var snapshot = SnapshotOf(messages[0].Text);

        snapshot.Select(node => node.Key)
            .ShouldBe(["projects", "queue", "recentReplies", "memoryFacts"], ignoreOrder: false);
        snapshot.ContainsKey("degraded").ShouldBeFalse();

        var projectRow = snapshot["projects"]![0]!.AsObject();
        projectRow.Select(node => node.Key)
            .ShouldBe(["id", "name", "openFeatures"], ignoreOrder: false);
        projectRow["name"]!.GetValue<string>().ShouldBe("project-0");

        var queueRow = snapshot["queue"]![0]!.AsObject();
        queueRow.Select(node => node.Key)
            .ShouldBe(["projectId", "slug", "title", "priority", "status", "tasks"], ignoreOrder: false);
        queueRow["tasks"]!.GetValue<string>().ShouldBe("5/8");

        var replyRow = snapshot["recentReplies"]![0]!.AsObject();
        replyRow.Select(node => node.Key)
            .ShouldBe(["id", "kind", "text", "replyText"], ignoreOrder: false);

        snapshot["memoryFacts"]!.AsArray().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ComposeAsync_OverCapSources_EverySectionIsCapped()
    {
        var memory = FakeMemory();
        memory
            .ListTopFactsAsync(Endpoint, AgentPromptComposer.MaxMemoryFacts, Arg.Any<CancellationToken>())
            .Returns([.. Enumerable.Range(0, 8).Select(_ => Fact(new string('y', 300)))]);
        var composer = CreateComposer(memory, Routes(twoProjects: true, rowsPerProject: 9, replies: 8, projects: 11));

        var messages = await composer.ComposeAsync(_conversation, [], "status?", CancellationToken.None);
        var snapshot = SnapshotOf(messages[0].Text);

        snapshot["projects"]!.AsArray().Count.ShouldBe(AgentPromptComposer.MaxSnapshotProjects);
        snapshot["queue"]!.AsArray().Count.ShouldBe(AgentPromptComposer.MaxQueueRows);
        snapshot["recentReplies"]!.AsArray().Count.ShouldBe(AgentPromptComposer.MaxRecentReplies);
        snapshot["memoryFacts"]!.AsArray().Count.ShouldBe(AgentPromptComposer.MaxMemoryFacts);

        var reply = snapshot["recentReplies"]![0]!.AsObject();
        reply["text"]!.GetValue<string>().Length.ShouldBe(161);
        reply["text"]!.GetValue<string>().ShouldEndWith("…");

        snapshot["memoryFacts"]![0]!.AsObject()["content"]!.GetValue<string>().Length.ShouldBe(241);
    }

    [Fact]
    public async Task ComposeAsync_ScopedConversation_QueueCoversOnlyTheScopedProject()
    {
        var requests = new List<string>();
        var routes = Routes(twoProjects: true, rowsPerProject: 2, replies: 1, log: requests.Add);
        var composer = CreateComposer(FakeMemory(), routes);
        var scoped = Conversation.Create(ProjectTwo, "scoped chat");

        var messages = await composer.ComposeAsync(scoped, [], "status?", CancellationToken.None);
        var snapshot = SnapshotOf(messages[0].Text);

        requests.ShouldNotContain(path => path.Contains(ProjectOne.ToString()));
        requests.Count(path => path.Contains("task-queue")).ShouldBe(1);
        snapshot["queue"]!.AsArray().Count.ShouldBe(2);
        snapshot["queue"]![0]!.AsObject()["projectId"]!.GetValue<string>().ShouldBe(ProjectTwo.ToString());
    }

    [Fact]
    public async Task ComposeAsync_QueueEndpointFailing_RemainingSectionsStillCompose()
    {
        var routes = Routes(twoProjects: true, rowsPerProject: 2, replies: 1);
        routes.Fail($"/api/agent/projects/{ProjectOne}/planning/task-queue");
        var composer = CreateComposer(FakeMemory(), routes);

        var messages = await composer.ComposeAsync(_conversation, [], "status?", CancellationToken.None);
        var snapshot = SnapshotOf(messages[0].Text);

        snapshot.ContainsKey("degraded").ShouldBeFalse();
        snapshot.Select(node => node.Key)
            .ShouldBe(["projects", "queue", "recentReplies", "memoryFacts"], ignoreOrder: false);
        snapshot["queue"]!.AsArray().Count.ShouldBe(2);
    }

    [Fact]
    public async Task ComposeAsync_BoardReadFailing_MarksDegradedAndStillCarriesMemoryFacts()
    {
        var composer = CreateComposer(FakeMemory(), failingBoard: true);

        var messages = await composer.ComposeAsync(_conversation, [], "status?", CancellationToken.None);
        var snapshot = SnapshotOf(messages[0].Text);

        snapshot.Select(node => node.Key).ShouldBe(["degraded", "memoryFacts"], ignoreOrder: false);
        snapshot["degraded"]!.GetValue<string>().ShouldContain("board read failed");
        snapshot["memoryFacts"]!.AsArray().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ComposeAsync_WithInstructions_AppendsDelimitedBlockAfterBootstrapSections()
    {
        const string instructions = "Be terse. Skip pleasantries.";
        var composer = CreateComposer(FakeMemory(), instructions: instructions);

        var messages = await composer.ComposeAsync(_conversation, [], "status?", CancellationToken.None);
        var system = messages[0].Text;

        var markerIndex = system.IndexOf("Bootstrap snapshot (may be partial):", StringComparison.Ordinal);
        var headerIndex = system.IndexOf(AgentPromptComposer.InstructionsHeader, StringComparison.Ordinal);
        markerIndex.ShouldBeGreaterThanOrEqualTo(0);
        headerIndex.ShouldBeGreaterThan(markerIndex);
        system.ShouldEndWith(instructions);

        var snapshot = SnapshotOf(system);
        snapshot.Select(node => node.Key)
            .ShouldBe(["projects", "queue", "recentReplies", "memoryFacts"], ignoreOrder: false);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ComposeAsync_WithoutInstructions_PromptStaysByteIdenticalToBootstrapOnly(string instructions)
    {
        var composer = CreateComposer(FakeMemory(), instructions: instructions);

        var messages = await composer.ComposeAsync(_conversation, [], "status?", CancellationToken.None);
        var system = messages[0].Text;

        system.ShouldNotContain(AgentPromptComposer.InstructionsHeader);
        system.TrimEnd().ShouldEndWith("}");
        system.ShouldEndWith(Environment.NewLine);
    }

    [Fact]
    public async Task ComposeAsync_OverCapInstructions_TruncatesAtTheCap()
    {
        var composer = CreateComposer(FakeMemory(), instructions: new string('i', 4500));

        var messages = await composer.ComposeAsync(_conversation, [], "status?", CancellationToken.None);
        var system = messages[0].Text;

        system.ShouldEndWith(new string('i', SettingDefaults.AgentInstructionsMaxLength));
        system.ShouldNotContain(new string('i', SettingDefaults.AgentInstructionsMaxLength + 1));
    }

    private static IGrimoireMemoryClient FakeMemory()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory.ResolveEndpointAsync(Arg.Any<CancellationToken>()).Returns(Endpoint);
        memory
            .ListTopFactsAsync(Endpoint, AgentPromptComposer.MaxMemoryFacts, Arg.Any<CancellationToken>())
            .Returns([Fact("delegation changes who executes, never whether a human decided")]);
        return memory;
    }

    private static GrimoireMemoryRecord Fact(string content) =>
        new(Guid.NewGuid(), content, "fact", [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "overmind-agent");

    private static JsonObject SnapshotOf(string system)
    {
        const string marker = "Bootstrap snapshot (may be partial):";
        var start = system.IndexOf(marker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var tail = system[(start + marker.Length)..];
        var terminator = tail.IndexOf(AgentPromptComposer.InstructionsHeader, StringComparison.Ordinal);
        if (terminator >= 0)
        {
            tail = tail[..terminator];
        }

        return JsonNode.Parse(tail.TrimEnd())!.AsObject();
    }

    private static RouteHandler Routes(
        bool twoProjects,
        int rowsPerProject,
        int replies,
        int projects = 2,
        Action<string>? log = null)
    {
        var payloadFor = new Dictionary<string, string>();
        var ids = new List<Guid> { ProjectOne, ProjectTwo };
        while (ids.Count < projects)
        {
            ids.Add(Guid.NewGuid());
        }

        var projectsPayload = string.Join(",", ids.Select((id, index) =>
            $$"""{"id":"{{id.ToString()}}","name":"project-{{index}}","openFeatureCount":"1"}"""));
        payloadFor["/api/projects"] = $"[{projectsPayload}]";

        var rows = string.Join(",", Enumerable.Range(0, rowsPerProject).Select(index =>
            $$"""{"slug":"feature-{{index}}","title":"Feature {{index}}","priority":"p1","status":"inProgress","taskDone":"5","taskTotal":"8"}"""));
        foreach (var id in ids)
        {
            payloadFor[$"/api/agent/projects/{id}/planning/task-queue"] =
                $$"""{"items":[{{rows}}]}""";
        }

        var replyItems = string.Join(",", Enumerable.Range(0, replies).Select(index =>
            $$"""{"id":"{{Guid.NewGuid()}}","kind":"approval","text":"{{new string('x', 200)}}","replyText":"approved {{index}}"}"""));
        payloadFor["/api/agent/notifications/replies"] = $"{{\"items\":[{replyItems}]}}";

        return new RouteHandler(payloadFor, log);
    }

    private static AgentPromptComposer CreateComposer(
        IGrimoireMemoryClient memory,
        RouteHandler? routes = null,
        bool failingBoard = false,
        string instructions = "")
    {
        HttpMessageHandler handler = failingBoard
            ? new StatusHandler(HttpStatusCode.InternalServerError)
            : routes ?? Routes(twoProjects: false, rowsPerProject: 0, replies: 0);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:1") };
        var settings = Substitute.For<IAgentRunSettingsProvider>();
        settings
            .GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AgentRunSettings("overmind-agent", 8192, 0, instructions));
        return new AgentPromptComposer(
            new AgentLoopbackClient(http),
            settings,
            memory);
    }

    private sealed class RouteHandler(Dictionary<string, string> payloadFor, Action<string>? log) : HttpMessageHandler
    {
        private readonly HashSet<string> _failures = [];

        public void Fail(string path) => _failures.Add(path);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            log?.Invoke(path);
            var payload = payloadFor.TryGetValue(path, out var value) ? value : "{}";
            return Task.FromResult(
                _failures.Contains(path)
                    ? Respond(HttpStatusCode.InternalServerError, payload)
                    : Respond(HttpStatusCode.OK, payload));
        }
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Respond(status, "{}"));
        }
    }

    private static HttpResponseMessage Respond(HttpStatusCode status, string payload) => new(status)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json"),
    };
}
