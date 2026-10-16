using System.Globalization;
using EmTrading.Domain.Common;
using EmTrading.Domain.MarketData;

namespace EmTrading.Domain.Backtesting;

/// <summary>
/// Readable, sortable run id, e.g. <c>20261015-233403029_AAPL_SmaCrossStrategy</c>. It is also the result file name.
/// </summary>
public sealed record BacktestRunId
{
    private BacktestRunId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static BacktestRunId New(Symbol symbol, StrategyName strategy, DateTimeOffset createdAt) =>
        new($"{createdAt.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)}_{symbol.Value}_{strategy.Value}");

    /// <summary>
    /// Restores an existing id. Only characters that are safe in file names are accepted.
    /// </summary>
    public static BacktestRunId From(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
            ? new BacktestRunId(value)
            : throw new DomainException($"'{value}' is not a valid backtest run id.");

    public override string ToString() => Value;
}
