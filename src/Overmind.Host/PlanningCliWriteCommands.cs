using System.Text.Json;

namespace Overmind.Host;

internal static class PlanningCliWriteCommands
{
    public static async Task<int> RunAsync(PlanningCliRun run, CancellationToken ct) =>
        run.Args.Command switch
        {
            "claim" => await ClaimAsync(run, ct),
            "unclaim" => await UnclaimAsync(run, ct),
            "artifact upsert" => await UpsertAsync(run, ct),
            "artifact patch" => await PatchAsync(run, ct),
            "tasks batch" => await TasksBatchAsync(run, ct),
            "log append" => await LogAppendAsync(run, ct),
            "log edit" => await LogEditAsync(run, ct),
            "task status" => await TaskStatusAsync(run, ct),
            "feature update" => await FeatureUpdateAsync(run, ct),
            "directive append" => await DirectiveAppendAsync(run, ct),
            "directive ack" => await DirectiveAckAsync(run, ct),
            _ => await run.UnknownAsync(),
        };

    private static async Task<int> ClaimAsync(PlanningCliRun run, CancellationToken ct)
    {
        var slug = run.Args.RequirePositional(0, "claim requires the feature slug.");
        var project = run.Args.ProjectId();
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        await run.Sessions.ResolveAsync(required: true, ct);
        var body = new Dictionary<string, object?>
        {
            ["alias"] = run.Args.Flag("--alias") ?? run.Sessions.Alias,
            ["vendor"] = run.Args.Flag("--vendor") ?? run.Sessions.Vendor,
            ["overrideLaneCheck"] = run.Args.Has("--override-lane-check"),
        };
        var response = await run.Client.PostAsync(
            $"/api/agent/projects/{project}/planning/features/{Uri.EscapeDataString(slug!)}/claim",
            body,
            ct);
        await run.WriteAsync(response, PlanningCliMutationText.Claim, ct);
        return 0;
    }

    private static async Task<int> UnclaimAsync(PlanningCliRun run, CancellationToken ct)
    {
        var slug = run.Args.RequirePositional(0, "unclaim requires the feature slug.");
        var project = run.Args.ProjectId();
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        await run.Sessions.ResolveAsync(required: true, ct);
        var response = await run.Client.PostAsync(
            $"/api/agent/projects/{project}/planning/features/{Uri.EscapeDataString(slug!)}/unclaim",
            body: null,
            ct);
        await run.WriteAsync(response, PlanningCliMutationText.Claim, ct);
        return 0;
    }

    private static async Task<int> UpsertAsync(PlanningCliRun run, CancellationToken ct)
    {
        var slug = run.Args.RequirePositional(0, "artifact upsert requires the feature slug.");
        var project = run.Args.ProjectId();
        var kind = run.Args.Require("--kind", "artifact upsert requires --kind spec|plan|task|result.");
        var body = await BodyTextAsync(run, ct);
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        await run.Sessions.ResolveAsync(required: true, ct);
        var payload = new Dictionary<string, object?>
        {
            ["kind"] = kind,
            ["body"] = body,
        };

        if (int.TryParse(run.Args.Flag("--order"), out var order))
        {
            payload["order"] = order;
        }

        if (run.Args.Flag("--review") is { } review)
        {
            payload["review"] = review;
        }

        if (run.Args.Flag("--brief") is { } brief)
        {
            payload["brief"] = brief;
        }

        if (run.Args.Flag("--log-note") is { } logNote)
        {
            payload["logNote"] = logNote;
        }

        if (run.Args.Flag("--blocked-by") is { } blockedBy)
        {
            payload["blockedBy"] = IntList(blockedBy);
        }

        var response = await run.Client.PostAsync(
            $"/api/agent/projects/{project}/planning/features/{Uri.EscapeDataString(slug!)}/artifacts",
            payload,
            ct);
        await run.WriteAsync(response, PlanningCliMutationText.Mutation, ct);
        return 0;
    }

    private static async Task<int> PatchAsync(PlanningCliRun run, CancellationToken ct)
    {
        var artifact = await run.AddressArtifactAsync(ct);
        if (artifact is null)
        {
            return await run.ReportUsageAsync();
        }

        var payload = PatchPayload(run, out var payloadProblem);
        if (payloadProblem is not null)
        {
            run.Args.Problems.Add(payloadProblem);
        }

        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        await run.Sessions.ResolveAsync(required: true, ct);
        var response = await run.Client.PostAsync($"/api/agent/planning/artifacts/{artifact}/patch", payload, ct);
        await run.WriteAsync(response, PlanningCliMutationText.Patch, ct);
        return 0;
    }

