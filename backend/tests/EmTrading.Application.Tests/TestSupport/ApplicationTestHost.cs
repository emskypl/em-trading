using EmTrading.Application.Abstractions.Jobs;
using EmTrading.Application.Abstractions.Messaging;
using EmTrading.Application.Abstractions.Persistence;
using EmTrading.Application.Backtesting;
using EmTrading.Application.Backtesting.Strategies;
using EmTrading.Application.MarketData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace EmTrading.Application.Tests.TestSupport;

/// <summary>
/// The real Application registration (assembly scanning, behaviors, validators, domain events) with fake ports.
/// <c>ValidateOnBuild</c> fails if any handler needs a service nobody registered.
/// </summary>
internal sealed class ApplicationTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public ApplicationTestHost()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        services.AddHybridCache();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddSingleton<IBacktestEngine>(Engine);
        services.AddSingleton<IBacktestResultReader>(Reader);
        services.AddSingleton<IMarketDataDownloader>(Downloader);
        services.AddSingleton<IJobQueue>(Jobs);
        services.AddSingleton<IStrategyCatalog>(Strategies);

        services.AddSingleton(Database);
        services.AddSingleton<IBacktestRunRepository>(Database);
        services.AddSingleton<IBacktestHistoryReader>(Database);
        services.AddSingleton<IMarketDataSetRepository>(Database);
        services.AddSingleton<IMarketDataCoverageReader>(Database);
        services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public FakeTimeProvider Clock { get; } = new(TestData.Now);
    public FakeBacktestEngine Engine { get; } = new();
    public CountingResultReader Reader { get; } = new();
    public FakeMarketDataDownloader Downloader { get; } = new();
    public InlineJobQueue Jobs { get; } = new();
    public FakeStrategyCatalog Strategies { get; } = new();
    public InMemoryDatabase Database { get; } = new();

    public IDispatcher Dispatcher => _provider.GetRequiredService<IDispatcher>();

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
