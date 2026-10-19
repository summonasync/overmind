namespace Overmind.Infrastructure.Grimoire;

public interface IGrimoireHealthClient
{
    Task<bool> IsHealthyAsync(int port, CancellationToken ct);
}
