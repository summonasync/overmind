namespace Overmind.Mcp;

public interface IWakeTicker : IAsyncDisposable
{
    ValueTask<bool> WaitAsync(TimeSpan period, CancellationToken cancellationToken);
}
