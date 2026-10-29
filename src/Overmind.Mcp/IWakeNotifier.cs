namespace Overmind.Mcp;

public interface IWakeNotifier
{
    Task NotifyAsync(int pending, CancellationToken cancellationToken);
}
