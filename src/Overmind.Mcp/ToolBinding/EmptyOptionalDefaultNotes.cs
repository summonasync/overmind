namespace Overmind.Mcp.ToolBinding;

// 266/T3 row 3 — an OPTIONAL parameter that arrives empty ("" or whitespace) used to vanish: the
// adapter drops it and the documented behaviour takes over without a word. The behaviour itself is
// byte-stable and unchanged here; what changes is that the caller now hears about it.
// A row is allowed ONLY where a value is actually substituted — something the caller can be told by
// name. A door that simply has no bound when the parameter is missing has no default to name, and
// writing its behaviour here is how a row rots: the "overmind_replies.since → replies from the last
// 7 days" entry asserted a bound the door never had (measured at
// src/Overmind.Infrastructure/Notifications/NotificationsService.cs:119-138 — the since filter runs
// only when since is present, and the reply set is capped by Take(ListCapacity), newest first). The
// row is gone; the generic note covers it, and it cannot lie about a value because it names none.
// Every remaining row cites the code that applies it, and EmptyOptionalNotesTableTests pins both the
// substitution rows and the absence of the since row.
internal static class EmptyOptionalDefaultNotes
{
    private static readonly (string Tool, string Parameter, string Default)[] Known =
    [
        // src/Overmind.Mcp/OvermindTools.cs:360 substitutes it, :362 puts it on the wire.
        ("search", "mode", "mode=fulltext"),
        // src/Overmind.Mcp/Planning/PlanningFeatureTools.cs:170 drops a blank slug; the Host composes
        // '{NNN}-{title-derived}' at PlanningFeatureService.cs:59-63 (FeatureSlugGenerator.Compose).
        ("planning_create_feature", "slug", "the generated '{NNN}-{title-derived}' slug"),
    ];

    public static string? Lookup(string toolName, string parameter) =>
        Known.FirstOrDefault(row => row.Tool == toolName && row.Parameter == parameter).Default;

    // The generic half never claims a value exists — it says what mechanically happens: the empty
    // argument is dropped and the call goes out exactly as if the parameter had been omitted.
    public static string Note(string parameter, string? documentedDefault) =>
        documentedDefault is null
            ? $"'{parameter}' arrived empty and was dropped — the call goes out as if you had omitted " +
              "it, and what the door does then is what this parameter's description documents"
            : $"'{parameter}' arrived empty and was dropped — {documentedDefault} applies";
}
