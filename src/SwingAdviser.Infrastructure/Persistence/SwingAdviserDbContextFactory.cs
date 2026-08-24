using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SwingAdviser.Infrastructure.Persistence;

public sealed class SwingAdviserDbContextFactory : IDesignTimeDbContextFactory<SwingAdviserDbContext>
{
    public SwingAdviserDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SwingAdviserDbContext>()
            .UseSwingAdviserSqlite(
                SwingAdviserSqliteDatabase.CreateConnectionString("swing-adviser.design.db"))
            .Options;

        return new SwingAdviserDbContext(options);
    }
}
