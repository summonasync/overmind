using Microsoft.AspNetCore.Http;

namespace Overmind.Host.Kestrel.Tests;

public sealed class KestrelResponseDelayGate
{
    private readonly object _lock = new();
    private readonly List<ArmedResponseDelay> _armed = [];
    private readonly List<string> _consumedPaths = [];
    private int _holding;
    private int _consumed;

    public bool IsHolding => HoldingCount > 0;

    public int HoldingCount
    {
        get
        {
            lock (_lock)
            {
                return _holding;
            }
        }
    }

    public int ConsumedCount
    {
        get
        {
            lock (_lock)
            {
                return _consumed;
            }
        }
    }

    public IReadOnlyList<string> ConsumedPaths
    {
        get
        {
            lock (_lock)
            {
                return [.. _consumedPaths];
            }
        }
    }

    public void Arm(string pathPrefix, int delayMs, bool delayAfterHandler = false)
    {
        lock (_lock)
        {
            _armed.Add(new ArmedResponseDelay(pathPrefix, delayMs, delayAfterHandler));
        }
    }

    public void DisarmAll()
    {
        lock (_lock)
        {
            _armed.Clear();
        }
    }

    public bool TryTakeMatching(string? path, out ArmedResponseDelay slot)
    {
        if (path is not null)
        {
            lock (_lock)
            {
                var index = _armed.FindIndex(candidate => path.StartsWith(candidate.PathPrefix, StringComparison.Ordinal));
                if (index >= 0)
                {
                    slot = _armed[index];
                    _armed.RemoveAt(index);
                    Interlocked.Increment(ref _holding);
                    Interlocked.Increment(ref _consumed);
                    _consumedPaths.Add($"{DateTime.UtcNow:HH:mm:ss.fff} {path} delay={slot.DelayMs}ms afterHandler={slot.DelayAfterHandler}");
                    return true;
                }
            }
        }

        slot = null!;
        return false;
    }

    public async Task HoldAsync(HttpContext context, ArmedResponseDelay slot)
    {
        try
        {
            await Task.Delay(slot.DelayMs, context.RequestAborted);
        }
        finally
        {
            Interlocked.Decrement(ref _holding);
        }
    }

    public sealed record ArmedResponseDelay(string PathPrefix, int DelayMs, bool DelayAfterHandler);
}
