namespace Overmind.Infrastructure.Grimoire;

public interface IGrimoireEngineRunner
{
    Task<GrimoireEngineRunResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct);
}
