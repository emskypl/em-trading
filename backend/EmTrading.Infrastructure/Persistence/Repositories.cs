using EmTrading.Application.Abstractions.Events;
using EmTrading.Application.Abstractions.Persistence;
using EmTrading.Application.Backtesting;
using EmTrading.Domain.Backtesting;
using EmTrading.Domain.Common;
using EmTrading.Domain.MarketData;
using Microsoft.EntityFrameworkCore;

namespace EmTrading.Infrastructure.Persistence;

internal sealed class UnitOfWork(TradingDbContext db, IDomainEventPublisher eventPublisher) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Remember which aggregates raised events, commit, and only then publish:
        // handlers must never react to changes that were rolled back.
        var aggregates = db.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var events = aggregates.SelectMany(aggregate => aggregate.DequeueDomainEvents()).ToList();
        if (events.Count > 0)
            await eventPublisher.PublishAsync(events, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class BacktestRunRepository(TradingDbContext db) : IBacktestRunRepository
{
    public void Add(BacktestRun run) => db.BacktestRuns.Add(run);

    public Task<BacktestRun?> GetAsync(BacktestRunId id, CancellationToken cancellationToken = default) =>
        db.BacktestRuns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<BacktestRun>> ListUnfinishedAsync(CancellationToken cancellationToken = default) =>
        await db.BacktestRuns
            .Where(r => r.Status == BacktestStatus.Queued || r.Status == BacktestStatus.Running)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

/// <summary>
/// Read side: a no-tracking projection of only the columns the history view needs.
/// </summary>
internal sealed class BacktestHistoryReader(TradingDbContext db) : IBacktestHistoryReader
{
    public async Task<IReadOnlyList<BacktestRunSummary>> ListAsync(Symbol? symbol, int take, CancellationToken cancellationToken = default)
    {
        var query = db.BacktestRuns.AsNoTracking();
        if (symbol is not null)
            query = query.Where(r => r.Symbol == symbol);

        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .Select(r => new
            {
                r.Id,
                r.Symbol,
                r.Strategy,
                r.Period.Start,
                r.Period.End,
                r.InitialCapital.Amount,
                r.Status,
                r.Progress,
                r.CreatedAt,
                r.StartedAt,
                r.FinishedAt,
                r.FailureReason
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new BacktestRunSummary(
                r.Id.Value,
                r.Symbol.Value,
                r.Strategy.Value,
                r.Start,
                r.End,
                r.Amount,
                r.Status,
                r.Progress,
                r.CreatedAt,
                r.StartedAt is { } started && r.FinishedAt is { } finished ? finished - started : null,
                r.FailureReason))
            .ToList();
    }
}
