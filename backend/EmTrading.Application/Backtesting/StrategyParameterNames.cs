namespace EmTrading.Application.Backtesting;

/// <summary>
/// Parameter names understood by the strategies. Values are passed as invariant-culture strings.
/// </summary>
public static class StrategyParameterNames
{
    public const string DateFormat = "yyyy-MM-dd";

    public const string Symbol = "symbol";
    public const string StartDate = "start-date";
    public const string EndDate = "end-date";
    public const string Cash = "cash";
    public const string FastPeriod = "fast-period";
    public const string SlowPeriod = "slow-period";
}
