using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NSubstitute;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Agent.Tools;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class AgentToolScopeEnforcementTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    private static readonly Guid ConversationProject = Guid.Parse("019712af-8ea4-7aa1-9f5b-2f3c4d5e6f01");
    private static readonly Guid ForeignProject = Guid.Parse("019712af-8ea4-7aa1-9f5b-2f3c4d5e6f02");
    private static readonly Guid UnnamedProjectA = Guid.Parse("019712af-8ea4-7aa1-9f5b-2f3c4d5e6f03");
    private static readonly Guid UnnamedProjectB = Guid.Parse("019712af-8ea4-7aa1-9f5b-2f3c4d5e6f04");

    private static readonly AgentToolInvocation ScopedTurn = new(false, null, Guid.NewGuid(), ConversationProject);

    private static readonly Regex GuidToken = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);

    [Theory]
    [InlineData("Planning", "planning_list_features")]
    [InlineData("Wave", "wave_rooms")]
    [InlineData("Lane", "planning_list_lanes")]
    [InlineData("Entry", "list_entries")]
    [InlineData("Issue", "issues_list")]
    [InlineData("Board", "read_project")]
    public async Task InvokeAsync_RequiredForeignProject_RefusedBeforeAnyRequest(string family, string tool)
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync(tool, ProjectArgs(ForeignProject), ScopedTurn, CancellationToken.None);

        var error = ScopeRefusal(result, $"{family} ({tool})");
        error.ShouldContain(tool);
        error.ShouldContain(ConversationProject.ToString());
        error.ShouldContain(ForeignProject.ToString());
        handler.Requests.ShouldBeEmpty($"{family} tool '{tool}' reached the loopback API with a foreign projectId");
    }

    [Theory]
    [InlineData("Planning", "planning_list_features")]
    [InlineData("Wave", "wave_rooms")]
    [InlineData("Lane", "planning_list_lanes")]
    [InlineData("Entry", "list_entries")]
    [InlineData("Issue", "issues_list")]
    [InlineData("Board", "read_project")]
    public async Task InvokeAsync_RequiredOwnProject_DispatchesToTheConversationBoard(string family, string tool)
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync(
            tool, ProjectArgs(ConversationProject), ScopedTurn, CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse($"{family} ({tool}): {result.GetRawText()}");
        var request = handler.Requests.ShouldHaveSingleItem();
        request.RequestUri!.ToString().ShouldContain(ConversationProject.ToString());
    }

    [Fact]
    public async Task InvokeAsync_LaneRosterOfForeignProject_Refused_MeasuredIssueRepro()
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync(
            "planning_list_lanes", ProjectArgs(ForeignProject.ToString().ToUpperInvariant()), ScopedTurn,
            CancellationToken.None);

        ScopeRefusal(result, "issue db8d4d47 repro: a scoped conversation read another board's lanes");
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("planning_list_features")]
    [InlineData("planning_task_queue")]
    [InlineData("planning_list_lanes")]
    [InlineData("list_entries")]
    [InlineData("wave_rooms")]
    public async Task InvokeAsync_OwnProjectWrittenUppercase_NotRefused(string tool)
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync(
            tool, ProjectArgs(ConversationProject.ToString().ToUpperInvariant()), ScopedTurn, CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse($"{tool}: {result.GetRawText()}");
    }

    [Theory]
    [InlineData("Entry", "search", """{"query":"scope","projectId":"{0}"}""")]
    [InlineData("Board", "notify_human", """{"kind":"info","text":"heads up","projectId":"{0}"}""")]
    public async Task InvokeAsync_OptionalForeignProject_Refused(string family, string tool, string template)
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync(tool, Template(template, ForeignProject), ScopedTurn,
            CancellationToken.None);

        ScopeRefusal(result, $"{family} optional projectId ({tool})");
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("search", """{"query":"scope"}""")]
    [InlineData("notify_human", """{"kind":"info","text":"heads up"}""")]
    public async Task InvokeAsync_OptionalProjectOmitted_PassesThrough(string tool, string body)
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync(tool, Parse(body), ScopedTurn, CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse($"{tool}: {result.GetRawText()}");
        handler.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task InvokeAsync_ProjectLessConversation_PassesTheModelSuppliedScopeThrough()
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);
        var globalTurn = new AgentToolInvocation(false, null, Guid.NewGuid(), null);

        var result = await registry.InvokeAsync(
            "planning_list_lanes", ProjectArgs(ForeignProject), globalTurn, CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse(result.GetRawText());
        handler.Requests.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("list_projects")]
    [InlineData("get_board_context")]
    public async Task InvokeAsync_DiscoveryToolOnForeignProject_StillRuns(string tool)
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync(tool, ProjectArgs(ForeignProject), ScopedTurn, CancellationToken.None);

        result.TryGetProperty("code", out _).ShouldBeFalse(result.GetRawText());
        handler.Requests.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ContextCarveOutCoversTheSnapshotButNotItsFamilyNeighbours()
    {
        var registry = CreateRegistry();

        var snapshot = await registry.InvokeAsync(
            "get_board_context", ProjectArgs(ForeignProject), ScopedTurn, CancellationToken.None);
        var lanes = await registry.InvokeAsync(
            "planning_list_lanes", ProjectArgs(ForeignProject), ScopedTurn, CancellationToken.None);

        snapshot.TryGetProperty("code", out _).ShouldBeFalse(snapshot.GetRawText());
        ScopeRefusal(lanes, "the Discovery exemption stops at the specs that declare it");
    }

    [Fact]
    public void GuidParseLenience_IsMeasuredAndNarrowsTheBypassClass()
    {
        var controls = new[] { ' ', '\t', '\n', '\r', '\v', '\f', '\u00a0', '\u0085', '\u2000', '\u202f', '\u205f', '\u3000' };

        controls.All(char.IsWhiteSpace).ShouldBeTrue();
        controls.Count(control => Guid.TryParse($"{control}{ForeignProject}", out _))
            .ShouldBe(controls.Length, "Guid.TryParse skips every whitespace codepoint on this runtime");
        controls.Count(control => Guid.TryParse($"{ForeignProject}{control}", out _))
            .ShouldBe(controls.Length, "trailing padding parses too");
        Guid.TryParse("\u00a0019712af\u00a0-8ea4-7aa1-9f5b-2f3c4d5e6f02", out _)
            .ShouldBeFalse("only a value that resolves to no guid at all is guard-blind");
    }

    [Theory]
    [InlineData('\u00a0')]
    [InlineData('\u202f')]
    [InlineData('\u3000')]
    [InlineData('\u0085')]
    public async Task InvokeAsync_WhitespacePaddedForeignProject_RefusedWithNoRequest(char padding)
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync(
            "planning_list_lanes", ProjectArgs($"{padding}{ForeignProject}"), ScopedTurn, CancellationToken.None);

        ScopeRefusal(result, $"padded foreign id (U+{(int)padding:X4}) must not reach the handler");
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData('\u00a0')]
    [InlineData('\u3000')]
    public async Task InvokeAsync_WhitespacePaddedOwnProject_NotRefused(char padding)
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync(
            "planning_list_lanes", ProjectArgs($"{padding}{ConversationProject}"), ScopedTurn, CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse($"padded own id U+{(int)padding:X4}: {result.GetRawText()}");
        handler.Requests.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("""{"query":"scope","projectId":"not-a-guid"}""")]
    [InlineData("""{"query":"scope","projectId":"\u00a0\u00a0"}""")]
    [InlineData("""{"query":"scope","projectId":"019712af-8ea4\u00a0-7aa1-9f5b-2f3c4d5e6f02"}""")]
    [InlineData("""{"query":"scope","projectId":42}""")]
    [InlineData("""{"query":"scope","projectId":{"nested":"object"}}""")]
    public async Task InvokeAsync_OptionalProjectIdUnverifiable_IsRefusedInsteadOfForwardedRaw(string body)
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync("search", Parse(body), ScopedTurn, CancellationToken.None);

        result.TryGetProperty("error", out var error).ShouldBeTrue(
            $"the value reached the loopback API unscoped instead of being refused: {result.GetRawText()}");
        result.GetProperty("code").GetString().ShouldBe("projectScopeUnverifiable", body);
        error.GetString()!.ShouldNotContain("evil-echo-canary");
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{"projectId":"not-a-guid"}""")]
    [InlineData("""{"projectId":"\u00a0\u00a0"}""")]
    [InlineData("""{"projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f02-99"}""")]
    [InlineData("""{"projectId":"<|mask_privacy|>"}""")]
    [InlineData("""{"projectId":42}""")]
    [InlineData("""{"projectId":true}""")]
    [InlineData("""{"projectId":{"nested":"object"}}""")]
    public async Task InvokeAsync_ProjectIdPresentButUnverifiable_RefusedWithoutEchoingIt(string body)
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync(
            "planning_list_lanes", Parse(body), ScopedTurn, CancellationToken.None);

        result.TryGetProperty("error", out var error).ShouldBeTrue(result.GetRawText());
        result.GetProperty("code").GetString().ShouldBe("projectScopeUnverifiable", body);
        error.GetString()!.ShouldNotContain("evil-echo-canary");
        error.GetString()!.ShouldNotContain("nested");
        error.GetString()!.ShouldContain(ConversationProject.ToString());
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{"query":"scope"}""")]
    [InlineData("""{"query":"scope","projectId":null}""")]
    public async Task InvokeAsync_ProjectIdAbsentOrNull_PassesThrough(string body)
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync("search", Parse(body), ScopedTurn, CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse(result.GetRawText());
        handler.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task InvokeAsync_RefusalText_NamesNoProjectOtherThanTheConversationAndTheArgument()
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync(
            "planning_list_features", ProjectArgs(ForeignProject), ScopedTurn, CancellationToken.None);

        var error = ScopeRefusal(result, "leak check");
        handler.Requests.ShouldBeEmpty("the refusal must not be composed from a board read");
        error.ShouldNotContain(UnnamedProjectA.ToString());
        error.ShouldNotContain(UnnamedProjectB.ToString());
        GuidToken.Matches(error)
            .Select(match => Guid.Parse(match.Value))
            .Distinct()
            .ShouldBe([ConversationProject, ForeignProject], ignoreOrder: true);
    }

    [Fact]
    public async Task InvokeAsync_UnknownToolEnumeratesToolNamesButNoProjectIdentifiers()
    {
        var registry = CreateRegistry();

        var result = await registry.InvokeAsync(
            "planning_list_lanes_of_other_board", EmptyArgs(), ScopedTurn, CancellationToken.None);

        var error = result.GetProperty("error").GetString()!;
        error.ShouldStartWith("Unknown tool");
        GuidToken.Matches(error).ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_IssuesCreateStaysEnforced_UntilTask2MarksItCrossProject()
    {
        var handler = new RecordingHandler();
        var registry = CreateRegistry(handler);

        var result = await registry.InvokeAsync(
            "issues_create",
            Parse($$"""
                    {"projectId":"{{ForeignProject}}","authorProjectId":"{{ConversationProject}}",
                     "title":"foreign board","body":"## Repro\nnone"}
                    """),
            ScopedTurn,
            CancellationToken.None);

        ScopeRefusal(result, "issues_create is enforced in task 0; task 2 opens the cross-project door");
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(AgentToolRegistry.ProjectScopeMismatchMessage)]
    [InlineData(AgentToolRegistry.ProjectScopeUnverifiableMessage)]
    public void ScopeRefusalTemplate_ClaimsOnlyTheArgumentItCompared(string template)
    {
        var phrased = string.Format(template, "some_tool", ConversationProject, ForeignProject);

        phrased.ShouldContain("this call");
        phrased.ShouldContain("projectId");
        foreach (var general in new[]
                 {
                     "every call", "all calls", "any call", "no bypass", "cannot reach", "always",
                     "impossible", "guarantee", "other project ids"
                 })
        {
            phrased.ShouldNotContain(general, Case.Insensitive, phrased);
        }
    }

    [Fact]
    public void UnverifiableTemplate_HasNoSlotForTheValueItRejected()
    {
        AgentToolRegistry.ProjectScopeUnverifiableMessage.ShouldNotContain("{2}");
    }

    [Fact]
    public void AgentToolScope_EnforceIsTheZeroValue_SoAnUnmarkedSpecGetsTheArgumentComparison()
    {
        Convert.ToInt32(AgentToolScope.Enforce).ShouldBe(0);
        default(AgentToolScope).ShouldBe(AgentToolScope.Enforce);
    }

    [Fact]
    public void AgentToolSpec_WithoutAnExplicitPolicy_CarriesTheEnforcePolicy()
    {
        var spec = new AgentToolSpec("a_tool", "does something", """{"type":"object","properties":{}}""",
            (_, _) => Task.FromResult(Parse("{}")));

        spec.Scope.ShouldBe(AgentToolScope.Enforce);
    }

    [Fact]
    public void AgentToolInvocation_WithoutAProjectIsTheGlobalCase_AndRefusesNothing()
    {
        default(AgentToolInvocation).ProjectId.ShouldBeNull();
    }

    private static JsonElement ProjectArgs(Guid projectId) => ProjectArgs(projectId.ToString());

    private static JsonElement ProjectArgs(string projectId) =>
        JsonSerializer.SerializeToElement(new { projectId });

    private static JsonElement Template(string template, Guid projectId) =>
        Parse(template.Replace("{0}", projectId.ToString()));

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static string ScopeRefusal(JsonElement result, string because)
    {
        result.TryGetProperty("error", out var error).ShouldBeTrue($"{because}: got {result.GetRawText()}");
        result.GetProperty("code").GetString().ShouldBe(AgentToolRegistry.ProjectScopeMismatchCode, because);
        var text = error.GetString()!;
        text.ShouldNotBeNullOrWhiteSpace();
        return text;
    }

    private AgentToolRegistry CreateRegistry(RecordingHandler? handler = null)
    {
        var http = new HttpClient(handler ?? new RecordingHandler()) { BaseAddress = new Uri("http://127.0.0.1:1") };
        var state = new EmbeddedAgentSessionState();
        state.Set(Guid.NewGuid());
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory.ResolveEndpointAsync(Arg.Any<GrimoireTomeTarget>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GrimoireMemoryEndpoint>(
                new GrimoireMemoryException("grimoire is not configured in this test")));
        return new AgentToolRegistry(
            new AgentLoopbackClient(http), state, memory,
            new ConversationMessageStore(_temp.CreateContext()),
            new AgentRouteRecorder(new AgentRouteMemoryStore()));
    }

    public void Dispose() => _temp.Dispose();

    private static JsonElement EmptyArgs() => JsonSerializer.SerializeToElement(new { });

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly List<HttpRequestMessage> _requests = [];

        public IReadOnlyList<HttpRequestMessage> Requests => _requests;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }
    }
}
