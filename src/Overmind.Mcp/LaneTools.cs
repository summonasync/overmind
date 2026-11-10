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
    [Description("Reshapes the project's lane roster as an UPSERT of known lanes — lanes are first-class planning data: named code contours owned by one agent each, serial within the lane. Only the lanes the payload names change (name-keyed case-insensitively); lanes absent from the payload are never touched, so a payload missing a lane deletes nothing. An empty array is an explicit no-op echoing the unchanged roster — it used to clear the whole roster and no longer does. An unknown name fails with 400 LaneBirthRestricted naming planning_update_feature as the only birth path: set_lanes never creates lanes. Each item: name (required, max 64, unique per project, and addressable as a single URL path segment — a lane is edited granularly at its name, so no slash, backslash or control character, and never a bare dot or dot-dot: those are refused with 400 InvalidLaneName before anything is written), contour (free text description of the code contour), serialOrder (optional int, reserved — no behavior yet), priority (optional int 0-3, mirroring feature priorities p0-p3 — lane columns sort priority asc, nulls last; null = no priority), boundAlias (optional, max 32 — binds the lane to one agent alias so the resident re-adopts it after a restart via session_identity_set; vendor is intentionally not bound — it is per-runtime; null or omitted clears the binding, item-level replace like every optional field), liveSurfaces (optional array of {glob, owner} — repo-relative globs watched by a running stand, owner = the alias running it, glob flavor identical to codeSurfaces; entries whose globs match a feature's codeSurfaces are shown in that feature's claim response as an informational warning, never a block; omitted liveSurfaces clears the lane's entries, item-level replace like every optional field). Removal is a separate explicit act: planning_remove_lane. Features join a lane via planning_update_feature's lane field, which auto-creates unknown names. Max 64 lanes per project. Returns the resulting roster with per-lane holder and open-feature counts.")]
    public async Task<CallToolResult> SetLanes(
        [Description("Lanes to reshape: an array of lane objects — name (required), contour, serialOrder, priority (optional 0-3, omit for none), boundAlias (optional, max 32; null or omitted clears it). Only named lanes change; absent lanes survive; an empty array is a no-op. Each item may carry liveSurfaces: an optional array of {glob, owner} objects (omitted clears the lane's entries).")] JsonElement lanes,
        [Description("Id of the project the roster belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
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
                        ? "No lanes in the roster — the payload wrote nothing: an empty payload is a no-op, and lanes are born via planning_update_feature (lane=...)."
                        : $"Lane roster holds {count} lane(s): {string.Join(", ", names)}."
                }
            ]
        };
    }

    [McpServerTool(Name = "planning_create_lane", Destructive = false, Idempotent = true)]
    [Description("Births one lane on the project's lane roster — the create door that planning_set_lanes refuses: set_lanes only reshapes known lanes and fails with LaneBirthRestricted on an unknown name, while this tool and planning_update_feature's lane field are the two paths that actually add lanes. name is required (max 64, unique per project, addressable as a single URL path segment — no slash, backslash or control character, never a bare dot or dot-dot); contour, priority (0-3, mirroring feature priorities p0-p3) and boundAlias (optional, max 32) ride along. Birth is idempotent by echo: a name already on the roster (case-insensitive) is not an error — the stored row comes back unchanged and nothing is written, so a retry after a dropped connection is safe; a concurrent race that slips past the dedupe surfaces as 409 LaneNameDuplicate. The roster caps at 64 lanes per project — over the cap fails with LaneRosterTooLarge. No claim lock is involved: lanes are roster rows, not board artifacts, so creating one never blocks on or touches a feature claim. Creating a lane moves no features; features join it via planning_update_feature's lane field. Returns a one-line echo of the stored lane.")]
    public async Task<CallToolResult> CreateLane(
        [Description("Name of the lane to birth: max 64, no slash, backslash or control character, never a bare dot or dot-dot. Trimmed before the write; the echo carries the stored spelling.")] string name,
        [Description("Free text description of the code contour this lane covers.")] string? contour = null,
        [Description("Optional priority 0-3 mirroring feature priorities p0-p3 (lane columns sort priority asc, nulls last); omit for none.")] int? priority = null,
        [Description("Optional agent alias to bind the lane to, max 32; omit to leave the lane free for adoption via session_identity_set.")] string? boundAlias = null,
        [Description("Id of the project the roster belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
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
    [Description("Removes one lane from the project's lane roster — the only manual removal path: planning_set_lanes never deletes, and a lane whose last open feature leaves or closes retires itself. Refuses with 409 LaneStillOccupied while the lane still holds open features, naming their slugs — close them as done or move them to another lane first. A delete on an unknown name is an idempotent no-op. Removing a lane touches no features: the lane field is a plain name reference, never an FK. Returns the resulting roster with per-lane holder and open-feature counts.")]
    public async Task<CallToolResult> RemoveLane(
        [Description("Name of the lane to remove, as shown by planning_list_lanes.")] string name,
        [Description("Id of the project the roster belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
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
    [Description("Reads the project's lane roster with the live projection per lane: name, contour, serialOrder (reserved, no behavior yet), priority (0-3, null = no priority — the board sorts lanes priority asc, nulls last), boundAlias (the agent alias the lane is bound to, null when free — entering the lane means adopting it via session_identity_set, or binding your own before claiming), liveSurfaces (the {glob, owner} entries of running stands this lane watches, shown verbatim; entries whose glob matches a claimant feature's codeSurfaces surface as informational warnings in planning_claim_feature responses), openFeatureCount (non-done features currently in the lane), holder (alias + vendor + claimState of the alive claim on any feature in the lane), and the features themselves (slug, status, claim identity, claimState). The wave marshal reads this before deciding who moves — a lane is busy when some feature in it holds an alive claim by another agent.")]
    public async Task<JsonElement> ListLanes(
        [Description("Id of the project whose roster to read; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
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
