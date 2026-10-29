namespace Overmind.Mcp;

public sealed class PeriodicWakeTicker : IWakeTicker
{
    private PeriodicTimer? _timer;
    private TimeSpan _period;

    public async ValueTask<bool> WaitAsync(TimeSpan period, CancellationToken cancellationToken)
    {
        if (_timer is null || _period != period)
        {
            _timer?.Dispose();
            _timer = new PeriodicTimer(period);
            _period = period;
        }

        return await _timer.WaitForNextTickAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _timer?.Dispose();
        _timer = null;
        return ValueTask.CompletedTask;
    }
}
