namespace EmTrading.Application.Backtesting;

public interface IBacktestResultReader
{
    Task<BacktestReport> ReadAsync(BacktestRunResult run, CancellationToken cancellationToken = default);

    /// <returns>The most recent report for the symbol and strategy, or <c>null</c> if none exists.</returns>
    Task<BacktestReport?> ReadLatestAsync(string symbol, string strategyName, CancellationToken cancellationToken = default);
}
