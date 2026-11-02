namespace Overmind.Infrastructure.Agent.Tools;

public static class WaveAgentTools
{
    private static string RoomsPath(Guid projectId) => $"/api/agent/projects/{projectId}/wave/rooms";

    public static IReadOnlyList<AgentToolSpec> Build(AgentLoopbackClient api, EmbeddedAgentSessionState state)
    {
        return
        [
            PostSpec(api),
            UnreadSpec(api),
            PeekSpec(api),
            HistorySpec(api),
            RoomsSpec(api),
            RoleSetSpec(api, state)
        ];
    }

    private static AgentToolSpec PostSpec(AgentLoopbackClient api) => new(
        "wave_chat_post",
        "Posts one signal to a wave chat room: a lane room by roster name or the shared 'all' room. Signal-only etiquette: blocker, critical spot, handoff or broadcast; an @alias mention means the addressee must read this; no chatter. Capped at 500 characters.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"room":{"type":"string"},"text":{"type":"string"}},"required":["projectId","room","text"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{RoomsPath(AgentToolArgs.RequiredProjectId(args))}/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "room"))}/messages",
            new { text = AgentToolArgs.RequiredString(args, "text") },
            ct));

    private static AgentToolSpec UnreadSpec(AgentLoopbackClient api) => new(
        "wave_chat_unread",
        "Fetches the calling alias's unread wave chat feed and advances its watermarks. Optional rooms filter (comma-separated), since datetime, page limit and replayMentions.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"room":{"type":"string"},"since":{"type":"string"},"limit":{"type":"integer"},"replayMentions":{"type":"boolean"}},"required":["projectId","room"]}
        """,
        async (args, ct) => await api.GetAsync(
            AgentToolArgs.BuildQuery(
                $"{RoomsPath(AgentToolArgs.RequiredProjectId(args))}/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "room"))}/unread",
                args, "since", "limit", "replayMentions"),
            ct));

    private static AgentToolSpec PeekSpec(AgentLoopbackClient api) => new(
        "wave_chat_peek",
        "Counts the calling alias's undelivered wave chat traffic per room without advancing any watermark — read fully with wave_chat_unread only where count > 0.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"room":{"type":"string"}},"required":["projectId","room"]}
        """,
        async (args, ct) => await api.GetAsync(
            $"{RoomsPath(AgentToolArgs.RequiredProjectId(args))}/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "room"))}/peek",
            ct));

    private static AgentToolSpec HistorySpec(AgentLoopbackClient api) => new(
        "wave_chat_history",
        "Reads the message history of one wave room — optional limit and before datetime for paging backward.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"room":{"type":"string"},"limit":{"type":"integer"},"before":{"type":"string"}},"required":["projectId","room"]}
        """,
        async (args, ct) => await api.GetAsync(
            AgentToolArgs.BuildQuery(
                $"{RoomsPath(AgentToolArgs.RequiredProjectId(args))}/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "room"))}/messages",
                args, "limit", "before"),
            ct));

    private static AgentToolSpec RoomsSpec(AgentLoopbackClient api) => new(
        "wave_rooms",
        "Lists the wave chat rooms of a project: roster lanes plus the shared 'all' room, with last-activity stamps.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"}},"required":["projectId"]}
        """,
        async (args, ct) => await api.GetAsync(RoomsPath(AgentToolArgs.RequiredProjectId(args)), ct));

    private static AgentToolSpec RoleSetSpec(AgentLoopbackClient api, EmbeddedAgentSessionState state) => new(
        "wave_role_set",
        "Sets the calling session's wave chat role: worker (default) or marshal — the wave lead who reads the board across all lanes and issues move/wait decisions. Take marshal only when actually marshalling the wave.",
        """
        {"type":"object","properties":{"role":{"type":"string"}},"required":["role"]}
        """,
        async (args, ct) =>
        {
            if (!state.IsRegistered)
            {
                throw new ArgumentException(
                    "The embedded agent session is not registered yet; wave role changes need a session.");
            }

            return await api.PostAsync(
                $"/api/agent/sessions/{state.SessionId}/role",
                new { role = AgentToolArgs.RequiredString(args, "role") },
                ct);
        });
}
