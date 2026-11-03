using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using NSubstitute;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Tools;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class ContextAgentToolsTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    private static readonly GrimoireMemoryEndpoint Endpoint = new("http://127.0.0.1:19999", "crew", "overmind-agent");
    private static readonly GrimoireMemoryEndpoint PersonalEndpoint =
        new("http://127.0.0.1:19999", "overmind-agent", "overmind-agent");

    private static readonly Guid ProjectOne = Guid.NewGuid();
    private static readonly Guid ProjectTwo = Guid.NewGuid();

    [Fact]
    public async Task InvokeAsync_AllSourcesUp_SectionsLandInPinnedOrder()
    {
        var memory = FakeMemory();
        memory
            .ListTopFactsAsync(Endpoint, ContextAgentTools.MaxMemoryFacts, Arg.Any<CancellationToken>())
            .Returns([Fact("scope approvals stay human-door unless delegation is armed")]);
        var registry = CreateRegistry(memory, Routes(rowsPerProject: 1, replies: 1));

        var result = await registry.InvokeAsync("get_board_context", EmptyArgs(), default, CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse(result.GetRawText());
        result.EnumerateObject().Select(row => row.Name)
            .ShouldBe(["projects", "queue", "recentReplies", "memoryFacts"], ignoreOrder: false);
        result.TryGetProperty("degraded", out _).ShouldBeFalse();

        var projectRow = result.GetProperty("projects")[0];
        projectRow.EnumerateObject().Select(row => row.Name)
            .ShouldBe(["id", "name", "openFeatures"], ignoreOrder: false);
        projectRow.GetProperty("name").GetString().ShouldBe("project-0");

        var queueRow = result.GetProperty("queue")[0];
        queueRow.EnumerateObject().Select(row => row.Name)
            .ShouldBe(["projectId", "slug", "title", "priority", "status", "tasks"], ignoreOrder: false);
        queueRow.GetProperty("tasks").GetString().ShouldBe("5/8");

        result.GetProperty("recentReplies")[0].EnumerateObject().Select(row => row.Name)
            .ShouldBe(["id", "kind", "text", "replyText"], ignoreOrder: false);

        result.GetProperty("memoryFacts").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task InvokeAsync_OverCapSources_EverySectionIsCapped()
    {
        var memory = FakeMemory();
        memory
            .ListTopFactsAsync(Endpoint, ContextAgentTools.MaxMemoryFacts, Arg.Any<CancellationToken>())
            .Returns([.. Enumerable.Range(0, 8).Select(_ => Fact(new string('y', 300)))]);
        var registry = CreateRegistry(memory, Routes(rowsPerProject: 9, replies: 8, projects: 11));

        var result = await registry.InvokeAsync("get_board_context", EmptyArgs(), default, CancellationToken.None);

        result.GetProperty("projects").GetArrayLength().ShouldBe(ContextAgentTools.MaxSnapshotProjects);
        result.GetProperty("queue").GetArrayLength().ShouldBe(ContextAgentTools.MaxQueueRows);
        result.GetProperty("recentReplies").GetArrayLength().ShouldBe(ContextAgentTools.MaxRecentReplies);
        result.GetProperty("memoryFacts").GetArrayLength().ShouldBe(ContextAgentTools.MaxMemoryFacts);

        var reply = result.GetProperty("recentReplies")[0];
        reply.GetProperty("text").GetString()!.Length.ShouldBe(161);
        reply.GetProperty("text").GetString()!.ShouldEndWith("…");

        result.GetProperty("memoryFacts")[0].GetProperty("content").GetString()!.Length.ShouldBe(241);
    }

    [Fact]
    public async Task InvokeAsync_ScopedProjectId_QueueCoversOnlyTheScopedProject()
    {
        var requests = new List<string>();
        var registry = CreateRegistry(FakeMemory(), Routes(rowsPerProject: 2, replies: 1, log: requests.Add));

        var result = await registry.InvokeAsync(
            "get_board_context",
            JsonSerializer.SerializeToElement(new { projectId = ProjectTwo.ToString() }),
            default,
            CancellationToken.None);

        requests.ShouldNotContain(path => path.Contains(ProjectOne.ToString()));
        requests.Count(path => path.Contains("task-queue")).ShouldBe(1);
        var queue = result.GetProperty("queue");
        queue.GetArrayLength().ShouldBe(2);
        queue[0].GetProperty("projectId").GetString().ShouldBe(ProjectTwo.ToString());
    }

    [Fact]
    public async Task InvokeAsync_QueueEndpointFailing_RemainingSectionsStillCompose()
    {
        var routes = Routes(rowsPerProject: 2, replies: 1);
        routes.Fail($"/api/agent/projects/{ProjectOne}/planning/task-queue");
        var registry = CreateRegistry(FakeMemory(), routes);

        var result = await registry.InvokeAsync("get_board_context", EmptyArgs(), default, CancellationToken.None);

        result.TryGetProperty("degraded", out _).ShouldBeFalse();
        result.EnumerateObject().Select(row => row.Name)
            .ShouldBe(["projects", "queue", "recentReplies", "memoryFacts"], ignoreOrder: false);
        result.GetProperty("queue").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task InvokeAsync_BoardReadFailing_MarksDegradedAndStillCarriesMemoryFacts()
    {
        var registry = CreateRegistry(FakeMemory(), failingBoard: true);

        var result = await registry.InvokeAsync("get_board_context", EmptyArgs(), default, CancellationToken.None);

        result.EnumerateObject().Select(row => row.Name).ShouldBe(["degraded", "memoryFacts"], ignoreOrder: false);
        result.GetProperty("degraded").GetString().ShouldContain("board read failed");
        result.GetProperty("memoryFacts").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task InvokeAsync_GrimoireDown_DropsMemoryFactsWithoutFailing()
    {
        var memory = FakeMemory();
        memory
            .ResolveEndpointAsync(Arg.Any<GrimoireTomeTarget>(), Arg.Any<CancellationToken>())
            .Returns<GrimoireMemoryEndpoint>(_ => throw new GrimoireMemoryException(
                "grimoire memory is unavailable: no running grimoire instance was detected"));
        var registry = CreateRegistry(memory, Routes(rowsPerProject: 1, replies: 1));

        var result = await registry.InvokeAsync("get_board_context", EmptyArgs(), default, CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse(result.GetRawText());
        result.TryGetProperty("memoryFacts", out _).ShouldBeFalse();
        result.GetProperty("queue").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task InvokeAsync_FactsFromBothTomes_LabeledBySource()
    {
        var memory = FakeMemory();
        memory.ResolveEndpointAsync(GrimoireTomeTarget.Personal, Arg.Any<CancellationToken>()).Returns(PersonalEndpoint);
        memory
            .ListTopFactsAsync(PersonalEndpoint, ContextAgentTools.MaxMemoryFacts, Arg.Any<CancellationToken>())
            .Returns([Fact("owner reviews every spec before validation")]);
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync("get_board_context", EmptyArgs(), default, CancellationToken.None);

        var rows = result.GetProperty("memoryFacts");
        rows.GetArrayLength().ShouldBe(2);
        rows[0].GetProperty("source").GetString().ShouldBe("personal");
        rows[0].GetProperty("content").GetString().ShouldBe("owner reviews every spec before validation");
        rows[1].GetProperty("source").GetString().ShouldBe("crew");
    }

    [Fact]
    public async Task InvokeAsync_PersonalTomeDown_CrewFactsStillCompose()
    {
        var memory = FakeMemory();
        memory
            .ResolveEndpointAsync(GrimoireTomeTarget.Personal, Arg.Any<CancellationToken>())
            .Returns<GrimoireMemoryEndpoint>(_ => throw new GrimoireMemoryException("personal tome is down"));
        var registry = CreateRegistry(memory);

        var result = await registry.InvokeAsync("get_board_context", EmptyArgs(), default, CancellationToken.None);

        var rows = result.GetProperty("memoryFacts");
        rows.GetArrayLength().ShouldBe(1);
        rows[0].GetProperty("source").GetString().ShouldBe("crew");
    }

    [Fact]
    public void Build_RegistersGetBoardContextAsAStandardTool()
    {
        var registry = CreateRegistry(FakeMemory());

        registry.CreateTools(includeHumanGated: false).Select(tool => tool.Name).ShouldContain("get_board_context");
    }

    private static IGrimoireMemoryClient FakeMemory()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory
            .ResolveEndpointAsync(Arg.Any<GrimoireTomeTarget>(), Arg.Any<CancellationToken>())
            .Returns(Endpoint);
        memory
            .ListTopFactsAsync(Endpoint, ContextAgentTools.MaxMemoryFacts, Arg.Any<CancellationToken>())
            .Returns([Fact("delegation changes who executes, never whether a human decided")]);
        return memory;
    }

    private static GrimoireMemoryRecord Fact(string content) =>
        new(Guid.NewGuid(), content, "fact", [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "overmind-agent");

    private static RouteHandler Routes(
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

    private AgentToolRegistry CreateRegistry(IGrimoireMemoryClient memory, RouteHandler? routes = null, bool failingBoard = false)
    {
        HttpMessageHandler handler = failingBoard
            ? new StatusHandler(HttpStatusCode.InternalServerError)
            : routes ?? Routes(rowsPerProject: 0, replies: 0);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:1") };
        var state = new EmbeddedAgentSessionState();
        state.Set(Guid.NewGuid());
        return new AgentToolRegistry(
            new AgentLoopbackClient(http), state, memory,
            new ConversationMessageStore(_temp.CreateContext()));
    }

    public void Dispose() => _temp.Dispose();

    private static JsonElement EmptyArgs() => JsonSerializer.SerializeToElement(new { });

    private sealed class RouteHandler(Dictionary<string, string> payloadFor, Action<string>? log = null) : HttpMessageHandler
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
