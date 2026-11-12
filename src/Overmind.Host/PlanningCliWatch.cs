namespace Overmind.Host;

internal static class PlanningCliWatch
{
    public const string WakePath = "/api/agent/wake";

    internal static async Task<int> RunAsync(PlanningCliRun run, CancellationToken ct)
    {
        var options = PlanningCliWatchOptions.From(run.Args);
        if (run.Failed)
        {
            return await run.ReportUsageAsync();
        }

        var sessions = options.Alias is null
            ? run.Sessions
            : new PlanningCliSession(run.Client, new PlanningCliOptions(null, options.Alias, run.Sessions.Vendor));
        await sessions.ResolveAsync(required: true, ct);

        using var interrupts = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, interrupts.Token);
        ConsoleCancelEventHandler interrupt = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            interrupts.Cancel();
        };
        Console.CancelKeyPress += interrupt;
        try
        {
            var loop = new PlanningCliWatchLoop(
                (since, token) => PollAsync(run, since, token),
                Task.Delay,
                () => DateTimeOffset.UtcNow,
                run.Output);
            return await loop.RunAsync(options, linked.Token);
        }
        finally
        {
            Console.CancelKeyPress -= interrupt;
        }
    }

    private static async Task<PlanningCliWatchPage> PollAsync(PlanningCliRun run, string since, CancellationToken ct)
    {
        var response = await run.Client.GetAsync(
            WakePath,
            new Dictionary<string, string?> { ["since"] = since },
            ct);
        return PlanningCliWatchPage.Parse(response.Body);
    }
}