    private static async Task<int> TasksBatchAsync(PlanningCliRun run, CancellationToken ct)
    {
        var slug = run.Args.RequirePositional(0, "tasks batch requires the feature slug.");
        var project = run.Args.ProjectId();
        var file = run.Args.Require("--file", "tasks batch requires --file <tasks.json>.");
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        var batch = await ReadFileAsync(file!, ct);
        if (batch is null)
        {
            run.Args.Problems.Add($"--file: {file} does not exist.");
            return await run.ReportUsageAsync();
        }

        var tasks = ParseTasksBatch(batch);
        if (tasks is null)
        {
            run.Args.Problems.Add("--file must hold a JSON array of task items or an object with a 'tasks' array.");
            return await run.ReportUsageAsync();
        }

        await run.Sessions.ResolveAsync(required: true, ct);
        var response = await run.Client.PostAsync(
            $"/api/agent/projects/{project}/planning/features/{Uri.EscapeDataString(slug!)}/artifacts/tasks",
            new Dictionary<string, object?> { ["tasks"] = tasks },
            ct);
        await run.WriteAsync(response, PlanningCliMutationText.TasksBatch, ct);
        return 0;
    }

    private static async Task<int> LogAppendAsync(PlanningCliRun run, CancellationToken ct)
    {
        var artifact = await run.AddressArtifactAsync(ct);
        var note = run.Args.Require("--note", "log append requires --note.");
        if (artifact is null || run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        await run.Sessions.ResolveAsync(required: true, ct);
        var response = await run.Client.PostAsync(
            $"/api/agent/planning/artifacts/{artifact}/log",
            new Dictionary<string, object?> { ["note"] = note },
            ct);
        await run.WriteAsync(response, PlanningCliMutationText.Mutation, ct);
        return 0;
    }

    private static async Task<int> LogEditAsync(PlanningCliRun run, CancellationToken ct)
    {
        var artifact = await run.AddressArtifactAsync(ct);
        var text = run.Args.Require("--text", "log edit requires --text.");
        if (artifact is null || run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        await run.Sessions.ResolveAsync(required: true, ct);
        var response = await run.Client.PutAsync(
            $"/api/agent/planning/artifacts/{artifact}/log",
            new Dictionary<string, object?> { ["text"] = text },
            ct);
        await run.WriteAsync(response, PlanningCliMutationText.Mutation, ct);
        return 0;
    }

    private static async Task<int> TaskStatusAsync(PlanningCliRun run, CancellationToken ct)
    {
        var artifact = await run.AddressArtifactAsync(ct);
        var status = run.Args.Require(
            "--status", "task status requires --status todo|inProgress|review|done|blocked.");
        var logNote = run.Args.Require("--log-note", "task status requires --log-note.");
        if (artifact is null || run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        await run.Sessions.ResolveAsync(required: true, ct);
        var response = await run.Client.PostAsync(
            $"/api/agent/planning/artifacts/{artifact}/status",
            new Dictionary<string, object?> { ["status"] = status, ["logNote"] = logNote },
            ct);
        await run.WriteAsync(response, PlanningCliMutationText.Mutation, ct);
        return 0;
    }

    private static async Task<int> FeatureUpdateAsync(PlanningCliRun run, CancellationToken ct)
    {
        var slug = run.Args.RequirePositional(0, "feature update requires the feature slug.");
        var project = run.Args.ProjectId();
        var logNote = run.Args.Require("--log-note", "feature update requires --log-note.");
        var body = await BodyTextAsync(run, ct);
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        await run.Sessions.ResolveAsync(required: true, ct);
        var payload = new Dictionary<string, object?>
        {
            ["logNote"] = logNote,
        };

        if (run.Args.Flag("--title") is { } title)
        {
            payload["title"] = title;
        }

        if (body is not null)
        {
            payload["body"] = body;
        }

        if (run.Args.Flag("--priority") is { } priority)
        {
            payload["priority"] = priority;
        }

        if (run.Args.Flag("--lane") is { } lane)
        {
            payload["lane"] = lane;
        }

        if (run.Args.Flag("--code-surfaces") is { } surfaces)
        {
            payload["codeSurfaces"] = StringList(surfaces);
        }

        var response = await run.Client.PostAsync(
            $"/api/agent/projects/{project}/planning/features/{Uri.EscapeDataString(slug!)}/update",
            payload,
            ct);
        await run.WriteAsync(response, PlanningCliMutationText.FeatureUpdate, ct);
        return 0;
    }

    private static async Task<int> DirectiveAppendAsync(PlanningCliRun run, CancellationToken ct)
    {
        var slug = run.Args.RequirePositional(0, "directive append requires the feature slug.");
        var project = run.Args.ProjectId();
        var text = run.Args.Require("--text", "directive append requires --text.");
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        await run.Sessions.ResolveAsync(required: true, ct);
        var response = await run.Client.PostAsync(
            $"/api/agent/projects/{project}/planning/features/{Uri.EscapeDataString(slug!)}/directives",
            new Dictionary<string, object?> { ["text"] = text },
            ct);
        await run.WriteAsync(response, PlanningCliMutationText.Mutation, ct);
        return 0;
    }

    private static async Task<int> DirectiveAckAsync(PlanningCliRun run, CancellationToken ct)
    {
        var slug = run.Args.RequirePositional(0, "directive ack requires the feature slug.");
        var directive = run.Args.RequirePositional(1, "directive ack requires the directive id.");
        var project = run.Args.ProjectId();
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        if (!Guid.TryParse(directive, out var directiveId))
        {
            run.Args.Problems.Add("directive ack expects the directive id as a GUID.");
            return await run.ReportUsageAsync();
        }

        await run.Sessions.ResolveAsync(required: true, ct);
        var response = await run.Client.PostAsync(
            $"/api/agent/projects/{project}/planning/features/{Uri.EscapeDataString(slug!)}/directives/{directiveId}/ack",
            new Dictionary<string, object?>(),
            ct);
        await run.WriteAsync(response, PlanningCliMutationText.DirectiveAck, ct);
        return 0;
    }

    private static Dictionary<string, object?> PatchPayload(PlanningCliRun run, out string? problem)
    {
        problem = null;
        var brief = run.Args.Flag("--brief");
        var section = run.Args.Flag("--section");
        var content = run.Args.Flag("--content");
        var contentFile = run.Args.Flag("--content-file");
        var acIndex = run.Args.Flag("--ac-index");
        var acText = run.Args.Flag("--ac-text");
        var untick = run.Args.Has("--untick");

        var modes = new[]
            {
                section is not null,
                acIndex is not null || acText is not null,
                brief is not null,
            }
            .Count(enabled => enabled);

        if (modes != 1)
        {
            problem =
                "artifact patch needs exactly one mode: --section with --content(-file), --ac-index/--ac-text, or --brief.";
            return [];
        }

        var payload = new Dictionary<string, object?>();
        if (brief is not null)
        {
            payload["op"] = "set_brief";
            payload["brief"] = brief;
            return payload;
        }

        if (acIndex is not null || acText is not null)
        {
            if (acIndex is not null && acText is not null)
            {
                problem = "Flip one acceptance-criteria item by acIndex or by exact acText, not both.";
                return payload;
            }

            payload["op"] = untick ? "untick_ac" : "tick_ac";
            if (acIndex is not null)
            {
                if (!int.TryParse(acIndex, out var index))
                {
                    problem = "--ac-index expects an integer.";
                    return payload;
                }

                payload["acIndex"] = index;
                return payload;
            }

            payload["acText"] = acText;
            return payload;
        }

        var sources = new[] { content is not null, contentFile is not null }.Count(enabled => enabled);
        if (sources != 1)
        {
            problem = "--section needs exactly one content source: --content or --content-file.";
            return payload;
        }

        payload["op"] = "replace_section";
        payload["section"] = section;
        payload["content"] = contentFile is not null ? ReadFileText(contentFile) : content;
        if (payload["content"] is null)
        {
            problem = "--content-file points at a missing file.";
        }

        return payload;
    }

    private static async Task<string?> BodyTextAsync(PlanningCliRun run, CancellationToken ct)
    {
        var body = run.Args.Flag("--body");
        var bodyFile = run.Args.Flag("--body-file");
        if (bodyFile is null)
        {
            return body;
        }

        var text = await ReadFileAsync(bodyFile, ct);
        if (text is null)
        {
            run.Args.Problems.Add($"--body-file: {bodyFile} does not exist.");
        }

        return text ?? body;
    }

    private static async Task<string?> ReadFileAsync(string path, CancellationToken ct) =>
        File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null;

    private static string? ReadFileText(string path) =>
        File.Exists(path) ? File.ReadAllText(path) : null;

    private static JsonElement? ParseTasksBatch(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement.Clone();
            if (root.ValueKind == JsonValueKind.Array
                || (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("tasks", out _)))
            {
                return root;
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int[] IntList(string raw) =>
        raw.Split(',')
            .Select(item => item.Trim())
            .Where(item => int.TryParse(item, out _))
            .Select(int.Parse)
            .ToArray();

    private static string[] StringList(string raw) =>
        raw.Split(',')
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToArray();
}
