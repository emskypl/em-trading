using EmTrading.Application.Backtesting.Strategies;
using EmTrading.Domain.Backtesting;
using EmTrading.Infrastructure.Lean.Strategies;
using EmTrading.Strategies;

namespace EmTrading.Infrastructure.Tests.Engine;

/// <summary>
/// The real strategies assembly, discovered by reflection.
/// </summary>
public class LeanStrategyCatalogTests
{
    private readonly LeanStrategyCatalog _catalog = new();

    [Fact]
    public void Discovers_both_strategies_with_their_declared_parameters()
    {
        Assert.Equal(["RsiMeanReversionStrategy", "SmaCrossStrategy"], _catalog.GetAll().Select(s => s.Name).Order());

        var smaCross = _catalog.Find(nameof(SmaCrossStrategy))!;
        var slow = smaCross.Parameters.Single(p => p.Name == SmaCrossStrategy.SlowPeriod);
        Assert.Equal((StrategyParameterKind.Integer, 130m, 3m, 2000m, SmaCrossStrategy.FastPeriod),
            (slow.Kind, slow.DefaultValue, slow.Minimum, slow.Maximum, slow.MustExceed));

        var atr = smaCross.Parameters.Single(p => p.Name == SmaCrossStrategy.AtrStopMultiplier);
        Assert.Equal((StrategyParameterKind.Decimal, 1.5m), (atr.Kind, atr.DefaultValue));
    }

    [Fact]
    public void Every_strategy_is_usable_with_its_defaults()
    {
        Assert.All(_catalog.GetAll(), strategy =>
        {
            Assert.True(StrategyName.IsValid(strategy.Name), $"{strategy.Name} is not a valid strategy name");
            Assert.Empty(strategy.Validate(new Dictionary<string, string>()));
            Assert.All(strategy.Parameters, p => Assert.True(StrategyParameters.IsValidName(p.Name), $"{strategy.Name}.{p.Name}"));
            Assert.All(strategy.Parameters.Where(p => p.MustExceed is not null),
                p => Assert.Contains(strategy.Parameters, other => other.Name == p.MustExceed));
        });
    }

    [Fact]
    public void Unknown_strategy_is_not_found()
    {
        Assert.Null(_catalog.Find("StrategyBase"));
        Assert.Null(_catalog.Find("smacrossstrategy"));
    }
}
