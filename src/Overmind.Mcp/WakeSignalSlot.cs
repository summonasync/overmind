namespace Overmind.Mcp;

public sealed class WakeSignalSlot
{
    private int _pending;

    public void Add(int count) => Interlocked.Add(ref _pending, count);

    public int Consume() => Interlocked.Exchange(ref _pending, 0);
}
