using EmTrading.Application.Abstractions.Messaging;

namespace EmTrading.Application.Backtesting.RunBacktest;

/// <param name="Parameters">Strategy inputs by name (see <see cref="Strategies.StrategyDescriptor"/>); missing ones use their defaults.</param>
public sealed record RunBacktestCommand(
    string StrategyName,
    string Symbol,
    DateOnly Start,
    DateOnly End,
    decimal InitialCapital,
    IReadOnlyDictionary<string, string> Parameters) : ICommand<BacktestRunResult>
{
    /// <summary>Optional live progress for the caller; not part of the command's data.</summary>
    public IProgress<BacktestProgress>? Progress { get; init; }
}

/// <summary>
/// Identifies a completed run; pass it to <c>GetBacktestReportQuery</c> to load the report.
/// </summary>
public sealed record BacktestRunResult(string RunId, string Symbol, string StrategyName);
