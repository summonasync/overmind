using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace Overmind.Mcp;

// Escalating wake poll: the watcher idles at the idle period and drops to the gate period while
// the wake endpoint reports at least one signal. A signal lands in the WakeSignalSlot for the next
// tool response and goes out as a logging notification. An unregistered session or a 400/401/404
// is a silent backoff — never a warning loop; only unexpected failures are logged, once per idle pass.
public sealed class AgentWakeWatcher(
    OvermindApiClient api,
    WakeSignalSlot slot,
    IWakeNotifier notifier,
    IWakeTicker ticker) : BackgroundService
{
    public static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(5);

    private const string WakePath = "/api/agent/wake";

    private readonly TimeSpan _idle = WakePeriods.ResolveIdle();
    private readonly TimeSpan _gate = WakePeriods.ResolveGate();
    private bool _inGate;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await ticker.WaitAsync(_inGate ? _gate : _idle, stoppingToken))
            {
                await PollAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await ticker.DisposeAsync();
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        if (AdapterSession.Id is not { } id || id == Guid.Empty)
        {
            return;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(PollTimeout);
            var payload = await api.GetAsync(WakePath, timeout.Token);
            var count = CountSignals(payload);
            _inGate = count > 0;
            if (count > 0)
            {
                slot.Add(count);
                await notifier.NotifyAsync(count, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is ApiStatusException or ApiNotFoundException)
        {
            _inGate = false;
        }
        catch (Exception ex)
        {
            _inGate = false;
            Console.Error.WriteLine($"[overmind-mcp] WARN: wake poll failed: {ex.Message}");
        }
    }

    private static int CountSignals(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("signals", out var signals)
            && signals.ValueKind == JsonValueKind.Array
                ? signals.GetArrayLength()
                : 0;
}
