using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SwingAdviser.Infrastructure.Persistence;

public sealed class SwingAdviserDbContextFactory : IDesignTimeDbContextFactory<SwingAdviserDbContext>
{
    public SwingAdviserDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SwingAdviserDbContext>()
            .UseSqlite(
                "Data Source=swing-adviser.design.db",
                sqliteOptions => sqliteOptions.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;

        return new SwingAdviserDbContext(options);
    }
}
