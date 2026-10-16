using EmTrading.Application.Abstractions.Messaging;
using EmTrading.Application.Abstractions.Results;
using EmTrading.Application.Backtesting;
using EmTrading.Application.Backtesting.GetBacktestReport;
using EmTrading.Application.Backtesting.GetLatestBacktestReport;
using EmTrading.Application.MarketData;
using EmTrading.Application.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace EmTrading.Application.Tests.Backtesting;

/// <summary>
/// Runs the real Application registration (assembly scanning, behaviors, validators, domain events)
/// with fake infrastructure, to check the pieces work together.
/// </summary>
public class BacktestPipelineIntegrationTests
{
    private readonly FakeBacktestEngine _engine = new();
    private readonly CountingResultReader _reader = new();

    [Fact]
    public async Task Completed_run_invalidates_the_cached_latest_report()
    {
        await using var provider = CreateProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();
        var ct = TestContext.Current.CancellationToken;
        var latest = new GetLatestBacktestReportQuery("AAPL", "SmaCrossStrategy");

        await dispatcher.SendAsync(latest, ct);
        await dispatcher.SendAsync(latest, ct);
        Assert.Equal(1, _reader.LatestCalls);

        var run = await dispatcher.SendAsync(RunBacktestCommandHandlerTests.Command(), ct);
        Assert.True(run.IsSuccess);

        await dispatcher.SendAsync(latest, ct);
        Assert.Equal(2, _reader.LatestCalls);
    }

    [Fact]
    public async Task Run_report_is_cached_by_run_id()
    {
        await using var provider = CreateProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();
        var ct = TestContext.Current.CancellationToken;

        var first = await dispatcher.SendAsync(new GetBacktestReportQuery("aapl", "run-1"), ct);
        var second = await dispatcher.SendAsync(new GetBacktestReportQuery("AAPL", "run-1"), ct);

        Assert.Same(first.Value, second.Value);
        Assert.Equal(1, _reader.ReadCalls);
    }

    [Fact]
    public async Task Invalid_command_never_reaches_the_engine()
    {
        await using var provider = CreateProvider();

        var result = await provider.GetRequiredService<IDispatcher>()
            .SendAsync(RunBacktestCommandHandlerTests.Command(fast: 200, slow: 100), TestContext.Current.CancellationToken);

        Assert.Equal(ErrorType.Validation, result.Error?.Type);
        Assert.Empty(_engine.Requests);
    }

    [Fact]
    public async Task Missing_latest_report_is_a_not_found_error()
    {
        _reader.Latest = null;
        await using var provider = CreateProvider();

        var result = await provider.GetRequiredService<IDispatcher>()
            .SendAsync(new GetLatestBacktestReportQuery("MSFT", "SmaCrossStrategy"), TestContext.Current.CancellationToken);

        Assert.Equal(BacktestErrors.NoResults("MSFT", "SmaCrossStrategy"), result.Error);
    }

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        services.AddHybridCache();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(TestData.Now));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IBacktestEngine>(_engine);
        services.AddSingleton<IBacktestResultReader>(_reader);
        services.AddSingleton<IMarketDataDownloader, UnusedMarketDataDownloader>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
