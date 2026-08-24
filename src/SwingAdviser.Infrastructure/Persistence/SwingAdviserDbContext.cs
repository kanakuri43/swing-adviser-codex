using Microsoft.EntityFrameworkCore;

namespace SwingAdviser.Infrastructure.Persistence;

public sealed class SwingAdviserDbContext(DbContextOptions<SwingAdviserDbContext> options)
    : DbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<Guid>()
            .HaveConversion<CanonicalGuidConverter>()
            .HaveColumnType("TEXT");
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<UtcInstantConverter>()
            .HaveColumnType("TEXT");
        configurationBuilder.Properties<DateOnly>()
            .HaveConversion<MarketDateConverter>()
            .HaveColumnType("TEXT");
        configurationBuilder.Properties<decimal>()
            .HaveConversion<CanonicalDecimalConverter>()
            .HaveColumnType("TEXT");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SwingAdviserDbContext).Assembly);
        modelBuilder.ApplySwingAdviserStorageConventions();
    }
}
