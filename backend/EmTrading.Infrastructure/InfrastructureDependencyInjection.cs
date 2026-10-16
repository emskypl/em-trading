using EmTrading.Application.Abstractions.Persistence;
using EmTrading.Application.Backtesting;
using EmTrading.Application.MarketData;
using EmTrading.Infrastructure.Configuration;
using EmTrading.Infrastructure.Engine;
using EmTrading.Infrastructure.Engine.Results;
using EmTrading.Infrastructure.MarketData;
using EmTrading.Infrastructure.MarketData.Alpaca;
using EmTrading.Infrastructure.MarketData.Lean;
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

        // Runs after LeanDataFolderInitializer (registration order) and before the main window is shown.
        services.AddHostedService<DatabaseInitializer>();
    }

    private static void AddMarketData(IServiceCollection services)
    {
        // Pooled connections are recycled so long-lived typed clients still pick up DNS changes.
        static HttpMessageHandler CreateHandler() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };

        services.AddHttpClient<AlpacaMarketDataClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<AlpacaOptions>>().Value;
                client.BaseAddress = new Uri(options.DataBaseUrl.TrimEnd('/') + "/");
            })
            .ConfigurePrimaryHttpMessageHandler(CreateHandler);

        services.AddHttpClient<LeanReferenceDataClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<LeanOptions>>().Value;
                client.BaseAddress = new Uri(options.ReferenceDataBaseUrl.TrimEnd('/') + "/");
            })
            .ConfigurePrimaryHttpMessageHandler(CreateHandler);

        services.AddSingleton<LeanEquityDataWriter>();
        services.AddTransient<IMarketDataDownloader, MarketDataDownloader>();
        services.AddHostedService<LeanDataFolderInitializer>();
    }

    private static void AddEngine(IServiceCollection services)
    {
        services.AddSingleton<IBacktestEngine, LeanBacktestEngine>();
        services.AddSingleton<IBacktestResultReader, LeanResultReader>();
    }
}
