namespace Overmind.Host;

internal static class PlanningCliReadCommands
{
    public static async Task<int> RunAsync(PlanningCliRun run, CancellationToken ct) =>
        run.Args.Command switch
        {
            "projects list" => await ProjectsAsync(run, ct),
            "features list" => await FeaturesAsync(run, ct),
            "feature read" => await FeatureAsync(run, ct),
            "artifact read" => await ArtifactAsync(run, ct),
            "queue" => await QueueAsync(run, ct),
            "current" => await CurrentAsync(run, ct),
            "lanes" => await LanesAsync(run, ct),
            "artifact diff" => await DiffAsync(run, ct),
            "materialization-status" => await MaterializationAsync(run, ct),
            "watch" => await PlanningCliWatch.RunAsync(run, ct),
            "report" => await ReportAsync(run, ct),
            _ => await run.UnknownAsync(),
        };

    private static async Task<int> ProjectsAsync(PlanningCliRun run, CancellationToken ct)
    {
        var response = await run.Client.GetAsync("/api/projects/", query: null, ct);
        await run.WriteAsync(response, PlanningCliBoardText.Projects, ct);
        return 0;
    }

    private static async Task<int> FeaturesAsync(PlanningCliRun run, CancellationToken ct)
    {
        var project = run.Args.ProjectId();
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        var response = await run.Client.GetAsync(
            $"/api/agent/projects/{project}/planning/features", query: null, ct);
        await run.WriteAsync(response, PlanningCliBoardText.Board, ct);
        return 0;
    }

    private static async Task<int> FeatureAsync(PlanningCliRun run, CancellationToken ct)
    {
        var slug = run.Args.RequirePositional(0, "feature read requires the feature slug.");
        var project = run.Args.ProjectId();
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        var response = await run.Client.GetAsync(
            $"/api/agent/projects/{project}/planning/features/{Uri.EscapeDataString(slug!)}", query: null, ct);
        await run.WriteAsync(response, PlanningCliDocumentText.Feature, ct);
        return 0;
    }

    private static async Task<int> ArtifactAsync(PlanningCliRun run, CancellationToken ct)
    {
        var artifact = await run.AddressArtifactAsync(ct);
        if (artifact is null)
        {
            return await run.ReportUsageAsync();
        }

        var sections = run.Args.Flag("--sections");
        var query = sections is null
            ? null
            : new Dictionary<string, string?> { ["sections"] = sections };
        var response = await run.Client.GetAsync($"/api/agent/planning/artifacts/{artifact}", query, ct);
        await run.WriteAsync(response, PlanningCliDocumentText.Artifact, ct);
        return 0;
    }

    private static async Task<int> QueueAsync(PlanningCliRun run, CancellationToken ct)
    {
        var project = run.Args.ProjectId();
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        var limit = run.Args.Flag("--limit");
        var query = limit is null
            ? null
            : new Dictionary<string, string?> { ["limit"] = limit };
        var response = await run.Client.GetAsync(
            $"/api/agent/projects/{project}/planning/task-queue", query, ct);
        await run.WriteAsync(response, PlanningCliBoardText.Queue, ct);
        return 0;
    }

    private static async Task<int> CurrentAsync(PlanningCliRun run, CancellationToken ct)
    {
        var slug = run.Args.RequirePositional(0, "current requires the feature slug.");
        var project = run.Args.ProjectId();
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        var response = await run.Client.GetAsync(
            $"/api/agent/projects/{project}/planning/features/{Uri.EscapeDataString(slug!)}/current",
            query: null,
            ct);
        await run.WriteAsync(response, PlanningCliDocumentText.Current, ct);
        return 0;
    }

    private static async Task<int> LanesAsync(PlanningCliRun run, CancellationToken ct)
    {
        var project = run.Args.ProjectId();
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        var response = await run.Client.GetAsync(
            $"/api/agent/projects/{project}/planning/lanes", query: null, ct);
        await run.WriteAsync(response, PlanningCliBoardText.Lanes, ct);
        return 0;
    }

    private static async Task<int> DiffAsync(PlanningCliRun run, CancellationToken ct)
    {
        var artifact = await run.AddressArtifactAsync(ct);
        if (artifact is null)
        {
            return await run.ReportUsageAsync();
        }

        var query = new Dictionary<string, string?>
        {
            ["from"] = run.Args.Flag("--from"),
            ["to"] = run.Args.Flag("--to"),
        };
        var response = await run.Client.GetAsync($"/api/agent/planning/artifacts/{artifact}/diff", query, ct);
        await run.WriteAsync(response, PlanningCliDocumentText.Diff, ct);
        return 0;
    }

    private static async Task<int> MaterializationAsync(PlanningCliRun run, CancellationToken ct)
    {
        var project = run.Args.ProjectId();
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        var response = await run.Client.GetAsync(
            $"/api/agent/projects/{project}/planning/materialization-status", query: null, ct);
        await run.WriteAsync(response, PlanningCliBoardText.Materialization, ct);
        return 0;
    }

    private static async Task<int> ReportAsync(PlanningCliRun run, CancellationToken ct)
    {
        var options = PlanningCliReportOptions.From(run.Args);
        var project = run.Args.ProjectId();
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        var query = new Dictionary<string, string?>
        {
            ["since"] = options.Since,
            ["until"] = options.Until,
            ["tag"] = options.Tag,
        };
        var response = await run.Client.GetAsync($"/api/projects/{project}/entries", query, ct);
        await run.WriteAsync(response, entries => PlanningCliReportText.Render(entries, options.Day), ct);
        return 0;
    }
}
