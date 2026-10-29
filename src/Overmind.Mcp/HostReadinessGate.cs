using System.Diagnostics;

namespace Overmind.Mcp;

public sealed class HostReadinessGate(HttpClient http, TextWriter log)
{
    public static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(500);

    public async Task<bool> WaitUntilReadyAsync(TimeSpan budget, TimeSpan interval, CancellationToken ct)
    {
        var started = Stopwatch.StartNew();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await OvermindApiClient.CheckHealthAsync(http, ct);
                return true;
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or OperationCanceledException && !ct.IsCancellationRequested)
            {
                if (started.Elapsed >= budget)
                {
                    return false;
                }

                log.WriteLine($"[overmind-mcp] overmind API not ready yet (attempt {attempt}): {ex.Message}");
                await Task.Delay(interval, ct);
            }
        }
    }
}
