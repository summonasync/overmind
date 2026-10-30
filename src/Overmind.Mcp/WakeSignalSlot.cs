namespace Overmind.Mcp;

public sealed class WakeSignalSlot
{
    private int _pending;

    public void Set(int count) => Interlocked.Exchange(ref _pending, count);

    public int Consume() => Interlocked.Exchange(ref _pending, 0);
}
