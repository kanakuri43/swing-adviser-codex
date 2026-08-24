using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SwingAdviser.Infrastructure.Persistence;

namespace SwingAdviser.Infrastructure.Tests.Persistence;

public sealed class SwingAdviserDbContextTests
{
    [Fact]
    public async Task InitialMigration_CanBeAppliedToEmptyDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SwingAdviserDbContext>()
            .UseSwingAdviserSqlite(connection)
            .Options;

        await using var dbContext = new SwingAdviserDbContext(options);
        await dbContext.Database.MigrateAsync();

        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray();

        var migration = Assert.Single(appliedMigrations);
        Assert.EndsWith("_InitialCreate", migration, StringComparison.Ordinal);

        var tableNames = await ReadTableNamesAsync(connection);
        Assert.Contains(SwingAdviserSqliteDatabase.MigrationsHistoryTableName, tableNames);
    }

    [Fact]
    public async Task ConnectionStringConfiguration_UsesTheSameMigrationHistoryTable()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"swing-adviser-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<SwingAdviserDbContext>()
                .UseSwingAdviserSqlite(SwingAdviserSqliteDatabase.CreateConnectionString(databasePath))
                .Options;

            await using var dbContext = new SwingAdviserDbContext(options);
            await dbContext.Database.MigrateAsync();
            await dbContext.Database.MigrateAsync();

            await using var connection = new SqliteConnection(
                SwingAdviserSqliteDatabase.CreateConnectionString(databasePath));
            await connection.OpenAsync();

            var tableNames = await ReadTableNamesAsync(connection);
            Assert.Contains(SwingAdviserSqliteDatabase.MigrationsHistoryTableName, tableNames);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public void RuntimeDatabasePath_IsBasedOnTheExecutableDirectory()
    {
        Assert.Equal(
            Path.Combine(AppContext.BaseDirectory, SwingAdviserSqliteDatabase.RuntimeDatabaseFileName),
            SwingAdviserSqliteDatabase.RuntimeDatabasePath);
    }

    [Fact]
    public async Task InitializeDatabase_EnablesWalAndBusyTimeout()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"swing-adviser-{Guid.NewGuid():N}.db");

        try
        {
            SwingAdviserSqliteDatabase.InitializeDatabase(databasePath);

            await using var connection = new SqliteConnection(
                SwingAdviserSqliteDatabase.CreateConnectionString(databasePath));
            await connection.OpenAsync();

            await using var journalModeCommand = connection.CreateCommand();
            journalModeCommand.CommandText = "PRAGMA journal_mode;";
            var journalMode = Assert.IsType<string>(await journalModeCommand.ExecuteScalarAsync());

            await using var busyTimeoutCommand = connection.CreateCommand();
            busyTimeoutCommand.CommandText = "PRAGMA busy_timeout;";
            var busyTimeout = Assert.IsType<long>(await busyTimeoutCommand.ExecuteScalarAsync());

            Assert.Equal("wal", journalMode, ignoreCase: true);
            Assert.Equal(SwingAdviserSqliteDatabase.BusyTimeoutSeconds * 1000, busyTimeout);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
            File.Delete($"{databasePath}-shm");
            File.Delete($"{databasePath}-wal");
        }
    }

    [Fact]
    public void InitializeDatabase_WhenParentDoesNotExist_ReportsPathAndDoesNotFallback()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"swing-adviser-missing-{Guid.NewGuid():N}",
            "swing-adviser.db");

        var exception = Assert.Throws<InvalidOperationException>(
            () => SwingAdviserSqliteDatabase.InitializeDatabase(databasePath));

        Assert.Contains(Path.GetFullPath(databasePath), exception.Message, StringComparison.Ordinal);
        Assert.Contains("No fallback database", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(databasePath));
    }

    private static async Task<string[]> ReadTableNamesAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_schema WHERE type = 'table' ORDER BY name;";

        var tableNames = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }

        return tableNames.ToArray();
    }
}
