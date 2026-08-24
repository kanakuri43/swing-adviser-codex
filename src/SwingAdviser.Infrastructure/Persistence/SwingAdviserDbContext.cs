using Microsoft.EntityFrameworkCore;

namespace SwingAdviser.Infrastructure.Persistence;

public sealed class SwingAdviserDbContext(DbContextOptions<SwingAdviserDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SwingAdviserDbContext).Assembly);
    }
}
