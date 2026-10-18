namespace EmTrading.Domain.Optimization;

/// <summary>What an optimization maximizes.</summary>
public enum OptimizationObjective
{
    SharpeRatio,
    NetProfit,

    /// <summary>Net profit divided by maximum drawdown: rewards return earned with shallow drawdowns.</summary>
    ReturnOverDrawdown
}

/// <summary>
/// Key figures of one finished trial, calculated by the app from the run's equity curve and trades so every
/// trial is scored the same way.
/// </summary>
/// <param name="NetProfitPercent">e.g. 12.5 for +12.5%.</param>
/// <param name="MaxDrawdownPercent">Deepest fall from a peak, as a positive number, e.g. 8.2.</param>
/// <param name="SharpeRatio">Annualized, from daily returns.</param>
/// <param name="WinRatePercent">Share of closed trades with a profit, e.g. 55.</param>
public sealed record TrialMetrics(
    decimal NetProfitPercent,
    decimal MaxDrawdownPercent,
    decimal SharpeRatio,
    int TotalTrades,
    decimal WinRatePercent,
    decimal ProfitFactor)
{
    /// <summary>Drawdowns below this are treated as this, so a nearly flat equity curve does not get an extreme score.</summary>
    private const decimal MinimumDrawdownPercent = 1m;

    /// <summary>Higher is better for every objective.</summary>
    public decimal Score(OptimizationObjective objective) => objective switch
    {
        OptimizationObjective.SharpeRatio => SharpeRatio,
        OptimizationObjective.NetProfit => NetProfitPercent,
        OptimizationObjective.ReturnOverDrawdown => Math.Round(NetProfitPercent / Math.Max(MaxDrawdownPercent, MinimumDrawdownPercent), 4),
        _ => throw new ArgumentOutOfRangeException(nameof(objective), objective, null)
    };
}
