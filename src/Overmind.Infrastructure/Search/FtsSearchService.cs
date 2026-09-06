using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Search;

public sealed class FtsSearchService
{
    private readonly OvermindDbContext _dbContext;

    public FtsSearchService(OvermindDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<EntrySearchHit>> SearchAsync(
        Guid? projectId, string query, int limit, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var matchQuery = SanitizeQuery(query);
        if (matchQuery.Length == 0)
        {
            return [];
        }

        await _dbContext.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT e."Id", e."Title", e."Type",
                       snippet(entry_fts, 1, '<mark>', '</mark>', '…', 16),
                       bm25(entry_fts)
                FROM entry_fts
                JOIN "ContextEntries" e ON e.rowid = entry_fts.rowid
                WHERE entry_fts MATCH $query
                  AND ($projectId IS NULL OR e."ProjectId" = $projectId)
                ORDER BY bm25(entry_fts)
                LIMIT $limit
                """;

            command.Parameters.Add(new SqliteParameter("$query", matchQuery));
            command.Parameters.Add(new SqliteParameter(
                "$projectId", projectId.HasValue ? projectId.Value.ToString().ToUpperInvariant() : DBNull.Value));
            command.Parameters.Add(new SqliteParameter("$limit", limit));

            var hits = new List<EntrySearchHit>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var snippet = await reader.IsDBNullAsync(3, ct)
                    ? string.Empty
                    : reader.GetString(3);

                hits.Add(new EntrySearchHit(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    (EntryType)reader.GetInt32(2),
                    snippet,
                    reader.GetDouble(4)));
            }

            return hits;
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }
    }

    private static string SanitizeQuery(string query)
    {
        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term => term.Replace("\"", string.Empty, StringComparison.Ordinal))
            .Where(term => term.Length > 0)
            .Select(term => $"\"{term}\"");

        return string.Join(' ', terms);
    }
}
