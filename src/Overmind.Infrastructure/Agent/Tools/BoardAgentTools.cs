namespace Overmind.Infrastructure.Agent.Tools;

public static class BoardAgentTools
{
    public static IReadOnlyList<AgentToolSpec> Build(AgentLoopbackClient api)
    {
        return
        [
            ListProjectsSpec(api),
            GetProjectSpec(api),
            NotifySpec(api),
            RepliesSpec(api),
            WakeSpec(api)
        ];
    }

    private static AgentToolSpec ListProjectsSpec(AgentLoopbackClient api) => new(
        "list_projects",
        "Lists all overmind projects with id, name, disk path and description — the starting point for any board work.",
        """
        {"type":"object","properties":{}}
        """,
        async (_, ct) => await api.GetAsync("/api/projects", ct),
        Scope: AgentToolScope.Discovery);

    private static AgentToolSpec GetProjectSpec(AgentLoopbackClient api) => new(
        "read_project",
        "Reads one project: id, name, disk path, description, manifest, number style and favorite state.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"}},"required":["projectId"]}
        """,
        async (args, ct) => await api.GetAsync($"/api/projects/{AgentToolArgs.RequiredProjectId(args)}", ct));

    private static AgentToolSpec NotifySpec(AgentLoopbackClient api) => new(
        "notify_human",
        "Tells the human something through the notification center they watch: info, question or approval. Questions carry 2-3 concrete proposed answers plus a recommendation — never a bare question. A deep link opens on featureSlug when given.",
        """
        {"type":"object","properties":{"kind":{"type":"string"},"text":{"type":"string"},"projectId":{"type":"string"},"featureSlug":{"type":"string"},"tab":{"type":"string"},"from":{"type":"string"}},"required":["kind","text"]}
        """,
        async (args, ct) => await api.PostAsync(
            "/api/agent/notifications",
            new AgentToolPayload()
                .Set("kind", AgentToolArgs.RequiredString(args, "kind"))
                .Set("text", AgentToolArgs.RequiredString(args, "text"))
                .Set("projectId", AgentToolArgs.OptionalGuid(args, "projectId"))
                .Set("featureSlug", AgentToolArgs.OptionalString(args, "featureSlug"))
                .Set("tab", AgentToolArgs.OptionalString(args, "tab"))
                .Set("from", AgentToolArgs.OptionalString(args, "from"))
                .Build(),
            ct),
            // D5, owner ruling 2026-11-17: this projectId is a deep-link field on a message for the human, not a
            // board the agent acts on - refusing it would claim a protection with nothing behind it.
            Scope: AgentToolScope.Discovery);

    private static AgentToolSpec RepliesSpec(AgentLoopbackClient api) => new(
        "poll_replies",
        "Polls for the human's answers to your notifications, newest first — each item's replyText is their decision. Optional since, kind and featureSlug filters.",
        """
        {"type":"object","properties":{"since":{"type":"string"},"kind":{"type":"string"},"featureSlug":{"type":"string"}},"required":[]}
        """,
        async (args, ct) => await api.GetAsync(
            AgentToolArgs.BuildQuery("/api/agent/notifications/replies", args, "since", "kind", "featureSlug"), ct),
        Scope: AgentToolScope.SessionScoped);

    private static AgentToolSpec WakeSpec(AgentLoopbackClient api) => new(
        "wake_peek",
        "Cheap side-effect-free check of pending signals: human replies and wave chat mentions. Pass the returned cursor back in as since to page forward.",
        """
        {"type":"object","properties":{"since":{"type":"string"}},"required":[]}
        """,
        async (args, ct) => await api.GetAsync(
            AgentToolArgs.BuildQuery("/api/agent/wake", args, "since"), ct),
        Scope: AgentToolScope.SessionScoped);
}
