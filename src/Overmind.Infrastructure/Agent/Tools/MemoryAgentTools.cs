using System.Text.Json;
using Overmind.Infrastructure.Grimoire;

namespace Overmind.Infrastructure.Agent.Tools;

public static class MemoryAgentTools
{
    public static IReadOnlyList<AgentToolSpec> Build(IGrimoireMemoryClient memory)
    {
        return
        [
            RememberSpec(memory),
            NoteSpec(memory),
            RecallSpec(memory)
        ];
    }

    private static AgentToolSpec RememberSpec(IGrimoireMemoryClient memory) => new(
        "memory_remember",
        "Writes a durable memory and returns the stored record id, tome and supersede outcome. Lands in the " +
        "agent's personal tome by default; pass target=crew only when the human explicitly says the fact is for " +
        "the team (\"remember for the team\") or it is a hard crew-level decision. Durable facts in English, " +
        "verbatim quotes preserved, never secrets. When the response carries nearDuplicates naming the same fact " +
        "restated, rewrite with supersedes set to that memoryId instead of keeping both.",
        """
        {"type":"object","properties":{"content":{"type":"string"},"target":{"type":"string","enum":["personal","crew"]},"scope":{"type":"string"},"kind":{"type":"string"},"importance":{"type":"number"},"tags":{"type":"array","items":{"type":"string"}},"supersedes":{"type":"string"}},"required":["content"]}
        """,
        async (args, ct) =>
        {
            var content = AgentToolArgs.RequiredString(args, "content");
            var endpoint = await memory.ResolveEndpointAsync(TargetOf(args), ct);
            var write = await memory.RememberAsync(
                endpoint,
                new GrimoireMemoryWriteRequest(
                    content,
                    ScopeOf(args, fallback: "global"),
                    (AgentToolArgs.OptionalString(args, "kind") ?? "journal").ToLowerInvariant(),
                    ReadTags(args),
                    ClampImportance(AgentToolArgs.OptionalDouble(args, "importance")),
                    AgentToolArgs.OptionalGuid(args, "supersedes")),
                ct);
            return WriteResult(write, endpoint);
        },
        Scope: AgentToolScope.SessionScoped);

    private static AgentToolSpec NoteSpec(IGrimoireMemoryClient memory) => new(
        "memory_note",
        "Appends a journal note to the agent's personal tome: dated observations, progress records, " +
        "less-than-eternal context. Zero ceremony — the server classifies the topic; for standing decisions use " +
        "memory_remember instead.",
        """
        {"type":"object","properties":{"content":{"type":"string"},"tags":{"type":"array","items":{"type":"string"}}},"required":["content"]}
        """,
        async (args, ct) =>
        {
            var content = AgentToolArgs.RequiredString(args, "content");
            var endpoint = await memory.ResolveEndpointAsync(GrimoireTomeTarget.Personal, ct);
            var write = await memory.RememberAsync(
                endpoint,
                new GrimoireMemoryWriteRequest(
                    content, "global", "journal", ReadTags(args), 0.5, null),
                ct);
            return WriteResult(write, endpoint);
        },
        Scope: AgentToolScope.SessionScoped);

    private static AgentToolSpec RecallSpec(IGrimoireMemoryClient memory) => new(
        "memory_recall",
        "Searches the agent's personal tome and the shared crew tome semantically before assuming: ranked matches " +
        "over active records, superseded and tombstoned never surface, every hit labeled with its source tome. " +
        "Narrow with kind, tag and limit; every hit bumps the record's access stats.",
        """
        {"type":"object","properties":{"query":{"type":"string"},"kind":{"type":"string"},"tag":{"type":"string"},"limit":{"type":"integer"}},"required":["query"]}
        """,
        async (args, ct) =>
        {
            var query = AgentToolArgs.RequiredString(args, "query");
            var kind = AgentToolArgs.OptionalString(args, "kind");
            var tag = AgentToolArgs.OptionalString(args, "tag");
            var limit = Math.Max(1, AgentToolArgs.OptionalInt(args, "limit") ?? 5);

            var hits = new List<object>(limit);
            var failures = new List<string>();
            foreach (var (target, source) in new[]
                     {
                         (GrimoireTomeTarget.Personal, "personal"),
                         (GrimoireTomeTarget.Crew, "crew"),
                     })
            {
                try
                {
                    var endpoint = await memory.ResolveEndpointAsync(target, ct);
                    var records = await memory.RecallAsync(endpoint, query, kind, tag, limit, ct);
                    hits.AddRange(records.Select(record => (object)new
                    {
                        memoryId = record.Id,
                        content = record.Content,
                        kind = record.Kind,
                        tags = record.Tags,
                        createdAt = record.CreatedAt,
                        source,
                    }));
                }
                catch (GrimoireMemoryException ex)
                {
                    failures.Add($"{source}: {ex.Message}");
                }
            }

            if (failures.Count == 2)
            {
                throw new GrimoireMemoryException(string.Join(" | ", failures));
            }

            return JsonSerializer.SerializeToElement(hits.Take(limit));
        },
        Scope: AgentToolScope.SessionScoped);

    private static GrimoireTomeTarget TargetOf(JsonElement args)
    {
        var raw = (AgentToolArgs.OptionalString(args, "target") ?? "personal").Trim().ToLowerInvariant();
        return raw switch
        {
            "personal" => GrimoireTomeTarget.Personal,
            "crew" => GrimoireTomeTarget.Crew,
            _ => throw new ArgumentException($"'target' must be 'personal' or 'crew', got '{raw}'"),
        };
    }

    private static JsonElement WriteResult(GrimoireMemoryWriteResult write, GrimoireMemoryEndpoint endpoint) =>
        JsonSerializer.SerializeToElement(new
        {
            memoryId = write.Id,
            tome = write.Tome,
            actor = $"{endpoint.ActorAlias}+{EmbeddedAgentSessionRegistrar.Vendor}",
            write.SupersededId,
        });

    private static string ScopeOf(JsonElement args, string fallback) =>
        (AgentToolArgs.OptionalString(args, "scope") ?? fallback).ToLowerInvariant();

    private static IReadOnlyList<string> ReadTags(JsonElement args) =>
        AgentToolArgs.OptionalRaw(args, "tags") is not { } raw || raw.ValueKind != JsonValueKind.Array
            ? []
            : [.. raw.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))];

    private static double ClampImportance(double? importance) => importance is null
        ? 0.5
        : Math.Clamp(importance.Value, 0, 1);
}
