using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Overmind.Mcp.ToolBinding;

// The empty-optional echo (266/T3 row 3): a call that passes `mode: ""` or `slug: "  "` is not
// asking for an empty value — the adapter drops it and the documented default runs. Dropping it
// silently left the caller guessing why their filter did nothing, so the note now rides the result
// as a second text block: the first block (the payload itself) stays byte-identical.
internal static class OptionalDefaults
{
    // Empty is not "dropped" for these: the tool documents the empty string as the explicit clear,
    // and the adapter forwards it on purpose. Calling that a silent default would be a lie.
    // (planning_update_feature.lane: "" → PlanningFeatureService.ResolveLaneAsync clears;
    // planning_pin_feature_code.*: BlankToNull at src/Overmind.Mcp/Planning/PlanningFeatureTools.cs:496.)
    private static readonly HashSet<string> MeaningfulWhenEmpty =
    [
        "planning_update_feature.lane",
        "planning_pin_feature_code.branch",
        "planning_pin_feature_code.worktreePath",
    ];

    public static CallToolResult Annotate(
        CallToolResult result, IDictionary<string, JsonElement>? arguments, string toolName, ToolContract contract)
    {
        if (result.IsError == true || arguments is null || !contract.Closed)
        {
            return result;
        }

        List<string>? notes = null;
        foreach (var (name, value) in arguments)
        {
            if (contract.Required.Contains(name) || !contract.Parameters.ContainsKey(name)
                || MeaningfulWhenEmpty.Contains($"{toolName}.{name}"))
            {
                continue;
            }

            if (value.ValueKind != JsonValueKind.String || value.GetString()?.Trim().Length > 0)
            {
                continue;
            }

            notes ??= [];
            notes.Add(EmptyOptionalDefaultNotes.Note(name, EmptyOptionalDefaultNotes.Lookup(toolName, name)));
        }

        if (notes is null)
        {
            return result;
        }

        result.Content.Add(new TextContentBlock
        {
            Text = $"Warnings: {string.Join("; ", notes)}. Nothing else about the call changed."
        });
        return result;
    }
}
