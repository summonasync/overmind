using System.Text.Json;

namespace Overmind.Infrastructure.Agent.Tools;

public static class IssueAgentTools
{
    public static IReadOnlyList<AgentToolSpec> Build(AgentLoopbackClient api)
    {
        return
        [
            CreateSpec(api),
            ListSpec(api),
            GetSpec(api),
            AppendSpec(api),
            SetStatusSpec(api),
            PromoteSpec(api)
        ];
    }

    private static AgentToolSpec CreateSpec(AgentLoopbackClient api) => new(
        "issues_create",
        "Files an issue on the board of project projectId — the cross-project protocol: need a fix in project B while working in A? File an issue and do not touch their repo. Optional kind: issue (default) or request. Body sections '## Repro', '## Expected', '## Actual' recommended.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"authorProjectId":{"type":"string"},"title":{"type":"string"},"body":{"type":"string"},"kind":{"type":"string"}},"required":["projectId","authorProjectId","title","body"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"/api/agent/projects/{AgentToolArgs.RequiredProjectId(args)}/issues",
            new AgentToolPayload()
                .Set("authorProjectId", AgentToolArgs.RequiredGuid(args, "authorProjectId"))
                .Set("title", AgentToolArgs.RequiredString(args, "title"))
                .Set("body", AgentToolArgs.RequiredString(args, "body"))
                .Set("kind", AgentToolArgs.OptionalString(args, "kind"))
                .Build(),
            ct),
            // The only CrossProject spec in the registry: filing on another team's board is the protocol,
            // speaking for another project is not — AgentToolRegistry.ProvenanceRefusalFor enforces the author.
            Scope: AgentToolScope.CrossProject);

    private static AgentToolSpec ListSpec(AgentLoopbackClient api) => new(
        "issues_list",
        "Lists issues on the board of project projectId — metadata rows only: id, title, status, author provenance, appendCount, kind. Optional filters: status, authorProjectId, kind.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"status":{"type":"string"},"authorProjectId":{"type":"string"},"kind":{"type":"string"}},"required":["projectId"]}
        """,
        async (args, ct) => await api.GetAsync(
            AgentToolArgs.BuildQuery(
                $"/api/agent/projects/{AgentToolArgs.RequiredProjectId(args)}/issues",
                args, "status", "authorProjectId", "kind"),
            ct));

    private static AgentToolSpec GetSpec(AgentLoopbackClient api)
    {
        string IssuePath(JsonElement args) => $"/api/agent/issues/{AgentToolArgs.RequiredGuid(args, "issueId")}";

        return new(
            "issues_get",
            "Reads one issue in full: card (title, body, status, author provenance, lifecycle timestamps) plus the chronological append trail.",
            """
            {"type":"object","properties":{"issueId":{"type":"string"}},"required":["issueId"]}
            """,
            async (args, ct) => await api.GetAsync(IssuePath(args), ct),
            ScopedInvoke: async (args, callerProjectId, ct) => await api.GetAsync(
                IssuePath(args), new AgentCaller(callerProjectId), ct));
    }

    private static AgentToolSpec AppendSpec(AgentLoopbackClient api) => new(
        "issues_append",
        "Appends a follow-up to an issue — the trail is append-only and moves into the task's obstacles when the issue is promoted.",
        """
        {"type":"object","properties":{"issueId":{"type":"string"},"authorProjectId":{"type":"string"},"text":{"type":"string"}},"required":["issueId","authorProjectId","text"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"/api/agent/issues/{AgentToolArgs.RequiredGuid(args, "issueId")}/append",
            AppendPayload(args),
            ct),
        ScopedInvoke: async (args, callerProjectId, ct) => await api.PostAsync(
            $"/api/agent/issues/{AgentToolArgs.RequiredGuid(args, "issueId")}/append",
            AppendPayload(args),
            new AgentCaller(callerProjectId),
            ct));

    internal static object AppendPayload(JsonElement args) => new AgentToolPayload()
        .Set("authorProjectId", AgentToolArgs.RequiredGuid(args, "authorProjectId"))
        .Set("text", AgentToolArgs.RequiredString(args, "text"))
        .Build();

    private static AgentToolSpec SetStatusSpec(AgentLoopbackClient api) => new(
        "issues_set_status",
        "Moves an issue: open to triaged to promoted to closed. Promoted is refused here — use issues_promote. source is Agent or Ui.",
        """
        {"type":"object","properties":{"issueId":{"type":"string"},"status":{"type":"string"},"source":{"type":"string"}},"required":["issueId","status","source"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"/api/agent/issues/{AgentToolArgs.RequiredGuid(args, "issueId")}/status",
            StatusPayload(args),
            ct),
        ScopedInvoke: async (args, callerProjectId, ct) => await api.PostAsync(
            $"/api/agent/issues/{AgentToolArgs.RequiredGuid(args, "issueId")}/status",
            StatusPayload(args),
            new AgentCaller(callerProjectId),
            ct));

    internal static object StatusPayload(JsonElement args) => new AgentToolPayload()
        .Set("status", AgentToolArgs.RequiredString(args, "status"))
        .Set("source", AgentToolArgs.RequiredString(args, "source"))
        .Build();

    private static AgentToolSpec PromoteSpec(AgentLoopbackClient api) => new(
        "issues_promote",
        "Machine promotion: composes a task artifact on featureSlug from the issue body and sets the issue to promoted — traceable both ways. Only from open/triaged; a claimed target feature blocks promotion from any session that does not hold the claim.",
        """
        {"type":"object","properties":{"issueId":{"type":"string"},"featureSlug":{"type":"string"},"source":{"type":"string"}},"required":["issueId","featureSlug","source"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"/api/agent/issues/{AgentToolArgs.RequiredGuid(args, "issueId")}/promote",
            PromotePayload(args),
            ct),
        ScopedInvoke: async (args, callerProjectId, ct) => await api.PostAsync(
            $"/api/agent/issues/{AgentToolArgs.RequiredGuid(args, "issueId")}/promote",
            PromotePayload(args),
            new AgentCaller(callerProjectId),
            ct));

    internal static object PromotePayload(JsonElement args) => new AgentToolPayload()
        .Set("featureSlug", AgentToolArgs.RequiredString(args, "featureSlug"))
        .Set("source", AgentToolArgs.RequiredString(args, "source"))
        .Build();
}
