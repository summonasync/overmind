using System.Text.Json;

namespace Overmind.Infrastructure.Agent.Tools;

public static class EntryAgentTools
{
    public static IReadOnlyList<AgentToolSpec> Build(AgentLoopbackClient api)
    {
        return
        [
            ListEntriesSpec(api),
            ReadEntrySpec(api),
            WriteEntrySpec(api),
            UpdateEntrySpec(api),
            DeleteEntrySpec(api),
            AckEntrySpec(api),
            EntryVersionsSpec(api),
            SearchSpec(api)
        ];
    }

    private static AgentToolSpec ListEntriesSpec(AgentLoopbackClient api) => new(
        "list_entries",
        "Lists entries of a project: notes, decisions, gates, guards, ideas, audits, tombstones and journal notes. Optional type, tag and q (fulltext, capped at 100 hits) filters; most recently updated first.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"type":{"type":"string"},"tag":{"type":"string"},"q":{"type":"string"}},"required":["projectId"]}
        """,
        async (args, ct) => await api.GetAsync(
            AgentToolArgs.BuildQuery($"/api/projects/{AgentToolArgs.RequiredProjectId(args)}/entries", args, "type", "tag", "q"), ct));

    private static AgentToolSpec ReadEntrySpec(AgentLoopbackClient api) => new(
        "read_entry",
        "Reads a single entry by id, including content, tags and version.",
        """
        {"type":"object","properties":{"entryId":{"type":"string"}},"required":["entryId"]}
        """,
        async (args, ct) => await api.GetAsync($"/api/entries/{AgentToolArgs.RequiredGuid(args, "entryId")}", ct));

    private static AgentToolSpec WriteEntrySpec(AgentLoopbackClient api) => new(
        "write_entry",
        "Creates an entry through the agent door: type note, decision, gate, guard, idea, audit or tombstone; markdown body; optional tags. A hand-made tombstone buries an approach without deleting anything. Journal entries belong to the human surface and this door refuses them.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"type":{"type":"string"},"title":{"type":"string"},"content":{"type":"string"},"tags":{"type":"array","items":{"type":"string"}}},"required":["projectId","type","title","content"]}
        """,
        async (args, ct) => await api.PostAsync(
            "/api/agent/entries",
            new AgentToolPayload()
                .Set("projectId", AgentToolArgs.RequiredProjectId(args))
                .Set("type", AgentToolArgs.RequiredString(args, "type"))
                .Set("title", AgentToolArgs.RequiredString(args, "title"))
                .Set("content", AgentToolArgs.RequiredString(args, "content"))
                .Set("tags", AgentToolArgs.OptionalRaw(args, "tags"))
                .Build(),
            ct));

    private static AgentToolSpec UpdateEntrySpec(AgentLoopbackClient api) => new(
        "update_entry",
        "Replaces the content of an existing note, decision, gate, guard, idea, audit or tombstone through the agent door, keeping its title and tags.",
        """
        {"type":"object","properties":{"entryId":{"type":"string"},"content":{"type":"string"}},"required":["entryId","content"]}
        """,
        async (args, ct) => await api.PutAsync(
            $"/api/agent/entries/{AgentToolArgs.RequiredGuid(args, "entryId")}",
            new AgentToolPayload()
                .Set("title", AgentToolArgs.OptionalString(args, "title"))
                .Set("content", AgentToolArgs.RequiredString(args, "content"))
                .Build(),
            ct));

    // The embedded loop is thin by design: the write_entry and update_entry specs carry a client-side refusal for the
    // types the door will not take, and delete_entry does not pre-read — it trusts the route gate and surfaces the
    // server's typed refusal, so the loop pays no extra read per delete. The gate is the authority either way;
    // EntryAgentToolRouteTests pins this spec to the agent route.
    //
    // The reason rides the query string because that is where the delete route reads it — a DELETE body is bound
    // by nobody. The spec used to serialise it into a body, so every embedded tombstone was born reasonless while
    // the MCP twin (which sends ?reason=) worked; EmbeddedEntryToolDeleteApiTests is the seam that measures it.
    private static AgentToolSpec DeleteEntrySpec(AgentLoopbackClient api) => new(
        "delete_entry",
        "Deletes one entry through the agent door — a tombstone entry is born in its place carrying the title, the deletion date and the original content. Deleting a tombstone is a plain delete. Skills are refused — the UI owns them. Journal entries are the human surface: the door refuses them with JournalEntryWriteRefused.",
        """
        {"type":"object","properties":{"entryId":{"type":"string"},"reason":{"type":"string"}},"required":["entryId"]}
        """,
        async (args, ct) => await api.DeleteAsync(
            AgentToolArgs.BuildQuery(
                $"/api/agent/entries/{AgentToolArgs.RequiredGuid(args, "entryId")}", args, "reason"),
            ct));

    private static AgentToolSpec AckEntrySpec(AgentLoopbackClient api) => new(
        "ack_entry",
        "Acknowledges one open entry — the explicit read ack attributed to the calling session; the entry body is never touched.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"entryId":{"type":"string"}},"required":["projectId","entryId"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"/api/agent/projects/{AgentToolArgs.RequiredProjectId(args)}/entries/{AgentToolArgs.RequiredGuid(args, "entryId")}/ack",
            new { },
            ct));

    private static AgentToolSpec EntryVersionsSpec(AgentLoopbackClient api) => new(
        "entry_versions",
        "Reads the version history of one entry: version number, content and creation time per row.",
        """
        {"type":"object","properties":{"entryId":{"type":"string"}},"required":["entryId"]}
        """,
        async (args, ct) => await api.GetAsync(
            $"/api/entries/{AgentToolArgs.RequiredGuid(args, "entryId")}/versions", ct));

    private static AgentToolSpec SearchSpec(AgentLoopbackClient api) => new(
        "search",
        "Searches entries and planning artifacts by fulltext (or semantic mode when the vector store is up): hits carry source, title and a snippet window — read the full body separately.",
        """
        {"type":"object","properties":{"query":{"type":"string"},"mode":{"type":"string"},"projectId":{"type":"string"}},"required":["query"]}
        """,
        async (args, ct) => await api.GetAsync(
            AgentToolArgs.BuildQuery("/api/search", args, "query", "mode", "projectId"), ct));

}
