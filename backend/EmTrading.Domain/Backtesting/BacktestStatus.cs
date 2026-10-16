namespace EmTrading.Domain.Backtesting;

public enum BacktestStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}
