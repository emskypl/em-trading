using EmTrading.Application.Abstractions.Events;
using EmTrading.Application.Backtesting;
using EmTrading.Application.MarketData;
using EmTrading.Domain.Backtesting;
using EmTrading.Domain.Common;
using EmTrading.Domain.MarketData;

namespace EmTrading.Application.Tests.TestSupport;

internal sealed class FakeBacktestEngine : IBacktestEngine
{
    public List<BacktestRequest> Requests { get; } = [];

    /// <summary>Runs inside RunAsync; throw from it to simulate engine failures.</summary>
    public Func<BacktestRequest, IProgress<BacktestProgress>?, CancellationToken, Task> Behavior { get; set; } =
        (_, progress, _) =>
        {
            progress?.Report(new BacktestProgress(50));
            progress?.Report(new BacktestProgress(100));
            return Task.CompletedTask;
        };

    public Task RunAsync(BacktestRequest request, IProgress<BacktestProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Behavior(request, progress, cancellationToken);
    }
}

internal sealed class CountingResultReader : IBacktestResultReader
{
    private int _readCalls;
    private int _latestCalls;

    public int ReadCalls => _readCalls;
    public int LatestCalls => _latestCalls;

    public BacktestReport? Latest { get; set; } = TestData.Report("latest");

    public Task<BacktestReport?> ReadAsync(Symbol symbol, BacktestRunId runId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _readCalls);
        return Task.FromResult<BacktestReport?>(TestData.Report(runId.Value));
    }

    public Task<BacktestReport?> ReadLatestAsync(Symbol symbol, StrategyName strategy, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _latestCalls);
        return Task.FromResult(Latest);
    }
}

internal sealed class RecordingEventPublisher : IDomainEventPublisher
{
    public List<IDomainEvent> Published { get; } = [];

    public Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        Published.AddRange(domainEvents);
        return Task.CompletedTask;
    }
}

/// <summary>Lets the container validate every handler registration without real infrastructure.</summary>
internal sealed class UnusedMarketDataDownloader : IMarketDataDownloader
{
    public Task<MarketDataDownloadResult> DownloadAsync(MarketDataRequest request, IProgress<int>? progress = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    public static BacktestReport Report(string runId) =>
        new(runId, new Dictionary<string, string>(), new TradeStatistics(0, 0, 0, 0), [], []);
}
