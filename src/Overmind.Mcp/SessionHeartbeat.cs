namespace Overmind.Mcp;

// Honest process-alive signal: while the adapter runs, a background loop posts a session touch
// through the shared pipeline every period. A failed touch is logged and swallowed — a dead host
// API never takes the adapter down; Dispose is bounded because ApplicationStopping is synchronous.
public sealed class SessionHeartbeat(HttpClient http, TimeSpan period) : IDisposable
{
    private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(2);

    private readonly CancellationTokenSource _stopping = new();
    private Task _loop = Task.CompletedTask;

    public void Start()
    {
        _loop = RunAsync();
    }

    public void Dispose()
    {
        _stopping.Cancel();
        try
        {
            _loop.Wait(StopWait);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[overmind-mcp] WARN: heartbeat stop failed: {ex.Message}");
        }

        _stopping.Dispose();
    }

    private async Task RunAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(period);
            while (await timer.WaitForNextTickAsync(_stopping.Token))
            {
                await TouchAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task TouchAsync()
    {
        if (AdapterSession.Id is not { } id || id == Guid.Empty)
        {
            return;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
            timeout.CancelAfter(SessionTouchHandler.TouchTimeout);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/agent/sessions/{id}/touch");
            using var response = await http.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[overmind-mcp] WARN: heartbeat touch failed: {ex.Message}");
        }
    }
}
