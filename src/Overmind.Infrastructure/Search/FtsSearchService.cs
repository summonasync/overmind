using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

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
            return await SearchEntriesAsync(
                _dbContext.Database.GetDbConnection(), projectId, matchQuery, limit, ct);
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<IReadOnlyList<FulltextHit>> SearchUnionAsync(
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
            var entries = await SearchEntriesAsync(connection, projectId, matchQuery, limit, ct);
            var features = await SearchFeaturesAsync(connection, projectId, matchQuery, limit, ct);
            var artifacts = await SearchArtifactsAsync(connection, projectId, matchQuery, limit, ct);
            return InterleaveBySourceRank(entries, features, artifacts, limit);
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }
    }

    private static IReadOnlyList<FulltextHit> InterleaveBySourceRank(
        IReadOnlyList<EntrySearchHit> entries,
        IReadOnlyList<FeatureFulltextHit> features,
        IReadOnlyList<ArtifactFulltextHit> artifacts,
        int limit)
    {
        var merged = new List<FulltextHit>(Math.Min(limit, entries.Count + features.Count + artifacts.Count));
        for (var rankSlot = 0; merged.Count < limit; rankSlot++)
        {
            if (rankSlot < entries.Count)
            {
                var entry = entries[rankSlot];
                merged.Add(new EntryFulltextHit(
                    entry.EntryId, entry.Title, entry.Type, entry.Snippet, entry.Rank));
            }

            if (merged.Count < limit && rankSlot < features.Count)
            {
                merged.Add(features[rankSlot]);
            }

            if (merged.Count < limit && rankSlot < artifacts.Count)
            {
                merged.Add(artifacts[rankSlot]);
            }

            if (rankSlot >= entries.Count && rankSlot >= features.Count && rankSlot >= artifacts.Count)
            {
                break;
            }
        }

        return merged;
    }

    private static async Task<List<EntrySearchHit>> SearchEntriesAsync(
        DbConnection connection, Guid? projectId, string matchQuery, int limit, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT e."Id", e."Title", e."Type", e."Content",
                   bm25(entry_fts)
            FROM entry_fts
            JOIN "ContextEntries" e ON e.rowid = entry_fts.rowid
            WHERE entry_fts MATCH $query
              AND ($projectId IS NULL OR e."ProjectId" = $projectId)
            ORDER BY bm25(entry_fts)
            LIMIT $limit
            """;
        AddMatchParameters(command, matchQuery, projectId, limit);

        var hits = new List<EntrySearchHit>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            hits.Add(new EntrySearchHit(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                (EntryType)reader.GetInt32(2),
                WindowSnippet(reader.GetString(3), matchQuery),
                reader.GetDouble(4)));
        }

        return hits;
    }

    private static async Task<List<FeatureFulltextHit>> SearchFeaturesAsync(
        DbConnection connection, Guid? projectId, string matchQuery, int limit, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT f."Slug", f."Title", f."Body",
                   bm25(feature_fts)
            FROM feature_fts
            JOIN "PlanningFeatures" f ON f.rowid = feature_fts.rowid
            WHERE feature_fts MATCH $query
              AND ($projectId IS NULL OR f."ProjectId" = $projectId)
            ORDER BY bm25(feature_fts)
            LIMIT $limit
            """;
        AddMatchParameters(command, matchQuery, projectId, limit);

        var hits = new List<FeatureFulltextHit>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            hits.Add(new FeatureFulltextHit(
                reader.GetString(0),
                reader.GetString(1),
                WindowSnippet(reader.GetString(2), matchQuery),
                reader.GetDouble(3)));
        }

        return hits;
    }

    private static async Task<List<ArtifactFulltextHit>> SearchArtifactsAsync(
        DbConnection connection, Guid? projectId, string matchQuery, int limit, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT a."Id", f."Slug", a."Kind", a."Order", a."Body",
                   bm25(artifact_fts)
            FROM artifact_fts
            JOIN "PlanningArtifacts" a ON a.rowid = artifact_fts.rowid
            JOIN "PlanningFeatures" f ON f."Id" = a."FeatureId"
            WHERE artifact_fts MATCH $query
              AND ($projectId IS NULL OR f."ProjectId" = $projectId)
            ORDER BY bm25(artifact_fts)
            LIMIT $limit
            """;
        AddMatchParameters(command, matchQuery, projectId, limit);

        var hits = new List<ArtifactFulltextHit>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var slug = reader.GetString(1);
            var kind = (PlanningArtifactKind)reader.GetInt32(2);
            hits.Add(new ArtifactFulltextHit(
                Guid.Parse(reader.GetString(0)),
                slug,
                kind,
                reader.GetInt32(3),
                $"{BacklogNaming.SplitSlug(slug).Feature} {kind.ToString().ToLowerInvariant()}",
                WindowSnippet(reader.GetString(4), matchQuery),
                reader.GetDouble(5)));
        }

        return hits;
    }

    private static string WindowSnippet(string body, string matchQuery, int window = 160)
    {
        var terms = matchQuery.Split('"', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var anchor = -1;
        var anchorTerm = string.Empty;
        foreach (var term in terms)
        {
            var index = body.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && (anchor < 0 || index < anchor))
            {
                anchor = index;
                anchorTerm = term;
            }
        }

        if (anchor < 0)
        {
            return body.Length <= window ? body : body[..window] + "…";
        }

        var start = Math.Max(0, anchor - 60);
        var end = Math.Min(body.Length, anchor + anchorTerm.Length + 100);
        return (start > 0 ? "…" : string.Empty)
            + body[start..anchor]
            + "<mark>"
            + body.Substring(anchor, anchorTerm.Length)
            + "</mark>"
            + body[(anchor + anchorTerm.Length)..end]
            + (end < body.Length ? "…" : string.Empty);
    }

    private static void AddMatchParameters(DbCommand command, string matchQuery, Guid? projectId, int limit)
    {
        command.Parameters.Add(new SqliteParameter("$query", matchQuery));
        command.Parameters.Add(new SqliteParameter(
            "$projectId", projectId.HasValue ? projectId.Value.ToString().ToUpperInvariant() : DBNull.Value));
        command.Parameters.Add(new SqliteParameter("$limit", limit));
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
