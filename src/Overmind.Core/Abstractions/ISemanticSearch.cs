namespace Overmind.Core;

public interface ISemanticSearch
{
    Task<bool> HealthAsync(CancellationToken ct);

    Task<IReadOnlyList<EntrySearchHit>> SearchAsync(Guid? projectId, string query, int limit, CancellationToken ct);
}
