using System.Net;
using System.Text;
using System.Text.Json;
using NSubstitute;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Agent.Tools;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class AgentToolRegistryCallerScopeTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    private static readonly Guid ConversationProject = Guid.Parse("019712af-8ea4-7aa1-9f5b-2f3c4d5e6f01");

    // Every artifact door now travels with the caller pair the host compares against — and it travels on
    // one request, because the object-scope check reads headers the same call already carries (R6).
    // The turn is armed for all eight: that is the branch where an unscoped dispatch would be the most
    // privileged one, and these four doors carry their conversation's own projectId so 241's argument
    // guard cannot be what silences them.
    [Theory]
    [InlineData("planning_read_artifact", """{"artifactId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f13"}""")]
    [InlineData("planning_read_artifact_diff", """{"artifactId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f13","fromVersion":1,"toVersion":2}""")]
    [InlineData("planning_set_status", """{"artifactId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f13","status":"inProgress","logNote":"taking it"}""")]
    [InlineData("planning_validate", """{"artifactId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f13"}""")]
    [InlineData("planning_append_log", """{"artifactId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f13","note":"progress","projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f01"}""")]
    [InlineData("planning_edit_log", """{"artifactId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f13","text":"corrected","projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f01"}""")]
    [InlineData("planning_patch_artifact", """{"artifactId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f13","section":"goal","content":"<goal>x</goal>","projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f01"}""")]
    [InlineData("planning_delete_artifact", """{"artifactId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f13","logNote":"draft cleanup","projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f01"}""")]
    public async Task InvokeAsync_ArtifactDoor_ReachesTheHostWithTheCallerPairInOneRequest(string tool, string json)
    {
        var (registry, handler) = Registry();
        var instruction = Guid.NewGuid();

        var result = await registry.InvokeAsync(
            tool, JsonDocument.Parse(json).RootElement.Clone(),
            new AgentToolInvocation(true, instruction, Guid.NewGuid(), ConversationProject),
            CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse(
            $"{tool}: an error here means a guard refused before the wire — the argument guard, not 246, " +
            $"would produce exactly this shape: {result.GetRawText()}");

        var request = handler.Requests.ShouldHaveSingleItem($"{tool} must not spend a second round-trip on scope");
        Scope(request).ShouldBe(ConversationProject.ToString("D"), tool);
        Marker(request).ShouldBe(AgentCallerHeaders.MarkerValue, tool);
    }

    // The other half of the same sentence: where a projectId argument IS present, 241's argument guard is
    // still the first thing to refuse a foreign one, before any caller pair reaches the host.
    [Theory]
    [InlineData("planning_append_log", """{"artifactId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f13","note":"n","projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f99"}""")]
    [InlineData("planning_edit_log", """{"artifactId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f13","text":"t","projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f99"}""")]
    [InlineData("planning_patch_artifact", """{"artifactId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f13","section":"goal","content":"c","projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f99"}""")]
    [InlineData("planning_delete_artifact", """{"artifactId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f13","projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f99"}""")]
    public async Task InvokeAsync_ArtifactDoorWithForeignProjectArgument_RefusedByTheArgumentGuardFirst(
        string tool, string json)
    {
        var (registry, handler) = Registry();

        var result = await registry.InvokeAsync(
            tool, JsonDocument.Parse(json).RootElement.Clone(), Turn(ConversationProject), CancellationToken.None);

        result.GetProperty("code").GetString().ShouldBe(
            AgentToolRegistry.ProjectScopeMismatchCode,
            $"{tool}: a 246 test that passes by tripping this guard proves nothing about the object scope");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ArtifactDoorOfAGlobalConversation_SendsTheSentinelAndNothingElse()
    {
        var (registry, handler) = Registry();

        await registry.InvokeAsync(
            "planning_patch_artifact",
            Args(new { artifactId = Guid.NewGuid(), section = "goal", content = "<goal>x</goal>" }),
            Turn(null),
            CancellationToken.None);

        var request = handler.Requests.ShouldHaveSingleItem();
        Marker(request).ShouldBe(AgentCallerHeaders.MarkerValue);
        Scope(request).ShouldBe(AgentCallerHeaders.GlobalScopeValue);
    }

    [Fact]
    public async Task InvokeAsync_ScopedDoorOnThePlainBranch_CarriesTheConversationScope()
    {
        var (registry, handler) = Registry();

        await registry.InvokeAsync(
            "planning_read_artifact",
            Args(new { artifactId = Guid.NewGuid(), sections = "goal" }),
            Turn(ConversationProject),
            CancellationToken.None);

        var request = handler.Requests.ShouldHaveSingleItem();
        Scope(request).ShouldBe(ConversationProject.ToString("D"));
        Marker(request).ShouldBe(AgentCallerHeaders.MarkerValue);
    }

    [Fact]
    public async Task InvokeAsync_DelegatedBranch_ArmedValidateCarriesTheScopeWithTheDelegationHeader()
    {
        var (registry, handler) = Registry();
        var instruction = Guid.NewGuid();

        var result = await registry.InvokeAsync(
            "planning_validate",
            Args(new { artifactId = Guid.NewGuid() }),
            new AgentToolInvocation(true, instruction, Guid.NewGuid(), ConversationProject),
            CancellationToken.None);

        result.TryGetProperty("error", out _).ShouldBeFalse(result.GetRawText());
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Headers.GetValues(AgentLoopbackClient.DelegatedByHeader).ShouldBe([instruction.ToString()]);
        Scope(request).ShouldBe(ConversationProject.ToString("D"),
            "planning_validate is human-gated and object-addressed: if the scope rode only the plain branch, " +
            "arming delegated approvals would route the privileged call around it");
        Marker(request).ShouldBe(AgentCallerHeaders.MarkerValue);
    }

    [Fact]
    public async Task InvokeAsync_DelegatedBranch_GlobalConversation_SendsTheSentinelWithTheDelegationHeader()
    {
        var (registry, handler) = Registry();
        var instruction = Guid.NewGuid();

        await registry.InvokeAsync(
            "planning_validate",
            Args(new { artifactId = Guid.NewGuid() }),
            new AgentToolInvocation(true, instruction, Guid.NewGuid(), null),
            CancellationToken.None);

        var request = handler.Requests.ShouldHaveSingleItem();
        Marker(request).ShouldBe(AgentCallerHeaders.MarkerValue);
        Scope(request).ShouldBe(AgentCallerHeaders.GlobalScopeValue);
    }

    // One pin per family still on the old delegate, plus planning_upsert_artifact: an argument-addressed
    // door that 246 never converts, so this row cannot go stale half-way through the feature.
    // The issue family converted in T2; the entry rows move when T3 converts them.
    [Theory]
    [InlineData("read_entry", """{"entryId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f11"}""")]
    [InlineData("update_entry", """{"entryId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f11","content":"x"}""")]
    [InlineData("planning_upsert_artifact", """{"projectId":"019712af-8ea4-7aa1-9f5b-2f3c4d5e6f01","slug":"001-x","kind":"spec","body":"b"}""")]
    public async Task InvokeAsync_DoorStillOnTheOldDelegate_CarriesNoCallerHeadersYet(string tool, string json)
    {
        var (registry, handler) = Registry();

        await registry.InvokeAsync(tool, JsonDocument.Parse(json).RootElement.Clone(),
            Turn(ConversationProject), CancellationToken.None);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Headers.Contains(AgentCallerHeaders.ScopeHeader).ShouldBeFalse($"{tool} converts in its family task");
        request.Headers.Contains(AgentCallerHeaders.MarkerHeader).ShouldBeFalse($"{tool} converts in its family task");
    }

    [Fact]
    public async Task InvokeAsync_ScopedHumanGatedDoorUnarmed_IsRefusedBeforeItsUnscopedPlainHandler()
    {
        var (registry, handler) = Registry();

        var result = await registry.InvokeAsync(
            "planning_validate", Args(new { artifactId = Guid.NewGuid() }), Turn(ConversationProject),
            CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldContain("human-gated");
        handler.Requests.ShouldBeEmpty(
            "the plain handler of a scoped human-gated door takes no caller: this refusal is the only thing that " +
            "keeps that unscoped slot unreachable, so it is pinned here");
    }

    [Fact]
    public async Task InvokeAsync_HumanGatedWithoutInstruction_StillRefusedBeforeAnyRequest()
    {
        var (registry, handler) = Registry();

        var result = await registry.InvokeAsync(
            "planning_validate",
            Args(new { artifactId = Guid.NewGuid() }),
            new AgentToolInvocation(true, null, Guid.NewGuid(), ConversationProject),
            CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldContain("no owner instruction");
        handler.Requests.ShouldBeEmpty("241's guards still run before the new dispatch branches");
    }

    [Fact]
    public async Task InvokeAsync_ScopedDoorWithForeignProjectArgument_StillRefusedByTheArgumentGuard()
    {
        var (registry, handler) = Registry();
        var foreign = Guid.NewGuid();

        var result = await registry.InvokeAsync(
            "planning_list_features",
            Args(new { projectId = foreign }),
            Turn(ConversationProject),
            CancellationToken.None);

        result.GetProperty("code").GetString().ShouldBe(AgentToolRegistry.ProjectScopeMismatchCode);
        handler.Requests.ShouldBeEmpty("241's argument comparison is not displaced by the caller scope");
    }

    [Fact]
    public void AgentToolSpec_WithoutTheNewDelegates_DefaultsToThePre246Dispatch()
    {
        var spec = new AgentToolSpec("a_tool", "does something", """{"type":"object","properties":{}}""",
            (_, _) => Task.FromResult(Empty()));

        spec.ScopedInvoke.ShouldBeNull();
        spec.DelegatedScopedInvoke.ShouldBeNull();
    }

    [Fact]
    public void Src_CarriesNoAmbientCallerContext()
    {
        var srcRoot = FindSrcRoot();
        var files = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();

        files.Count.ShouldBeGreaterThan(400,
            $"the sweep is only meaningful over the real source tree (resolved {srcRoot})");
        var offenders = files
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, index, line)))
            .Where(row => row.line.Contains("AsyncLocal", StringComparison.Ordinal) ||
                          row.line.Contains("IHttpContextAccessor", StringComparison.Ordinal) ||
                          row.line.Contains("HttpContext.Current", StringComparison.Ordinal))
            .Select(row => $"{Path.GetRelativePath(srcRoot, row.path)}:{row.index + 1}")
            .ToList();

        offenders.ShouldBeEmpty("246 rules out ambient caller context; the scope travels as a parameter only");
    }

    private static string FindSrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Overmind.Infrastructure")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? AppContext.BaseDirectory, "src");
    }

    private static JsonElement Args(object shape) => JsonSerializer.SerializeToElement(shape);

    private static JsonElement Empty() => JsonDocument.Parse("{}").RootElement.Clone();

    private static AgentToolInvocation Turn(Guid? projectId) => new(false, null, Guid.NewGuid(), projectId);

    private static string? Scope(HttpRequestMessage request) =>
        request.Headers.TryGetValues(AgentCallerHeaders.ScopeHeader, out var values) ? values.Single() : null;

    private static string? Marker(HttpRequestMessage request) =>
        request.Headers.TryGetValues(AgentCallerHeaders.MarkerHeader, out var values) ? values.Single() : null;

    private (AgentToolRegistry Registry, RecordingHandler Handler) Registry()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:1") };
        var state = new EmbeddedAgentSessionState();
        state.Set(Guid.NewGuid());
        return (new AgentToolRegistry(
            new AgentLoopbackClient(http), state, Substitute.For<IGrimoireMemoryClient>(),
            new ConversationMessageStore(_temp.CreateContext()),
            new AgentRouteRecorder(new AgentRouteMemoryStore())), handler);
    }

    public void Dispose() => _temp.Dispose();

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
