using EmTrading.Application.Abstractions.Messaging;
using EmTrading.Application.Abstractions.Persistence;
using EmTrading.Application.Abstractions.Results;
using EmTrading.Application.Backtesting;
using EmTrading.Application.Backtesting.FailInterruptedBacktests;
using EmTrading.Application.Backtesting.GetBacktestHistory;
using EmTrading.Application.Backtesting.GetBacktestReport;
using EmTrading.Application.Backtesting.GetLatestBacktestReport;
using EmTrading.Application.MarketData;
using EmTrading.Application.Tests.TestSupport;
using EmTrading.Domain.Backtesting;
using EmTrading.Domain.Common;
using EmTrading.Domain.MarketData;
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
    private readonly InMemoryBacktestRunStore _store = new();

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

    [Fact]
    public async Task Runs_appear_in_history_newest_first()
    {
        await using var provider = CreateProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();
        var clock = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();
        var ct = TestContext.Current.CancellationToken;

        await dispatcher.SendAsync(RunBacktestCommandHandlerTests.Command(), ct);
        clock.Advance(TimeSpan.FromMinutes(1));
        _engine.Behavior = (_, _, _) => throw new BacktestEngineException("no data");
        await dispatcher.SendAsync(RunBacktestCommandHandlerTests.Command(), ct);

        var history = await dispatcher.SendAsync(new GetBacktestHistoryQuery("AAPL"), ct);

        Assert.Collection(history.Value,
            newest => Assert.Equal((BacktestStatus.Failed, "no data"), (newest.Status, newest.FailureReason)),
            oldest => Assert.Equal((BacktestStatus.Completed, true), (oldest.Status, oldest.HasReport)));
    }

    [Fact]
    public async Task Interrupted_runs_are_failed_and_saved()
    {
        await using var provider = CreateProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();
        var ct = TestContext.Current.CancellationToken;

        // A run that was still running when the previous session ended.
        var symbol = Symbol.Create("MSFT");
        var strategy = StrategyName.Create("SmaCrossStrategy");
        var stale = BacktestRun.Create(BacktestRunId.New(symbol, strategy, TestData.Now), symbol, strategy,
            DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1)), Money.Usd(1000), StrategyParameters.Empty, TestData.Now);
        stale.Start(TestData.Now);
        _store.Add(stale);

        var result = await dispatcher.SendAsync(new FailInterruptedBacktestsCommand(), ct);

        Assert.Equal(1, result.Value);
        Assert.Equal(BacktestStatus.Failed, stale.Status);
        Assert.Equal(FailInterruptedBacktestsCommandHandler.Reason, stale.FailureReason);
        Assert.Empty(stale.DomainEvents); // saved and published by UnitOfWorkBehavior
    }

    [Fact]
    public async Task Failed_command_is_not_saved_by_the_unit_of_work_behavior()
    {
        await using var provider = CreateProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();
        _reader.Latest = null;

        // A query never saves; a validation failure never reaches the handler or the unit of work.
        await dispatcher.SendAsync(new GetLatestBacktestReportQuery("AAPL", "SmaCrossStrategy"), TestContext.Current.CancellationToken);
        await dispatcher.SendAsync(RunBacktestCommandHandlerTests.Command(fast: 200, slow: 100), TestContext.Current.CancellationToken);

        Assert.Empty(_store.Runs);
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
        services.AddSingleton(_store);
        services.AddSingleton<IBacktestRunRepository>(_store);
        services.AddSingleton<IBacktestHistoryReader>(_store);
        services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
