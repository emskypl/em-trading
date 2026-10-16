namespace EmTrading.Application.Backtesting;

/// <param name="Parameters">Strategy-specific parameters, e.g. <c>fast-period</c>.</param>
public sealed record BacktestRequest(
    string StrategyName,
    string Symbol,
    DateOnly Start,
    DateOnly End,
    decimal InitialCash,
    IReadOnlyDictionary<string, string> Parameters);

/// <summary>
/// Identifies a finished engine run so its report can be loaded.
/// </summary>
public sealed record BacktestRunResult(string RunId, string Symbol, string StrategyName);

/// <param name="Percent">0 to 100.</param>
public readonly record struct BacktestProgress(int Percent, string? Message = null);
