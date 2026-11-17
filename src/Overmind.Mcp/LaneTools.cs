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
    private readonly ProjectResolver _projects;

    public LaneTools(OvermindApiClient api, MutationGuard mutations, ProjectResolver projects)
    {
        _api = api;
        _mutations = mutations;
        _projects = projects;
    }

    [McpServerTool(Name = "planning_set_lanes", Destructive = false, Idempotent = false)]
    [Description("Reshapes the lane roster as an UPSERT of KNOWN lanes — only lanes the payload names " +
        "change; absent lanes survive and an empty array is a no-op. Never creates (unknown names fail " +
        "LaneBirthRestricted — use planning_create_lane) and never deletes (planning_remove_lane).")]
    public async Task<CallToolResult> SetLanes(
        [Description("Lane objects keyed by name; empty array is a no-op.")] JsonElement lanes,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var payload = new JsonObject { ["lanes"] = BuildLaneItems(lanes) };
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);

        var roster = await _mutations.SendAsync(() => _api.PutAsync(
            $"/api/agent/projects/{resolved}/planning/lanes-roster",
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
                        ? "No lanes in the roster — the payload wrote nothing: an empty payload is a no-op, and lanes are born via planning_create_lane or planning_update_feature (lane=...)."
                        : $"Lane roster holds {count} lane(s): {string.Join(", ", names)}."
                }
            ]
        };
    }

    [McpServerTool(Name = "planning_create_lane", Destructive = false, Idempotent = true)]
    [Description("Births one lane on the roster — the create door planning_set_lanes refuses; idempotent by " +
        "echo: a known name returns the stored row unchanged. Features join lanes via " +
        "planning_update_feature's lane field, which also births them.")]
    public async Task<CallToolResult> CreateLane(
        [Description("Lane name, max 64, URL-path-segment safe.")] string name,
        [Description("Free text description of the code contour.")] string? contour = null,
        [Description("Optional lane priority 0-3, mirroring feature priorities.")] int? priority = null,
        [Description("Optional bound agent alias, max 32.")] string? boundAlias = null,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new McpException("name is required: the lane to birth, max 64, addressable as a single URL path segment.");
        }

        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);

        var payload = new JsonObject { ["name"] = name.Trim() };
        if (contour is not null)
        {
            payload["contour"] = contour;
        }

        if (priority is not null)
        {
            payload["priority"] = priority.Value;
        }

        if (boundAlias is not null)
        {
            payload["boundAlias"] = boundAlias;
        }

        var lane = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/lanes",
            payload,
            ct), readback: null, ct);

        var stored = lane.GetProperty("name").GetString() ?? name.Trim();
        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = $"Lane '{stored}' is in the roster — born now, or the echo of an existing one (case-insensitive)."
                }
            ]
        };
    }

    [McpServerTool(Name = "planning_remove_lane", Destructive = true, Idempotent = true)]
    [Description("Removes one lane from the roster — the only manual removal path; a lane retires itself " +
        "when its last open feature leaves. Refuses LaneStillOccupied while open features remain; unknown " +
        "names are an idempotent no-op. Touches no features.")]
    public async Task<CallToolResult> RemoveLane(
        [Description("Lane name to remove.")] string name,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new McpException("name is required: the lane to remove, as shown by planning_list_lanes.");
        }

        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var roster = await _mutations.SendAsync(() => _api.DeleteAsync(
            $"/api/agent/projects/{resolved}/planning/lanes/{Uri.EscapeDataString(name.Trim())}",
            ct), readback: null, ct);

        var lanes = roster.GetProperty("lanes");
        var count = lanes.GetArrayLength();
        var names = lanes
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
                        ? $"No lane named '{name.Trim()}' remains — the roster is empty."
                        : $"No lane named '{name.Trim()}' remains; the roster holds {count} lane(s): {string.Join(", ", names)}."
                }
            ]
        };
    }

    [McpServerTool(Name = "planning_list_lanes", ReadOnly = true, Idempotent = true)]
    [Description("Reads the lane roster with the live projection per lane — contour, priority, boundAlias, " +
        "liveSurfaces, open-feature count, the alive claim holder and its features. The wave marshal reads this " +
        "before deciding who moves; a lane is busy when a feature in it holds an alive claim by another agent. " +
        "For the whole wave picture in one call use overmind_wave_snapshot.")]
    public async Task<JsonElement> ListLanes(
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        try
        {
            return await _api.GetAsync($"/api/agent/projects/{resolved}/planning/lanes", ct);
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
                throw new McpException(
                    $"lanes[{index}] must be an object: name, contour, serialOrder, priority, boundAlias.");
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

            if (item.TryGetProperty("boundAlias", out var boundAlias) &&
                boundAlias.ValueKind == JsonValueKind.String)
            {
                entry["boundAlias"] = boundAlias.GetString();
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

            if (item.TryGetProperty("liveSurfaces", out var liveSurfaces) &&
                liveSurfaces.ValueKind == JsonValueKind.Array)
            {
                var surfaces = new JsonArray();
                foreach (var surface in liveSurfaces.EnumerateArray())
                {
                    if (surface.ValueKind != JsonValueKind.Object ||
                        !surface.TryGetProperty("glob", out var glob) ||
                        glob.ValueKind != JsonValueKind.String)
                    {
                        throw new McpException(
                            $"lanes[{index}]: each liveSurfaces entry must be an object with a glob string and an owner string.");
                    }

                    var surfaceEntry = new JsonObject { ["glob"] = glob.GetString() };
                    if (surface.TryGetProperty("owner", out var owner) &&
                        owner.ValueKind == JsonValueKind.String)
                    {
                        surfaceEntry["owner"] = owner.GetString();
                    }

                    surfaces.Add(surfaceEntry);
                }

                entry["liveSurfaces"] = surfaces;
            }

            items.Add(entry);
            index++;
        }

        return items;
    }
}
