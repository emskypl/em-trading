using System.Globalization;
using EmTrading.Application.Common;
using EmTrading.Domain.Backtesting;
using FluentValidation;

namespace EmTrading.Application.Backtesting.RunBacktest;

internal sealed class RunBacktestCommandValidator : AbstractValidator<RunBacktestCommand>
{
    public RunBacktestCommandValidator(TimeProvider clock)
    {
        RuleFor(c => c.StrategyName).MustBeStrategyName();
        RuleFor(c => c.Symbol).MustBeSymbol();
        RuleFor(c => c.End)
            .GreaterThanOrEqualTo(c => c.Start).WithMessage("End date must not be before start date.")
            .MustNotBeInTheFuture(clock);
        RuleFor(c => c.InitialCapital).GreaterThan(0).WithMessage("Initial capital must be positive.");

        RuleForEach(c => c.Parameters)
            .Must(p => StrategyParameters.IsValidName(p.Key) && !string.IsNullOrWhiteSpace(p.Value))
            .WithMessage("Parameter '{PropertyValue}' is invalid.");

        RuleFor(c => c.Parameters)
            .Must(HaveSlowPeriodAboveFastPeriod)
            .WithMessage("Slow period must be greater than fast period, and both must be positive.");
    }

    private static bool HaveSlowPeriodAboveFastPeriod(IReadOnlyDictionary<string, string> parameters)
    {
        var hasFast = TryGetInt(parameters, StrategyParameterNames.FastPeriod, out var fast);
        var hasSlow = TryGetInt(parameters, StrategyParameterNames.SlowPeriod, out var slow);
        return !hasFast || !hasSlow || (fast > 0 && slow > fast);
    }

    private static bool TryGetInt(IReadOnlyDictionary<string, string> parameters, string name, out int value)
    {
        value = 0;
        return parameters.TryGetValue(name, out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
