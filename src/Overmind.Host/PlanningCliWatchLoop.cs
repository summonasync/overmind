using System.Globalization;

namespace Overmind.Host;

internal sealed class PlanningCliWatchLoop(
    Func<string?, CancellationToken, Task<PlanningCliWatchPage>> poll,
    Func<TimeSpan, CancellationToken, Task> delay,
    Func<DateTimeOffset> clock,
    TextWriter output)
{
    public const int InterruptedExitCode = 130;

    public async Task<int> RunAsync(PlanningCliWatchOptions options, CancellationToken ct)
    {
        var start = clock();
        var since = start.ToString("O", CultureInfo.InvariantCulture);
        try
        {
            while (true)
            {
                var page = await poll(since, ct);
                var winners = page.Signals
                    .Where(signal => signal.CreatedAt >= start)
                    .Where(signal => options.KindFilter is null || signal.Kind == options.KindFilter)
                    .ToArray();
                if (winners.Length > 0)
                {
                    foreach (var signal in winners)
                    {
                        await output.WriteAsync(PlanningCliWatchText.Signal(signal));
                    }

                    return 0;
                }

                since = page.Cursor;
                if (clock() >= start + options.Timeout)
                {
                    await output.WriteAsync(PlanningCliWatchText.NoSignals(options.Timeout));
                    return 0;
                }

                await delay(options.Interval, ct);
            }
        }
        catch (OperationCanceledException)
        {
            return InterruptedExitCode;
        }
    }
}
