using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace SwingAdviser.Infrastructure.Persistence;

public static class SwingAdviserSqliteOptionsExtensions
{
    public static DbContextOptionsBuilder<SwingAdviserDbContext> UseSwingAdviserSqlite(
        this DbContextOptionsBuilder<SwingAdviserDbContext> optionsBuilder,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return optionsBuilder.UseSqlite(
            connectionString,
            sqliteOptions => sqliteOptions.MigrationsHistoryTable(
                SwingAdviserSqliteDatabase.MigrationsHistoryTableName));
    }

    public static DbContextOptionsBuilder<SwingAdviserDbContext> UseSwingAdviserSqlite(
        this DbContextOptionsBuilder<SwingAdviserDbContext> optionsBuilder,
        DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(connection);

        return optionsBuilder.UseSqlite(
            connection,
            sqliteOptions => sqliteOptions.MigrationsHistoryTable(
                SwingAdviserSqliteDatabase.MigrationsHistoryTableName));
    }
}
