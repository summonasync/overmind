using Overmind.Core;

namespace Overmind.Host;

public sealed class NullSemanticSearch : ISemanticSearch
{
    public Task<bool> HealthAsync(CancellationToken ct) => Task.FromResult(false);

    public Task<IReadOnlyList<EntrySearchHit>> SearchAsync(Guid? projectId, string query, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EntrySearchHit>>([]);
}
