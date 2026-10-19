namespace Overmind.Infrastructure.Grimoire;

public interface IGrimoireCli
{
    Task<GrimoireVersionProbe> ProbeVersionAsync(string binaryPath, CancellationToken ct);
}
