using System.Text.Json;
using NSubstitute;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Agent.Tools;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class AgentToolScopeDeclarationTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly AgentToolRegistry _registry;
    private readonly ITestOutputHelper _output;

    public AgentToolScopeDeclarationTests(ITestOutputHelper output)
    {
        _output = output;
        _registry = CreateRegistry();
    }

    [Fact]
    public void Audit_RegisteredSpecs_HasNoViolations()
    {
        var violations = AgentToolScopeDeclarationAudit.Audit(_registry.Specs);

        violations.ShouldBeEmpty(
            $"{_registry.Specs.Count} registered specs audited, " +
            $"{AgentToolScopeDeclarationAudit.ObjectDeterminedGap.Count} declared doors: " +
            string.Join(" | ", violations));
    }

    [Fact]
    public void Audit_DenominatorsComeFromTheRegistryAndCoverEverySpec()
    {
        var declaredTotal = AgentToolScopeDeclarationAudit.AllDeclared().Values.Sum(bucket => bucket.Count);
        var registered = _registry.Specs.Count;

        registered.ShouldBe(_registry.Count);
        declaredTotal.ShouldBe(registered,
            "the declared buckets must cover the registry exactly — a gap here is an unclassified tool");
        _output.WriteLine(
            $"registry {_registry.Count} specs, {AgentToolScopeDeclarationAudit.ObjectDeterminedGap.Count} declared doors, " +
            $"{AgentToolScopeDeclarationAudit.SessionScoped.Count} session, {AgentToolScopeDeclarationAudit.Discovery.Count} discovery, " +
            $"{AgentToolScopeDeclarationAudit.CrossProject.Count} cross-project, " +
            $"{AgentToolScopeDeclarationAudit.ArgumentEnforced.Count} argument-enforced, " +
            $"{AgentToolScopeDeclarationAudit.NoAddressedTarget.Count} addressing no target, " +
            $"{AgentToolScopeDeclarationAudit.ScopedHandleExceptions.Count} scoped-handle exceptions");
        registered.ShouldBeGreaterThanOrEqualTo(70);
        AgentToolScopeDeclarationAudit.ObjectDeterminedGap.Count.ShouldBe(19);
        AgentToolScopeDeclarationAudit.CrossProject.Count.ShouldBe(1);
        AgentToolScopeDeclarationAudit.SessionScoped.Count.ShouldBe(7);
        AgentToolScopeDeclarationAudit.Discovery.Count.ShouldBe(3);
    }

    [Fact]
    public void SessionScoped_ExactlyTheSevenSessionSpecsCarryThePolicy()
    {
        var marked = _registry.Specs
            .Where(spec => spec.Scope == AgentToolScope.SessionScoped)
            .Select(spec => spec.Name)
            .ToList();

        marked.ShouldNotBeEmpty("an empty marked set makes every SessionScoped assertion green on nothing");
        marked.ShouldBe(AgentToolScopeDeclarationAudit.SessionScoped, ignoreOrder: true);
        marked.ShouldNotContain("planning_list_lanes");
        marked.ShouldNotContain("list_entries");
    }

    [Fact]
    public void SessionScoped_SpecsAddressNoProjectArgument()
    {
        foreach (var name in AgentToolScopeDeclarationAudit.SessionScoped)
        {
            var spec = Spec(name);

            AgentToolScopeDeclarationAudit.CarriesProjectArgument(spec).ShouldBeFalse(
                $"{name} is declared session-scoped yet its schema addresses a project");
        }
    }

    [Fact]
    public void Discovery_ExactlyTheDeclaredReadsCarryThePolicy()
    {
        var marked = _registry.Specs
            .Where(spec => spec.Scope == AgentToolScope.Discovery)
            .Select(spec => spec.Name)
            .ToList();

        marked.ShouldBe(AgentToolScopeDeclarationAudit.Discovery, ignoreOrder: true);
        marked.ShouldNotBeEmpty();
        marked.ShouldContain("notify_human",
            "D5 (owner ruling 2026-11-17): notify_human's projectId is a deep-link field, not a board to act on");
        marked.ShouldContain("get_board_context");
        marked.ShouldContain("list_projects");
    }

    [Fact]
    public void CrossProject_IsDeclaredOnTheFilingDoorAlone_WhileTheFourIssueIdDoorsStayInTheGap()
    {
        var marked = _registry.Specs
            .Where(spec => spec.Scope == AgentToolScope.CrossProject)
            .Select(spec => spec.Name)
            .ToList();

        marked.ShouldBe(AgentToolScopeDeclarationAudit.CrossProject, ignoreOrder: true);
        marked.ShouldHaveSingleItem();
        Spec("issues_append").Scope.ShouldBe(AgentToolScope.Enforce);
        AgentToolScopeDeclarationAudit.ObjectDeterminedGap.ShouldContain("issues_append",
            "issues_append addresses an issue by id — marking it CrossProject would delete a tracked hole");
        foreach (var door in new[] { "issues_get", "issues_set_status", "issues_promote", "issues_append" })
        {
            AgentToolScopeDeclarationAudit.ObjectDeterminedGap.ShouldContain(door);
        }
    }

    [Fact]
    public void Gap_OptionalProjectIdDoorsAreDeclaredInsideTheList_NotFiledAsProtected()
    {
        foreach (var name in new[]
                 {
                     "planning_patch_artifact", "planning_append_log", "planning_edit_log", "planning_delete_artifact"
                 })
        {
            var spec = Spec(name);

            AgentToolScopeDeclarationAudit.ObjectDetermined(spec).ShouldBeTrue(name);
            AgentToolScopeDeclarationAudit.CarriesProjectArgument(spec).ShouldBeTrue(
                $"{name} does carry projectId — that is exactly why a has-no-projectId classifier would mis-file it");
            AgentToolScopeDeclarationAudit.ObjectDeterminedGap.ShouldContain(name);
        }
    }

    [Fact]
    public void Gap_TheObjectDeterminedSetIsWhatTheRuleDerives()
    {
        // Deliberately policy-blind: a door addressed by object id is a hole whatever scope someone declares
        // for it, so marking it CrossProject cannot shrink the tracked set — only closing it in 246 can.
        var derived = _registry.Specs
            .Where(AgentToolScopeDeclarationAudit.ObjectDetermined)
            .Select(spec => spec.Name)
            .ToList();

        derived
            .Where(name => !AgentToolScopeDeclarationAudit.ScopedHandleExceptions.Contains(name))
            .ShouldBe(AgentToolScopeDeclarationAudit.ObjectDeterminedGap, ignoreOrder: true);
        AgentToolScopeDeclarationAudit.ObjectDeterminedGap.Count.ShouldBe(19,
            "the declared gap stays at 19 doors while 241 ships argument-level enforcement only");
        derived.ShouldContain("ack_entry",
            "ack_entry carries a required projectId with the handle and is exempt only because the service scopes it");
    }

    [Fact]
    public void Gap_PlanningValidateIsDeclaredDeliberately_AsAnObjectDeterminedHumanDoor()
    {
        var spec = Spec("planning_validate");

        spec.Category.ShouldBe(AgentToolCategory.HumanGated);
        spec.Scope.ShouldBe(AgentToolScope.Enforce, "human-gating answers who may call, not which project it may touch");
        AgentToolScopeDeclarationAudit.BucketOf("planning_validate").ShouldBe("ObjectDeterminedGap");
        AgentToolScopeDeclarationAudit.CarriesProjectArgument(spec).ShouldBeFalse();
    }

    [Fact]
    public void Tripwire_NewProjectArgumentSpecWithoutDeclaration_IsUnclassified()
    {
        var mutated = AppendToRegistry(Spec("planning_list_directives", """
            {"type":"object","properties":{"projectId":{"type":"string"}},"required":["projectId"]}
            """));

        ViolationsFor(mutated).ShouldContain(v => v.Contains("unclassified") && v.Contains("planning_list_directives"));
    }

    [Fact]
    public void Tripwire_NewObjectDeterminedSpecMissingFromGap_IsFlagged()
    {
        var mutated = AppendToRegistry(Spec("issues_unassign", """
            {"type":"object","properties":{"issueId":{"type":"string"}},"required":["issueId"]}
            """));

        var violations = ViolationsFor(mutated);
        violations.ShouldContain(v => v.Contains("planning", StringComparison.OrdinalIgnoreCase) ||
                                      v.Contains("unclassified"), "a new door cannot pass the audit silently");
        violations.ShouldContain(v => v.Contains("issues_unassign"));
    }

    [Fact]
    public void Tripwire_SessionScopedSpecGainingProjectId_IsFlagged()
    {
        var mutated = ReplaceInRegistry("wake_peek", Spec("wake_peek", """
            {"type":"object","properties":{"since":{"type":"string"},"projectId":{"type":"string"}},"required":[]}
            """, scope: AgentToolScope.SessionScoped));

        ViolationsFor(mutated).ShouldContain(v =>
            v.Contains("wake_peek") && v.Contains("addresses a project"));
    }

    [Fact]
    public void Tripwire_NewRequiredProjectIdWithHandleNotDeclaredAsScopedException_IsFlagged()
    {
        var mutated = AppendToRegistry(Spec("planning_ack_artifact", """
            {"type":"object","properties":{"projectId":{"type":"string"},"artifactId":{"type":"string"}},"required":["projectId","artifactId"]}
            """));

        ViolationsFor(mutated).ShouldContain(v =>
            v.Contains("planning_ack_artifact") && v.Contains("ScopedHandleExceptions"));
    }

    [Fact]
    public void Tripwire_DeclaredNameNoLongerRegistered_IsFlagged()
    {
        var mutated = _registry.Specs
            .Where(spec => spec.Name != "read_entry")
            .ToList();

        ViolationsFor(mutated).ShouldContain(v =>
            v.Contains("read_entry") && v.Contains("no longer registered"));
    }

    private AgentToolSpec Spec(string name) =>
        _registry.Specs.Single(spec => spec.Name == name);

    private static AgentToolSpec Spec(string name, string schema, AgentToolScope scope = AgentToolScope.Enforce) => new(
        name, "synthetic spec used by an audit tripwire", schema,
        (_, _) => Task.FromResult(JsonDocument.Parse("{}").RootElement.Clone()),
        Scope: scope);

    private IReadOnlyList<AgentToolSpec> AppendToRegistry(AgentToolSpec extra) =>
        [.. _registry.Specs, extra];

    private IReadOnlyList<AgentToolSpec> ReplaceInRegistry(string name, AgentToolSpec replacement) =>
        [.. _registry.Specs.Where(spec => spec.Name != name), replacement];

    private static IReadOnlyList<string> ViolationsFor(IReadOnlyList<AgentToolSpec> specs) =>
        AgentToolScopeDeclarationAudit.Audit(specs);

    private AgentToolRegistry CreateRegistry()
    {
        var http = new HttpClient(new NoOpHandler()) { BaseAddress = new Uri("http://127.0.0.1:1") };
        var state = new EmbeddedAgentSessionState();
        state.Set(Guid.NewGuid());
        return new AgentToolRegistry(
            new AgentLoopbackClient(http), state, Substitute.For<IGrimoireMemoryClient>(),
            new ConversationMessageStore(_temp.CreateContext()),
            new AgentRouteRecorder(new AgentRouteMemoryStore()));
    }

    public void Dispose() => _temp.Dispose();

    private sealed class NoOpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            });
    }
}
