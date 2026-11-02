using System.Text.Json;

namespace Overmind.Infrastructure.Agent.Tools;

public static class KitAgentTools
{
    public static IReadOnlyList<AgentToolSpec> Build(AgentLoopbackClient api)
    {
        return
        [
            ListSpec(api),
            ImportScanSpec(api),
            ImportSpec(api),
            ReimportSpec(api)
        ];
    }

    private static AgentToolSpec ListSpec(AgentLoopbackClient api) => new(
        "kits_list",
        "Lists the kit library: every kit with id, name, description, entries and platform coverage.",
        """
        {"type":"object","properties":{}}
        """,
        async (_, ct) => await api.GetAsync("/api/kits", ct));

    private static AgentToolSpec ImportScanSpec(AgentLoopbackClient api) => new(
        "kit_import_scan",
        "Scans a skills source folder for importable kits: per-skill candidates with rule counts, issues and suggested names. Path is an absolute skills directory.",
        """
        {"type":"object","properties":{"path":{"type":"string"}},"required":["path"]}
        """,
        async (args, ct) => await api.PostAsync(
            "/api/kits/import/scan",
            new { path = AgentToolArgs.RequiredString(args, "path") },
            ct));

    private static AgentToolSpec ImportSpec(AgentLoopbackClient api) => new(
        "kit_import",
        "Imports kits from a scanned source: an absolute path plus the selected kits (relativePath + name each); import is dry-run-free and reports per-kit success or failure.",
        """
        {"type":"object","properties":{"path":{"type":"string"},"kits":{"type":"array","items":{"type":"object"}}},"required":["path","kits"]}
        """,
        async (args, ct) =>
        {
            var rawKits = AgentToolArgs.OptionalRaw(args, "kits");
            if (rawKits is not { } kits || kits.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException("Argument 'kits' must be an array of { relativePath, name } selections.");
            }

            return await api.PostAsync(
                "/api/kits/import",
                new AgentToolPayload()
                    .Set("path", AgentToolArgs.RequiredString(args, "path"))
                    .Set("kits", kits)
                    .Build(),
                ct);
        });

    private static AgentToolSpec ReimportSpec(AgentLoopbackClient api) => new(
        "kit_reimport",
        "Re-imports one kit from its tracked source location — refreshes skills after the source moved or changed.",
        """
        {"type":"object","properties":{"kitId":{"type":"string"}},"required":["kitId"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"/api/kits/{AgentToolArgs.RequiredGuid(args, "kitId")}/reimport",
            new { },
            ct));
}
