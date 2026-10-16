namespace EmTrading.Application.Backtesting;

public interface IBacktestEngine
{
    /// <summary>
    /// Runs a backtest to completion. Cancelling the token stops the engine.
    /// </summary>
    Task<BacktestRunResult> RunAsync(
        BacktestRequest request,
        IProgress<BacktestProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class BacktestEngineException(string message) : Exception(message);
