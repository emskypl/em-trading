using EmTrading.Application.Abstractions.Jobs;
using EmTrading.Application.Abstractions.Persistence;
using EmTrading.Application.Backtesting;
using EmTrading.Application.MarketData;
using EmTrading.Infrastructure.Configuration;
using EmTrading.Infrastructure.Jobs;
using EmTrading.Infrastructure.MarketData;
using EmTrading.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EmTrading.Infrastructure;

public static class InfrastructureDependencyInjection
{
    /// <summary>
    /// Shared infrastructure: storage settings, persistence, background jobs, caching and the market data downloader.
    /// The downloader needs an <see cref="IHistoricalBarSource"/> and an <see cref="IMarketDataStore"/>,
    /// registered by the Alpaca and Lean adapter projects.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IPostConfigureOptions<StorageOptions>, StorageOptionsPostConfigure>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddHybridCache();

        services.AddTransient<IMarketDataDownloader, MarketDataDownloader>();

        AddPersistence(services);
        AddJobs(services, configuration);

        return services;
    }

    private static void AddPersistence(IServiceCollection services)
    {
        services.AddDbContext<TradingDbContext>((sp, options) =>
        {
            var databaseFile = sp.GetRequiredService<IOptions<StorageOptions>>().Value.DatabaseFile;
            Directory.CreateDirectory(Path.GetDirectoryName(databaseFile)!);
            options.UseSqlite(new SqliteConnectionStringBuilder { DataSource = databaseFile }.ToString());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IBacktestRunRepository, BacktestRunRepository>();
        services.AddScoped<IBacktestHistoryReader, BacktestHistoryReader>();
        services.AddScoped<IMarketDataSetRepository, MarketDataSetRepository>();
        services.AddScoped<IMarketDataCoverageReader, MarketDataCoverageReader>();

        services.AddHostedService<DatabaseInitializer>();
    }

    private static void AddJobs(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JobOptions>()
            .Bind(configuration.GetSection(JobOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<JobQueue>();
        services.AddSingleton<IJobQueue>(sp => sp.GetRequiredService<JobQueue>());
        services.AddSingleton<IJobMonitor>(sp => sp.GetRequiredService<JobQueue>());

        // Registered after DatabaseInitializer: jobs may use the database.
        services.AddHostedService<JobProcessor>();
    }
}
