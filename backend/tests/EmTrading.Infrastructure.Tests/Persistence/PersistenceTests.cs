using EmTrading.Application.Abstractions.Events;
using EmTrading.Domain.Backtesting;
using EmTrading.Domain.Common;
using EmTrading.Domain.MarketData;
using EmTrading.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EmTrading.Infrastructure.Tests.Persistence;

/// <summary>
/// Real SQLite (in memory) with the real migrations, so mapping, conversions and SQL translation are all exercised.
/// </summary>
public sealed class PersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly RecordingPublisher _publisher = new();

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Migrations_match_the_current_model()
    {
        await using var db = CreateContext();

        Assert.False(db.Database.HasPendingModelChanges(), "The model changed; add a migration with dotnet ef migrations add.");
    }

    [Fact]
    public async Task Backtest_run_round_trips_with_all_value_objects()
    {
        var ct = TestContext.Current.CancellationToken;
        var run = CreateRun("AAPL", T0);
        run.Start(T0.AddSeconds(1));
        run.ReportProgress(40);
        run.Cancel(T0.AddSeconds(31));

        await using (var db = CreateContext())
        {
            new BacktestRunRepository(db).Add(run);
            await new UnitOfWork(db, _publisher).SaveChangesAsync(ct);
        }

        await using (var db = CreateContext())
        {
            var loaded = await new BacktestRunRepository(db).GetAsync(run.Id, ct);

            Assert.NotNull(loaded);
            Assert.Equal(run.Symbol, loaded.Symbol);
            Assert.Equal(run.Strategy, loaded.Strategy);
            Assert.Equal(run.Period, loaded.Period);
            Assert.Equal(run.InitialCapital, loaded.InitialCapital);
            Assert.Equal(run.Parameters, loaded.Parameters);
            Assert.Equal(BacktestStatus.Cancelled, loaded.Status);
            Assert.Equal(40, loaded.Progress);
            Assert.Equal(TimeSpan.FromSeconds(30), loaded.Duration);
            Assert.Equal(TimeSpan.Zero, loaded.CreatedAt.Offset);
            Assert.Empty(loaded.DomainEvents);
        }
    }

    [Fact]
    public async Task Unit_of_work_publishes_events_only_after_saving()
    {
        var ct = TestContext.Current.CancellationToken;
        var run = CreateRun("AAPL", T0);
        run.Start(T0);
        run.Complete(T0.AddSeconds(5));

        await using var db = CreateContext();
        _publisher.OnPublish = () =>
        {
            // The handler must already see the committed row.
            using var other = CreateContext();
            Assert.Equal(BacktestStatus.Completed, other.BacktestRuns.AsNoTracking().Single().Status);
        };

        new BacktestRunRepository(db).Add(run);
        await new UnitOfWork(db, _publisher).SaveChangesAsync(ct);

        Assert.IsType<BacktestRunCompleted>(Assert.Single(_publisher.Published));
        Assert.Empty(run.DomainEvents);
    }

    [Fact]
    public async Task Changes_to_a_loaded_run_are_tracked_and_saved()
    {
        var ct = TestContext.Current.CancellationToken;
        var run = CreateRun("MSFT", T0);
        run.Start(T0);
        await SaveAsync(run);

        await using (var db = CreateContext())
        {
            var repository = new BacktestRunRepository(db);
            var unfinished = Assert.Single(await repository.ListUnfinishedAsync(ct));
            unfinished.Fail("Interrupted", T0.AddMinutes(1));
            await new UnitOfWork(db, _publisher).SaveChangesAsync(ct);
        }

        await using (var db = CreateContext())
        {
            Assert.Empty(await new BacktestRunRepository(db).ListUnfinishedAsync(ct));
            Assert.Equal("Interrupted", (await new BacktestRunRepository(db).GetAsync(run.Id, ct))!.FailureReason);
        }
    }

    [Fact]
    public async Task History_is_filtered_by_symbol_newest_first()
    {
        var ct = TestContext.Current.CancellationToken;
        var older = CreateRun("AAPL", T0);
        older.Start(T0);
        older.Complete(T0.AddSeconds(12));
        await SaveAsync(older);
        await SaveAsync(CreateRun("MSFT", T0.AddMinutes(1)));
        var newer = CreateRun("AAPL", T0.AddMinutes(2));
        await SaveAsync(newer);

        await using var db = CreateContext();
        var reader = new BacktestHistoryReader(db);

        var aapl = await reader.ListAsync(Symbol.Create("AAPL"), take: 10, ct);
        Assert.Equal([newer.Id.Value, older.Id.Value], aapl.Select(r => r.RunId));
        Assert.Equal(TimeSpan.FromSeconds(12), aapl[1].Duration);
        Assert.Equal(100_000m, aapl[1].InitialCapital);
        Assert.True(aapl[1].HasReport);

        Assert.Equal(2, (await reader.ListAsync(null, take: 2, ct)).Count);
    }

    [Fact]
    public async Task Market_data_set_coverage_round_trips_and_changes_are_detected()
    {
        var ct = TestContext.Current.CancellationToken;
        var completeThrough = new DateOnly(2026, 3, 31);
        var set = MarketDataSet.Create(Symbol.Create("MSFT"), Timeframe.Daily);
        set.RecordDownload(DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)), completeThrough, 21, T0);

        await using (var db = CreateContext())
        {
            new MarketDataSetRepository(db).Add(set);
            await new UnitOfWork(db, _publisher).SaveChangesAsync(ct);
        }

        // Load, extend the coverage (the aggregate replaces its list), save again.
        await using (var db = CreateContext())
        {
            var loaded = await new MarketDataSetRepository(db).GetAsync(set.Id, ct);
            Assert.NotNull(loaded);
            loaded.RecordDownload(DateRange.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 10)), completeThrough, 7, T0.AddDays(1));
            await new UnitOfWork(db, _publisher).SaveChangesAsync(ct);
        }

        await using (var db = CreateContext())
        {
            var coverage = Assert.Single(await new MarketDataCoverageReader(db).ListAsync(ct));
            Assert.Equal("MSFT", coverage.Symbol);
            Assert.Equal(Timeframe.Daily, coverage.Timeframe);
            Assert.Equal("2026-01-01..2026-01-31; 2026-03-01..2026-03-10", coverage.RangesText);
            Assert.Equal(T0.AddDays(1), coverage.LastDownloadedAt);
        }

        Assert.Equal(2, _publisher.Published.OfType<MarketDataDownloaded>().Count());
    }

    private TradingDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TradingDbContext>().UseSqlite(_connection).Options);

    private async Task SaveAsync(BacktestRun run)
    {
        await using var db = CreateContext();
        new BacktestRunRepository(db).Add(run);
        await new UnitOfWork(db, _publisher).SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static BacktestRun CreateRun(string ticker, DateTimeOffset createdAt)
    {
        var symbol = Symbol.Create(ticker);
        var strategy = StrategyName.Create("SmaCrossStrategy");
        return BacktestRun.Create(
            BacktestRunId.New(symbol, strategy, createdAt),
            symbol,
            strategy,
            DateRange.Create(new DateOnly(2026, 1, 2), new DateOnly(2026, 9, 30)),
            Money.Usd(100_000m),
            StrategyParameters.Create([new("fast-period", "26"), new("slow-period", "130")]),
            createdAt);
    }

    private sealed class RecordingPublisher : IDomainEventPublisher
    {
        public List<IDomainEvent> Published { get; } = [];

        public Action? OnPublish { get; set; }

        public Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            var events = domainEvents.ToList();
            if (events.Count > 0)
                OnPublish?.Invoke();

            Published.AddRange(events);
            return Task.CompletedTask;
        }
    }
}
