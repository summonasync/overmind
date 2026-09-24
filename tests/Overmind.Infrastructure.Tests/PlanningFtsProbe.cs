using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Tests;

internal static class PlanningFtsProbe
{
    public static async Task<long> CountAsync(
        OvermindDbContext db, string sql, string? term = null, CancellationToken ct = default)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            if (term is not null)
            {
                command.Parameters.Add(new SqliteParameter("$term", term));
            }

            return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
