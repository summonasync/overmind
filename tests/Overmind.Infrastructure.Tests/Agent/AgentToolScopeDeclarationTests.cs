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
            $"registry {_registry.Count} specs, {AgentToolScopeDeclarationAudit.DeclaredEnforced.Count} closed object doors, " +
            $"{AgentToolScopeDeclarationAudit.ObjectDeterminedGap.Count} declared doors, " +
            $"{AgentToolScopeDeclarationAudit.SessionScoped.Count} session, {AgentToolScopeDeclarationAudit.Discovery.Count} discovery, " +
            $"{AgentToolScopeDeclarationAudit.CrossProject.Count} cross-project, " +
            $"{AgentToolScopeDeclarationAudit.ArgumentEnforced.Count} argument-enforced, " +
            $"{AgentToolScopeDeclarationAudit.NoAddressedTarget.Count} addressing no target, " +
            $"{AgentToolScopeDeclarationAudit.ScopedHandleExceptions.Count} scoped-handle exceptions");
        registered.ShouldBeGreaterThanOrEqualTo(70);
        AgentToolScopeDeclarationAudit.ObjectDeterminedGap.Count.ShouldBe(3,
            "the kit doors are the only object-addressed hole left after 246 T1-T3, and the number is the sample size");
        AgentToolScopeDeclarationAudit.DeclaredEnforced.Count.ShouldBe(16);
        (AgentToolScopeDeclarationAudit.ObjectDeterminedGap.Count +
         AgentToolScopeDeclarationAudit.DeclaredEnforced.Count).ShouldBe(19,
            "the object-addressed surface 241 declared is 16 closed plus 3 still open — the total must survive the move");
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
    public void CrossProject_IsDeclaredOnTheFilingDoorAlone_WhileTheFourIssueIdDoorsAreClosed()
    {
        var marked = _registry.Specs
            .Where(spec => spec.Scope == AgentToolScope.CrossProject)
            .Select(spec => spec.Name)
            .ToList();

        marked.ShouldBe(AgentToolScopeDeclarationAudit.CrossProject, ignoreOrder: true);
        marked.ShouldHaveSingleItem();
        Spec("issues_append").Scope.ShouldBe(AgentToolScope.Enforce);
        foreach (var door in new[] { "issues_get", "issues_set_status", "issues_promote", "issues_append" })
        {
            AgentToolScopeDeclarationAudit.DeclaredEnforced.ShouldContain(door,
                $"{door} addresses an issue by id and 246 T2 compares it — marking it CrossProject would " +
                "delete a tracked hole, leaving it in the gap would deny a closed door");
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
            AgentToolScopeDeclarationAudit.DeclaredEnforced.ShouldContain(name,
                $"{name} is the dangerous shape 246 closed: an own projectId satisfied the argument check while " +
                "the artifactId could name any board");
            AgentToolScopeDeclarationAudit.BucketOf(name).ShouldBe("DeclaredEnforced");
        }
    }

    [Fact]
    public void Gap_TheObjectDeterminedSetIsWhatTheRuleDerives()
    {
        // Deliberately policy-blind: a door addressed by object id is object-addressed whatever scope
        // someone declares for it, so the derived set cannot shrink when a door is closed — only the
        // bucket it is filed in moves.
        var derived = _registry.Specs
            .Where(AgentToolScopeDeclarationAudit.ObjectDetermined)
            .Select(spec => spec.Name)
            .ToList();

        var filed = AgentToolScopeDeclarationAudit.ObjectDeterminedGap
            .Concat(AgentToolScopeDeclarationAudit.DeclaredEnforced)
            .ToList();

        derived
            .Where(name => !AgentToolScopeDeclarationAudit.ScopedHandleExceptions.Contains(name))
            .ShouldBe(filed, ignoreOrder: true);
        filed.Count.ShouldBe(19,
            "16 doors 246 closed plus the 3 kit doors still declared open — the surface 241 measured must be whole");
        derived.ShouldContain("ack_entry",
            "ack_entry carries a required projectId with the handle and is exempt only because the service scopes it");
    }

    [Fact]
    public void Gap_PlanningValidateIsDeclaredDeliberately_AsAnObjectDeterminedHumanDoor()
    {
        var spec = Spec("planning_validate");

        spec.Category.ShouldBe(AgentToolCategory.HumanGated);
        spec.Scope.ShouldBe(AgentToolScope.Enforce, "human-gating answers who may call, not which project it may touch");
        AgentToolScopeDeclarationAudit.BucketOf("planning_validate").ShouldBe("DeclaredEnforced",
            "human-gating answers who may call, not which project it may touch — 246 T1 closed the object check");
        AgentToolScopeDeclarationAudit.CarriesProjectArgument(spec).ShouldBeFalse();
    }

    // AC-2: the kit doors are the remaining hole, and the reason is a fact about the schema, not a sentence
    // in a comment — so the test reads the entity. If a ProjectId ever appears on SkillKit, this goes red and
    // the three doors have to move to DeclaredEnforced with real enforcement behind them.
    [Fact]
    public void Gap_KitDoorsStayDeclared_WithTheReasonThatActuallyMakesThemOpen()
    {
        AgentToolScopeDeclarationAudit.ObjectDeterminedGap.ShouldBe(
            ["kit_import", "kit_import_scan", "kit_reimport"], ignoreOrder: true);

        typeof(Overmind.Core.SkillKit).GetProperty("ProjectId").ShouldBeNull(
            "a kit is a global library object; without an owning project there is nothing to compare a " +
            "conversation's scope against, which is why these three are declared open rather than closed");

        foreach (var name in AgentToolScopeDeclarationAudit.ObjectDeterminedGap)
        {
            Spec(name).ScopedInvoke.ShouldBeNull(
                $"{name} is declared a gap — a scoped handler would mean the bucket is lying");
        }
    }

    // AC-4: the counted surface is derived from schemas, never from the policy a door declares. Marking a
    // closed door CrossProject (or anything else) must not make the tracked set shrink by one.
    [Theory]
    [InlineData(AgentToolScope.Enforce)]
    [InlineData(AgentToolScope.CrossProject)]
    [InlineData(AgentToolScope.SessionScoped)]
    [InlineData(AgentToolScope.Discovery)]
    public void Coverage_CountedSurfaceIsBlindToTheDeclaredPolicy(AgentToolScope policy)
    {
        var mutated = ReplaceInRegistry("planning_read_artifact",
            Spec("planning_read_artifact", """
                {"type":"object","properties":{"artifactId":{"type":"string"}},"required":["artifactId"]}
                """, scope: policy));

        var counted = mutated
            .Where(AgentToolScopeDeclarationAudit.ObjectDetermined)
            .Where(spec => !AgentToolScopeDeclarationAudit.ScopedHandleExceptions.Contains(spec.Name))
            .Count();

        counted.ShouldBe(19,
            $"re-declaring a door as {policy} changed the counted object-addressed surface — the count would " +
            "then be a function of prose, not of what the schemas can name");
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
    public void Tripwire_NewIdAddressedSpecWithoutDeclaration_FailsTheAuditTwice()
    {
        var probe = Spec("issues_unassign", """
            {"type":"object","properties":{"issueId":{"type":"string"}},"required":["issueId"]}
            """);
        var mutated = AppendToRegistry(probe);

        var violations = ViolationsFor(mutated);

        // A new door cannot pass silently: it is unclassified, and the coverage invariant goes red by itself
        // because 19 declared object doors no longer add up to a registry that grew by one.
        violations.ShouldContain(v => v.Contains("unclassified") && v.Contains("issues_unassign"),
            "the audit must name the undeclared door, not just report a count mismatch");

        var declaredTotal = AgentToolScopeDeclarationAudit.AllDeclared().Values.Sum(bucket => bucket.Count);
        declaredTotal.ShouldNotBe(mutated.Count,
            $"declared {declaredTotal} vs registry {mutated.Count}: the buckets are exhaustive by construction, " +
            "so an undeclared door breaks the total even before anyone reads the violation list");
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
