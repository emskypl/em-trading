using EmTrading.Application.Abstractions.Results;
using EmTrading.Application.Backtesting;
using EmTrading.Application.Backtesting.RunBacktest;
using EmTrading.Application.Tests.TestSupport;
using EmTrading.Domain.Backtesting;
using Microsoft.Extensions.Time.Testing;

namespace EmTrading.Application.Tests.Backtesting;

public class RunBacktestCommandHandlerTests
{
    private readonly FakeBacktestEngine _engine = new();
    private readonly RecordingEventPublisher _events = new();
    private readonly InMemoryBacktestRunStore _runs = new();
    private readonly InMemoryUnitOfWork _unitOfWork;
    private readonly FakeTimeProvider _clock = new(TestData.Now);

    public RunBacktestCommandHandlerTests()
    {
        _unitOfWork = new InMemoryUnitOfWork(_runs, _events);
    }

    [Fact]
    public async Task Run_is_saved_as_running_before_the_engine_starts_and_saved_again_when_done()
    {
        var statusWhileEngineRuns = new List<BacktestStatus>();
        _engine.Behavior = (_, _, _) =>
        {
            statusWhileEngineRuns.AddRange(_unitOfWork.SavedStatuses);
            return Task.CompletedTask;
        };

        await CreateHandler().HandleAsync(Command(), TestContext.Current.CancellationToken);

        Assert.Equal([BacktestStatus.Running], statusWhileEngineRuns);
        Assert.Equal([BacktestStatus.Running, BacktestStatus.Completed], _unitOfWork.SavedStatuses);
        Assert.Equal(BacktestStatus.Completed, Assert.Single(_runs.Runs).Status);
    }

    [Fact]
    public async Task Successful_run_returns_its_id_and_publishes_completion()
    {
        var result = await CreateHandler().HandleAsync(Command(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("20261015-120000000_AAPL_SmaCrossStrategy", result.Value.RunId);
        Assert.Equal("AAPL", result.Value.Symbol);

        var request = Assert.Single(_engine.Requests);
        Assert.Equal("26", request.Parameters.Values[StrategyParameterNames.FastPeriod]);
        Assert.Equal(100_000m, request.InitialCapital.Amount);

        var completed = Assert.IsType<BacktestRunCompleted>(Assert.Single(_events.Published));
        Assert.Equal(result.Value.RunId, completed.RunId.Value);
    }

    [Fact]
    public async Task Engine_progress_is_forwarded_to_the_caller()
    {
        var reported = new List<int>();
        var command = Command() with { Progress = new SyncProgress<BacktestProgress>(p => reported.Add(p.Percent)) };

        await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.Equal([50, 100], reported);
    }

    [Fact]
    public async Task Engine_error_becomes_a_failure_result_and_failed_event()
    {
        _engine.Behavior = (_, _, _) => throw new BacktestEngineException("no data");

        var result = await CreateHandler().HandleAsync(Command(), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("Backtest.EngineFailed", result.Error.Code);
        var failed = Assert.IsType<BacktestRunFailed>(Assert.Single(_events.Published));
        Assert.Equal("no data", failed.Reason);
    }

    [Fact]
    public async Task Cancellation_propagates_and_publishes_cancelled_event()
    {
        using var cts = new CancellationTokenSource();
        _engine.Behavior = (_, progress, ct) =>
        {
            progress?.Report(new BacktestProgress(30));
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateHandler().HandleAsync(Command(), cts.Token));

        var cancelled = Assert.IsType<BacktestRunCancelled>(Assert.Single(_events.Published));
        Assert.Equal(30, cancelled.Progress);
        Assert.Equal([BacktestStatus.Running, BacktestStatus.Cancelled], _unitOfWork.SavedStatuses);
    }

    [Fact]
    public async Task Unexpected_exception_still_records_a_failed_run()
    {
        _engine.Behavior = (_, _, _) => throw new IOException("disk full");

        await Assert.ThrowsAsync<IOException>(() => CreateHandler().HandleAsync(Command(), TestContext.Current.CancellationToken));

        Assert.IsType<BacktestRunFailed>(Assert.Single(_events.Published));
        Assert.Equal(BacktestStatus.Failed, Assert.Single(_runs.Runs).Status);
    }

    private RunBacktestCommandHandler CreateHandler() => new(_engine, _runs, _unitOfWork, _clock);

    internal static RunBacktestCommand Command(int fast = 26, int slow = 130) => new(
        "SmaCrossStrategy",
        "aapl",
        new DateOnly(2026, 1, 2),
        new DateOnly(2026, 9, 30),
        100_000m,
        new Dictionary<string, string>
        {
            [StrategyParameterNames.FastPeriod] = fast.ToString(),
            [StrategyParameterNames.SlowPeriod] = slow.ToString()
        });
}

public class RunBacktestCommandValidatorTests
{
    private readonly RunBacktestCommandValidator _validator = new(new FakeTimeProvider(TestData.Now));

    [Fact]
    public void Valid_command_passes()
    {
        Assert.True(_validator.Validate(RunBacktestCommandHandlerTests.Command()).IsValid);
    }

    [Theory]
    [InlineData(130, 26)]
    [InlineData(50, 50)]
    [InlineData(0, 10)]
    public void Slow_period_must_exceed_a_positive_fast_period(int fast, int slow)
    {
        var result = _validator.Validate(RunBacktestCommandHandlerTests.Command(fast, slow));

        Assert.Contains(result.Errors, e => e.ErrorMessage.StartsWith("Slow period"));
    }

    [Fact]
    public void Collects_every_broken_rule()
    {
        var command = RunBacktestCommandHandlerTests.Command() with
        {
            Symbol = "not a symbol",
            Start = new DateOnly(2026, 12, 1),
            End = new DateOnly(2026, 11, 1),
            InitialCapital = 0
        };

        var messages = _validator.Validate(command).Errors.Select(e => e.ErrorMessage).ToList();

        Assert.Contains("'not a symbol' is not a valid symbol (e.g. AAPL).", messages);
        Assert.Contains("End date must not be before start date.", messages);
        Assert.Contains("Initial capital must be positive.", messages);
    }

    [Fact]
    public void End_date_must_not_be_in_the_future()
    {
        var command = RunBacktestCommandHandlerTests.Command() with { End = new DateOnly(2026, 10, 16) };

        Assert.Contains(_validator.Validate(command).Errors, e => e.ErrorMessage.Contains("must not be in the future"));
    }
}
