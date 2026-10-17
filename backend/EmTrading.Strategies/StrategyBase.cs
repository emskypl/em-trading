using System.Globalization;
using System.Reflection;
using EmTrading.Strategies.Metadata;
using QuantConnect;
using QuantConnect.Algorithm;

namespace EmTrading.Strategies;

/// <summary>
/// Common setup for the app's strategies: period, cash and symbol come from <see cref="EngineParameters"/>,
/// and strategy inputs fall back to the defaults declared in their <see cref="StrategyParameterAttribute"/>.
/// </summary>
public abstract class StrategyBase : QCAlgorithm
{
    /// <summary>
    /// Applies the engine parameters and subscribes to the requested equity.
    /// </summary>
    protected Symbol SetUpFromEngineParameters(Resolution resolution)
    {
        SetStartDate(GetDateParameter(EngineParameters.StartDate, new DateTime(2025, 1, 1)));
        SetEndDate(GetDateParameter(EngineParameters.EndDate, DateTime.Today));
        SetCash(GetParameter(EngineParameters.Cash, 100_000m));

        var ticker = GetParameter(EngineParameters.Symbol, "AAPL").Trim().ToUpperInvariant();
        return AddEquity(ticker, resolution).Symbol;
    }

    protected int IntegerParameter(string name) => GetParameter(name, (int)Describe(name).DefaultValue);

    protected decimal DecimalParameter(string name) => GetParameter(name, (decimal)Describe(name).DefaultValue);

    private StrategyParameterAttribute Describe(string name) =>
        GetType().GetCustomAttributes<StrategyParameterAttribute>().SingleOrDefault(a => a.Name == name)
        ?? throw new InvalidOperationException($"{GetType().Name} does not declare parameter '{name}'.");

    private DateTime GetDateParameter(string name, DateTime defaultValue) =>
        DateTime.TryParseExact(GetParameter(name), EngineParameters.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : defaultValue;
}
