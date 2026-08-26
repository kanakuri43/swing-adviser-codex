using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SwingAdviser.Infrastructure.Persistence;
using SwingAdviser.Infrastructure.TradingWorkspace;

namespace SwingAdviser.Infrastructure.Tests.TradingWorkspace;

public sealed class DevelopmentDataSeederTests
{
    [Fact]
    public async Task Seed_IsReproducibleIdempotentAndReadableThroughProductionRepository()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SwingAdviserDbContext>()
            .UseSwingAdviserSqlite(connection)
            .Options;
        await using (var context = new SwingAdviserDbContext(options))
        {
            await context.Database.MigrateAsync();
        }

        await DevelopmentDataSeeder.SeedAsync(options);
        await DevelopmentDataSeeder.SeedAsync(options);

        var snapshot = await new SqliteTradingWorkspaceRepository(options).LoadAsync();
        Assert.Equal(2, snapshot.Candidates.Count);
        Assert.Single(snapshot.Positions);
        Assert.Single(snapshot.Executions);
        Assert.Equal("7203", snapshot.Positions[0].Code);
        Assert.True(snapshot.Positions[0].Decision is not null);
        Assert.Single(snapshot.Positions[0].Lots);
    }
}
