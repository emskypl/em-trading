using EmTrading.Domain.Backtesting;
using EmTrading.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace EmTrading.Infrastructure.Persistence;

internal sealed class TradingDbContext(DbContextOptions<TradingDbContext> options) : DbContext(options)
{
    public DbSet<BacktestRun> BacktestRuns => Set<BacktestRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradingDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite cannot sort or compare DateTimeOffset; all timestamps are UTC, so store them as UTC DateTime.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
    }
}
