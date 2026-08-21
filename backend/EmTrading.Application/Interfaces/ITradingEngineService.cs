using EmTrading.Application.Events;

namespace EmTrading.Application.Interfaces;

public interface ITradingEngineService
{
    Task RunBacktestAsync(string strategyName, DateTime start, DateTime end, string symbol, CancellationToken cancellationToken = default);

    event EventHandler<BacktestProgressEventArgs>? ProgressUpdated;
    event EventHandler<TradeExecutedEventArgs>? TradeExecuted;
}