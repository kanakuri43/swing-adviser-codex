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
            .UseSqlite(
                connection,
                sqliteOptions => sqliteOptions.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;

        await using var dbContext = new SwingAdviserDbContext(options);
        await dbContext.Database.MigrateAsync();

        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray();

        var migration = Assert.Single(appliedMigrations);
        Assert.EndsWith("_InitialCreate", migration, StringComparison.Ordinal);
    }
}
