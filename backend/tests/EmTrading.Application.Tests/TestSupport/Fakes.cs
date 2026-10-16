using EmTrading.Application.Abstractions.Events;
using EmTrading.Application.Abstractions.Jobs;
using EmTrading.Application.Abstractions.Messaging;
using EmTrading.Application.Abstractions.Persistence;
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

/// <summary>
/// All repositories and readers over in-memory lists, shared across DI scopes like a real database would be.
/// </summary>
internal sealed class InMemoryDatabase :
    IBacktestRunRepository, IBacktestHistoryReader, IMarketDataSetRepository, IMarketDataCoverageReader
{
    public List<BacktestRun> Runs { get; } = [];

    public List<MarketDataSet> MarketDataSets { get; } = [];

    public IEnumerable<IHasDomainEvents> Aggregates => Runs.Cast<IHasDomainEvents>().Concat(MarketDataSets);

    public void Add(BacktestRun run) => Runs.Add(run);

    public Task<BacktestRun?> GetAsync(BacktestRunId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<BacktestRun>> ListUnfinishedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BacktestRun>>(Runs.Where(r => !r.IsFinished).ToList());

    public Task<IReadOnlyList<BacktestRunSummary>> ListAsync(Symbol? symbol, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BacktestRunSummary>>(Runs
            .Where(r => symbol is null || r.Symbol == symbol)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .Select(r => new BacktestRunSummary(r.Id.Value, r.Symbol.Value, r.Strategy.Value, r.Period.Start, r.Period.End,
                r.InitialCapital.Amount, r.Status, r.Progress, r.CreatedAt, r.Duration, r.FailureReason))
            .ToList());

    public void Add(MarketDataSet set) => MarketDataSets.Add(set);

    public Task<MarketDataSet?> GetAsync(MarketDataSetId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(MarketDataSets.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<MarketDataCoverage>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MarketDataCoverage>>(MarketDataSets
            .Select(s => new MarketDataCoverage(s.Symbol.Value, s.Timeframe, s.Coverage, s.LastDownloadedAt))
            .ToList());
}

/// <summary>
/// Mirrors the real unit of work: "commits" (records each run's status), then publishes the raised events.
/// </summary>
internal sealed class InMemoryUnitOfWork(InMemoryDatabase database, IDomainEventPublisher publisher) : IUnitOfWork
{
    public List<BacktestStatus> SavedStatuses { get; } = [];

    public int SaveCount { get; private set; }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        SavedStatuses.AddRange(database.Runs.Select(r => r.Status));
        var events = database.Aggregates.SelectMany(a => a.DequeueDomainEvents()).ToList();
        await publisher.PublishAsync(events, cancellationToken);
    }
}

/// <summary>Records requests and returns a configurable number of bars per downloaded range.</summary>
internal sealed class FakeMarketDataDownloader : IMarketDataDownloader
{
    public List<MarketDataRequest> Requests { get; } = [];

    public Func<MarketDataRequest, int> BarsFor { get; set; } = request => request.Period.Days * 10;

    public Task<MarketDataDownloadResult> DownloadAsync(MarketDataRequest request, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        progress?.Report(50);
        progress?.Report(100);
        var bars = BarsFor(request);
        return Task.FromResult(new MarketDataDownloadResult(request.Symbol.Value, bars, bars > 0 ? 1 : 0));
    }
}

/// <summary>
/// Collects jobs instead of running them in the background; <see cref="RunAllAsync"/> runs them inline.
/// </summary>
internal sealed class InlineJobQueue : IJobQueue
{
    public List<(Guid Id, string Title, string? Key, JobWork Work)> Pending { get; } = [];

    public JobEnqueueResult Enqueue(string title, JobWork work, string? key = null)
    {
        var existing = Pending.FirstOrDefault(p => key is not null && p.Key == key);
        if (existing.Work is not null)
            return new JobEnqueueResult(Snapshot(existing.Id, existing.Title, existing.Key), WasQueued: false);

        var id = Guid.NewGuid();
        Pending.Add((id, title, key, work));
        return new JobEnqueueResult(Snapshot(id, title, key), WasQueued: true);
    }

    public bool TryCancel(Guid jobId) => Pending.RemoveAll(p => p.Id == jobId) > 0;

    public void ClearFinished()
    {
    }

    public async Task<List<JobOutcome>> RunAllAsync(IDispatcher dispatcher)
    {
        var outcomes = new List<JobOutcome>();
        foreach (var job in Pending.ToList())
            outcomes.Add(await job.Work(new JobExecutionContext(dispatcher, new SyncProgress<JobProgress>(_ => { }), CancellationToken.None)));

        Pending.Clear();
        return outcomes;
    }

    private static JobSnapshot Snapshot(Guid id, string title, string? key) =>
        new(id, title, key, JobStatus.Queued, 0, null, TestData.Now, null, null);
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
