using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class LaneTools
{
    private const string RosterShapeError = "lanes must be an array of lane objects.";

    private readonly OvermindApiClient _api;
    private readonly MutationGuard _mutations;

    public LaneTools(OvermindApiClient api, MutationGuard mutations)
    {
        _api = api;
        _mutations = mutations;
    }

    [McpServerTool(Name = "planning_set_lanes", Destructive = false, Idempotent = false)]
    [Description("Replaces the project's lane roster wholesale — lanes are first-class planning data: named code contours owned by one agent each, serial within the lane. Full replace: lanes missing from the payload are deleted, known names (case-insensitive) are updated in place, new names are inserted; features keep their lane string when a lane is deleted — the field is a plain name reference, never an FK. Each item: name (required, max 64, unique per project), contour (free text description of the code contour), serialOrder (optional int, reserved — no behavior yet), priority (optional int 0-3, mirroring feature priorities p0-p3 — lane columns sort priority asc, nulls last; null = no priority). An empty array clears the roster. Features join a lane via planning_update_feature's lane field, which must name a roster entry. Max 64 lanes per project. Returns the resulting roster with per-lane holder and open-feature counts.")]
    public async Task<CallToolResult> SetLanes(
        [Description("Full replacement roster: an array of lane objects — name (required), contour, serialOrder, priority (optional 0-3, omit for none). An empty array clears the roster.")] JsonElement lanes,
        [Description("Id of the project the roster belongs to.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        if (projectId is null)
        {
            throw new McpException(PlanningToolText.ProjectIdRequired);
        }

        var payload = new JsonObject { ["lanes"] = BuildLaneItems(lanes) };

        var roster = await _mutations.SendAsync(() => _api.PutAsync(
            $"/api/agent/projects/{projectId}/planning/lanes",
            payload,
            ct), readback: null, ct);

        var count = roster.GetProperty("lanes").GetArrayLength();
        var names = roster.GetProperty("lanes")
            .EnumerateArray()
            .Select(lane => lane.GetProperty("name").GetString())
            .ToArray();
        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = count == 0
                        ? "Lane roster cleared."
                        : $"Lane roster holds {count} lane(s): {string.Join(", ", names)}."
                }
            ]
        };
    }

    [McpServerTool(Name = "planning_list_lanes", ReadOnly = true, Idempotent = true)]
    [Description("Reads the project's lane roster with the live projection per lane: name, contour, serialOrder (reserved, no behavior yet), priority (0-3, null = no priority — the board sorts lanes priority asc, nulls last), openFeatureCount (non-done features currently in the lane), holder (alias + vendor + claimState of the alive claim on any feature in the lane), and the features themselves (slug, status, claim identity, claimState). The wave marshal reads this before deciding who moves — a busy lane is one holder and serial lanes are one open feature at a time.")]
    public async Task<JsonElement> ListLanes(
        [Description("Id of the project whose roster to read.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        if (projectId is null)
        {
            throw new McpException(PlanningToolText.ProjectIdRequired);
        }

        try
        {
            return await _api.GetAsync($"/api/agent/projects/{projectId}/planning/lanes", ct);
        }
        catch (ApiStatusException ex)
        {
            throw new McpException(MutationGuard.StopDetailOf(ex) ?? ex.Message);
        }
        catch (Exception ex) when (ex is ApiNotFoundException or ApiUnreachableException)
        {
            throw new McpException(ex.Message);
        }
    }

    private static JsonArray BuildLaneItems(JsonElement lanes)
    {
        if (lanes.ValueKind == JsonValueKind.String)
        {
            try
            {
                if (JsonNode.Parse(lanes.GetString()!) is JsonArray encoded)
                {
                    lanes = JsonSerializer.SerializeToElement(encoded);
                }
            }
            catch (JsonException)
            {
                throw new McpException(RosterShapeError);
            }
        }

        if (lanes.ValueKind != JsonValueKind.Array)
        {
            throw new McpException(RosterShapeError);
        }

        var items = new JsonArray();
        var index = 0;

        foreach (var item in lanes.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new McpException($"lanes[{index}] must be an object: name, contour, serialOrder, priority.");
            }

            var name = item.TryGetProperty("name", out var nameElement) &&
                       nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new McpException($"lanes[{index}]: name is required.");
            }

            var entry = new JsonObject { ["name"] = name.Trim() };
            if (item.TryGetProperty("contour", out var contour) && contour.ValueKind == JsonValueKind.String)
            {
                entry["contour"] = contour.GetString();
            }

            if (item.TryGetProperty("serialOrder", out var serialOrder) &&
                serialOrder.ValueKind == JsonValueKind.Number)
            {
                entry["serialOrder"] = serialOrder.GetInt32();
            }

            if (item.TryGetProperty("priority", out var priority) &&
                priority.ValueKind == JsonValueKind.Number)
            {
                entry["priority"] = priority.GetInt32();
            }

            items.Add(entry);
            index++;
        }

        return items;
    }
}
