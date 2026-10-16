using EmTrading.Application.Abstractions.Jobs;
using EmTrading.Application.Abstractions.Persistence;
using EmTrading.Application.Backtesting;
using EmTrading.Application.MarketData;
using EmTrading.Infrastructure.Configuration;
using EmTrading.Infrastructure.Engine;
using EmTrading.Infrastructure.Engine.Results;
using EmTrading.Infrastructure.Jobs;
using EmTrading.Infrastructure.MarketData;
using EmTrading.Infrastructure.MarketData.Alpaca;
using EmTrading.Infrastructure.MarketData.Lean;
using EmTrading.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace EmTrading.Infrastructure;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IPostConfigureOptions<StorageOptions>, StorageOptionsPostConfigure>();

        // Credentials are optional at startup so backtests still work without them; the client checks them on use.
        services.AddOptions<AlpacaOptions>()
            .Bind(configuration.GetSection(AlpacaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<LeanOptions>()
            .Bind(configuration.GetSection(LeanOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.AddHybridCache();

        AddMarketData(services);
        AddEngine(services);
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

        // Runs after LeanDataFolderInitializer (registration order) and before the main window is shown.
        services.AddHostedService<DatabaseInitializer>();
    }

    private static void AddMarketData(IServiceCollection services)
    {
        // Pooled connections are recycled so long-lived typed clients still pick up DNS changes.
        static HttpMessageHandler CreateHandler() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };

        services.AddSingleton<AlpacaRateLimiter>();
        services.AddTransient<AlpacaRateLimitingHandler>();

        // Outer to inner: resilience (timeouts, retries with backoff honoring Retry-After, circuit breaker),
        // then the rate limiter, so every attempt including retries waits for a token.
        var alpaca = services.AddHttpClient<AlpacaMarketDataClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<AlpacaOptions>>().Value;
                client.BaseAddress = new Uri(options.DataBaseUrl.TrimEnd('/') + "/");
            })
            .ConfigurePrimaryHttpMessageHandler(CreateHandler);
        alpaca.AddStandardResilienceHandler(ConfigureResilience);
        alpaca.AddHttpMessageHandler<AlpacaRateLimitingHandler>();

        services.AddHttpClient<LeanReferenceDataClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<LeanOptions>>().Value;
                client.BaseAddress = new Uri(options.ReferenceDataBaseUrl.TrimEnd('/') + "/");
            })
            .ConfigurePrimaryHttpMessageHandler(CreateHandler)
            .AddStandardResilienceHandler(ConfigureResilience);

        services.AddSingleton<LeanEquityDataWriter>();
        services.AddTransient<IMarketDataDownloader, MarketDataDownloader>();
        services.AddHostedService<LeanDataFolderInitializer>();
    }

    private static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.Retry.MaxRetryAttempts = 4;
        // An attempt includes waiting for a rate-limit token, so allow for some queueing.
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(1);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
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

    private static void AddEngine(IServiceCollection services)
    {
        services.AddSingleton<IBacktestEngine, LeanBacktestEngine>();
        services.AddSingleton<IBacktestResultReader, LeanResultReader>();
    }
}
