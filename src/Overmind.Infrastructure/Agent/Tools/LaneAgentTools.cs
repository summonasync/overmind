using System.Text.Json;

namespace Overmind.Infrastructure.Agent.Tools;

public static class LaneAgentTools
{
    private static string LanesPath(Guid projectId) => $"/api/agent/projects/{projectId}/planning/lanes";

    public static IReadOnlyList<AgentToolSpec> Build(AgentLoopbackClient api)
    {
        return
        [
            ListSpec(api),
            SetSpec(api),
            UpdateSpec(api),
            RemoveSpec(api)
        ];
    }

    private static AgentToolSpec ListSpec(AgentLoopbackClient api) => new(
        "planning_list_lanes",
        "Reads the project's lane roster with the live projection per lane: contour, priority, bound alias, live surfaces, holders and the features in each lane.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"}},"required":["projectId"]}
        """,
        async (args, ct) => await api.GetAsync(LanesPath(AgentToolArgs.RequiredProjectId(args)), ct));

    private static AgentToolSpec SetSpec(AgentLoopbackClient api) => new(
        "planning_set_lanes",
        "Reshapes the lane roster as an upsert of the named lanes only — lanes absent from the payload are never touched; an empty array is a no-op. Setting lanes never creates lanes; features join via planning_update_feature.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"lanes":{"type":"array","items":{"type":"object"}}},"required":["projectId","lanes"]}
        """,
        async (args, ct) =>
        {
            var rawLanes = AgentToolArgs.OptionalRaw(args, "lanes");
            if (rawLanes is not { } lanes || lanes.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException("Argument 'lanes' must be an array of lane objects.");
            }

            return await api.PutAsync(
                $"{LanesPath(AgentToolArgs.RequiredProjectId(args))}-roster",
                new { lanes },
                ct);
        });

    private static AgentToolSpec UpdateSpec(AgentLoopbackClient api) => new(
        "planning_update_lane",
        "Updates one lane granularly by name: contour, serialOrder, priority, boundAlias, liveSurfaces. Unknown names are refused — lanes are born only through planning_update_feature.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"name":{"type":"string"},"contour":{"type":"string"},"serialOrder":{"type":"integer"},"priority":{"type":"integer"},"boundAlias":{"type":"string"},"liveSurfaces":{"type":"array","items":{"type":"object"}}},"required":["projectId","name"]}
        """,
        async (args, ct) => await api.PutAsync(
            $"{LanesPath(AgentToolArgs.RequiredProjectId(args))}/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "name"))}",
            new AgentToolPayload()
                .Set("contour", AgentToolArgs.OptionalString(args, "contour"))
                .Set("serialOrder", AgentToolArgs.OptionalInt(args, "serialOrder"))
                .Set("priority", AgentToolArgs.OptionalInt(args, "priority"))
                .Set("boundAlias", AgentToolArgs.OptionalString(args, "boundAlias"))
                .Set("liveSurfaces", AgentToolArgs.OptionalRaw(args, "liveSurfaces"))
                .Build(),
            ct));

    private static AgentToolSpec RemoveSpec(AgentLoopbackClient api) => new(
        "planning_remove_lane",
        "Removes one lane from the roster — refused while the lane still holds open features; the lane retires itself when its last open feature leaves or closes.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"name":{"type":"string"}},"required":["projectId","name"]}
        """,
        async (args, ct) => await api.DeleteAsync(
            $"{LanesPath(AgentToolArgs.RequiredProjectId(args))}/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "name"))}",
            ct));
}
