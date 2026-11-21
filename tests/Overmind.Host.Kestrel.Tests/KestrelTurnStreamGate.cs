namespace Overmind.Host.Kestrel.Tests;

public sealed class KestrelTurnStreamGate
{
    private volatile TaskCompletionSource? _hold;

    public bool LastObservedCancellation { get; private set; }

    public void ObserveCancellation(bool cancelled) => LastObservedCancellation = cancelled;

    public void Arm() => _hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => Interlocked.Exchange(ref _hold, null)?.TrySetResult();

    public async Task WaitAsync(CancellationToken ct)
    {
        var hold = _hold;
        if (hold is null)
        {
            return;
        }

        await hold.Task.WaitAsync(ct);
    }
}
