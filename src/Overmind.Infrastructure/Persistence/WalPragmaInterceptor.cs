using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Overmind.Infrastructure.Persistence;

public sealed class WalPragmaInterceptor : DbConnectionInterceptor
{
    public static readonly WalPragmaInterceptor Instance = new();

    private WalPragmaInterceptor()
    {
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        command.ExecuteNonQuery();
    }
}
