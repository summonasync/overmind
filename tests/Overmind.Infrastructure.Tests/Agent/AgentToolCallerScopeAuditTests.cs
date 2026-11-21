using System.Text.Json;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Tools;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class AgentToolCallerScopeAuditTests
{
    private static readonly JsonElement Empty = JsonDocument.Parse("{}").RootElement.Clone();

    [Fact]
    public void Tripwire_ScopedDoorKeepingAnUnscopedDelegatedHandler_IsFlagged()
    {
        var misdeclared = Spec("planning_validate",
            delegatedInvoke: (_, _, _) => Task.FromResult(Empty),
            scopedInvoke: (_, _, _) => Task.FromResult(Empty));

        var violations = AgentToolScopeDeclarationAudit.Audit([misdeclared]);

        violations.ShouldContain(v =>
            v.Contains("planning_validate") && v.Contains("without the conversation scope"),
            "the armed branch would dispatch the door unscoped — that is the bypass 246 T0 exists to close");
    }

    [Fact]
    public void Tripwire_DelegatedScopedHandlerWithoutAPlainScopedHandler_IsFlagged()
    {
        var misdeclared = Spec("planning_patch_artifact",
            delegatedScopedInvoke: (_, _, _, _) => Task.FromResult(Empty));

        AgentToolScopeDeclarationAudit.Audit([misdeclared]).ShouldContain(v =>
            v.Contains("planning_patch_artifact") && v.Contains("delegation is off"));
    }

    [Fact]
    public void Tripwire_TheConvertedDoors_HarmlessToTheAudit()
    {
        var armed = Spec("planning_validate",
            delegatedScopedInvoke: (_, _, _, _) => Task.FromResult(Empty),
            scopedInvoke: (_, _, _) => Task.FromResult(Empty));
        var plain = Spec("planning_read_artifact",
            scopedInvoke: (_, _, _) => Task.FromResult(Empty));

        AgentToolScopeDeclarationAudit.Audit([armed, plain])
            .ShouldNotContain(v => v.Contains("caller-scoped"));
    }

    private static AgentToolSpec Spec(
        string name,
        AgentDelegatedToolHandler? delegatedInvoke = null,
        AgentScopedToolHandler? scopedInvoke = null,
        AgentDelegatedScopedToolHandler? delegatedScopedInvoke = null) => new(
        name, "synthetic spec used by a caller-scope tripwire",
        """{"type":"object","properties":{"artifactId":{"type":"string"}},"required":["artifactId"]}""",
        (_, _) => Task.FromResult(Empty),
        DelegatedInvoke: delegatedInvoke,
        ScopedInvoke: scopedInvoke,
        DelegatedScopedInvoke: delegatedScopedInvoke);
}
