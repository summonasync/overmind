namespace Overmind.Infrastructure.Grimoire;

public interface IGrimoireMemoryClient
{
    Task<GrimoireMemoryEndpoint> ResolveEndpointAsync(CancellationToken ct);

    Task<GrimoireMemoryWriteResult> RememberAsync(
        GrimoireMemoryEndpoint endpoint, GrimoireMemoryWriteRequest request, CancellationToken ct);

    Task<IReadOnlyList<GrimoireMemoryRecord>> RecallAsync(
        GrimoireMemoryEndpoint endpoint, string query, string? kind, string? tag, int limit, CancellationToken ct);

    Task<IReadOnlyList<GrimoireMemoryRecord>> ListTopFactsAsync(
        GrimoireMemoryEndpoint endpoint, int limit, CancellationToken ct);

    Task<IReadOnlyList<GrimoireCompactionGroup>> GetCompactionCandidatesAsync(
        GrimoireMemoryEndpoint endpoint, CancellationToken ct);

    Task ForgetAsync(Guid memoryId, string reason, GrimoireMemoryEndpoint endpoint, CancellationToken ct);
}
