using EmTrading.Domain.Backtesting;
using EmTrading.Domain.MarketData;

namespace EmTrading.Application.Backtesting;

/// <summary>
/// Read side for backtest history. Returns flat read models straight from storage without loading aggregates.
/// </summary>
public interface IBacktestHistoryReader
{
    /// <param name="symbol">Only runs for this symbol, or every run when <c>null</c>.</param>
    /// <returns>Newest first.</returns>
    Task<IReadOnlyList<BacktestRunSummary>> ListAsync(Symbol? symbol, int take, CancellationToken cancellationToken = default);
}

public sealed record BacktestRunSummary(
    string RunId,
    string Symbol,
    string StrategyName,
    DateOnly Start,
    DateOnly End,
    decimal InitialCapital,
    BacktestStatus Status,
    int Progress,
    DateTimeOffset CreatedAt,
    TimeSpan? Duration,
    string? FailureReason)
{
    public bool HasReport => Status == BacktestStatus.Completed;
}
