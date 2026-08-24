using Microsoft.Data.Sqlite;

namespace SwingAdviser.Infrastructure.Persistence;

public static class SwingAdviserSqliteDatabase
{
    public const string MigrationsHistoryTableName = "__ef_migrations_history";
    public const string RuntimeDatabaseFileName = "swing-adviser.db";
    public const int BusyTimeoutSeconds = 5;

    public static string RuntimeDatabasePath =>
        Path.Combine(AppContext.BaseDirectory, RuntimeDatabaseFileName);

    public static string CreateConnectionString(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            DefaultTimeout = BusyTimeoutSeconds,
        }.ToString();
    }

    public static void InitializeRuntimeDatabase() => InitializeDatabase(RuntimeDatabasePath);

    public static void InitializeDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);

        try
        {
            using var connection = new SqliteConnection(CreateConnectionString(fullPath));
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutSeconds * 1000}; PRAGMA journal_mode = WAL;";
            command.ExecuteNonQuery();
        }
        catch (Exception exception) when (
            exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"SQLite database at '{fullPath}' could not be opened for writing. " +
                "No fallback database was selected.",
                exception);
        }
    }
}
