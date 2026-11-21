namespace Overmind.Mcp.ToolBinding;

// 266/T3 row 3 — an OPTIONAL parameter that arrives empty ("" or whitespace) used to vanish: the
// adapter drops it and the documented default applies without a word. The defaults themselves are
// byte-stable and unchanged here; what changes is that the caller now hears about it.
// This table names the default only where it was read in the code that applies it (the file:line
// beside each row is the measurement). Everything else gets the generic note, which points at the
// parameter's own description instead of inventing a value nobody verified.
internal static class EmptyOptionalDefaultNotes
{
    private static readonly (string Tool, string Parameter, string Default)[] Known =
    [
        // src/Overmind.Mcp/OvermindTools.cs:360 — mode is substituted adapter-side.
        ("search", "mode", "mode=fulltext"),
        // src/Overmind.Mcp/Planning/PlanningFeatureTools.cs:170 — a blank slug is dropped and the
        // Host generates '{NNN}-{title-derived}'.
        ("planning_create_feature", "slug", "the generated '{NNN}-{title-derived}' slug"),
        // src/Overmind.Mcp/OvermindNotificationTools.cs:78 — no since goes to the Host, whose
        // default lower bound is the last 7 days.
        ("overmind_replies", "since", "replies from the last 7 days"),
    ];

    public static string? Lookup(string toolName, string parameter) =>
        Known.FirstOrDefault(row => row.Tool == toolName && row.Parameter == parameter).Default;

    public static string Note(string parameter, string? documentedDefault) =>
        documentedDefault is null
            ? $"'{parameter}' arrived empty and was dropped — the default its own description documents applies"
            : $"'{parameter}' arrived empty and was dropped — {documentedDefault} applies";
}
